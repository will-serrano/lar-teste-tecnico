using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Api.Diagnostics;
using CandidateAssessment.Application.Abstractions.Idempotency;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace CandidateAssessment.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    private const string Header = "Idempotency-Key";
    private readonly RequestDelegate _next;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, IOptions<IdempotencyOptions> options, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store, ProblemDetailsFactory problems)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>() is null
            || !context.Request.Headers.TryGetValue(Header, out var values))
        {
            await _next(context);
            return;
        }

        using var activity = IdempotencyDiagnostics.Source.StartActivity("idempotency");
        activity?.SetTag("operation", IdempotencyDiagnostics.Operation(context));
        var started = Stopwatch.StartNew();
        try
        {
            await ExecuteAsync(context, store, problems, values);
        }
        catch
        {
            IdempotencyDiagnostics.Record(context, "failed");
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            IdempotencyDiagnostics.RecordDuration(context, started.Elapsed);
        }
    }

    private async Task ExecuteAsync(HttpContext context, IIdempotencyStore store, ProblemDetailsFactory problems,
        Microsoft.Extensions.Primitives.StringValues values)
    {
        var key = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrEmpty(key) || key.Length > 128
            || key.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or ':' or '-')))
        {
            await RejectAsync(context, problems, 400, "InvalidIdempotencyKey", "Provide one valid Idempotency-Key value (1-128 ASCII characters).");
            return;
        }

        if (context.Request.ContentLength > _options.MaxRequestBytes)
        {
            await RejectAsync(context, problems, 413, "IdempotencyRequestTooLarge", "The request exceeds the idempotency buffer limit.");
            return;
        }

        var body = await ReadBodyAsync(context);
        if (body is null)
        {
            await RejectAsync(context, problems, 413, "IdempotencyRequestTooLarge", "The request exceeds the idempotency buffer limit.");
            return;
        }

        var fingerprint = RequestFingerprint.Create(context, body);
        if (fingerprint is null)
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An authenticated user identifier is required for idempotency.");
        var issuer = context.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value.Issuer;
        var scopeHash = RequestFingerprint.Hash(JsonSerializer.Serialize(new[] { issuer, userId }));
        var keyHash = RequestFingerprint.Hash(key);
        IdempotencyEntry? existing;
        try
        {
            existing = await store.FindAsync(scopeHash, keyHash, _options.LockTimeoutSeconds, context.RequestAborted);
        }
        catch (IdempotencyBusyException)
        {
            await RejectBusyAsync(context, problems);
            return;
        }
        if (existing is not null)
        {
            await ReplayAsync(context, problems, existing, fingerprint);
            return;
        }

        IIdempotencySession session;
        var waiting = Stopwatch.StartNew();
        try
        {
            session = await store.BeginAsync(scopeHash, keyHash, fingerprint, _options.LockTimeoutSeconds, context.RequestAborted);
        }
        catch (IdempotencyBusyException)
        {
            await RejectBusyAsync(context, problems);
            return;
        }
        finally
        {
            IdempotencyDiagnostics.RecordWait(context, waiting.Elapsed);
        }

        await using (session)
        {
            if (session.Existing is not null)
            {
                var confirmed = session.Existing;
                await session.DisposeAsync();
                await ReplayAsync(context, problems, confirmed, fingerprint);
                return;
            }

            var responseFeature = context.Features.Get<IHttpResponseFeature>()
                ?? throw new InvalidOperationException("The response feature is missing.");
            var bodyFeature = context.Features.Get<IHttpResponseBodyFeature>()
                ?? throw new InvalidOperationException("The response body feature is missing.");
            await using var buffered = new BufferedResponse(responseFeature, _options.MaxResponseBytes);
            context.Features.Set<IHttpResponseFeature>(buffered);
            context.Features.Set<IHttpResponseBodyFeature>(buffered);
            try
            {
                await _next(context);
                await buffered.CompleteAsync();
                var bytes = buffered.ToArray();
                if (buffered.StatusCode is 201 or 204)
                {
                    if (buffered.StatusCode == 204 && bytes.Length != 0)
                    {
                        throw new InvalidOperationException("A 204 idempotent response must have an empty body.");
                    }

                    var response = new IdempotencyResponse(buffered.StatusCode, bytes,
                        buffered.Headers.ContentType, buffered.Headers.Location);
                    await session.CompleteAsync(response, _options.Lifetime, context.RequestAborted);
                    buffered.Headers["Idempotency-Replayed"] = "false";
                    _logger.LogInformation("Idempotency operation {Outcome}.", "executed");
                    IdempotencyDiagnostics.Record(context, "executed");
                }
                else
                {
                    _logger.LogInformation("Idempotency operation {Outcome} with status {StatusCode}.", "rejected", buffered.StatusCode);
                    IdempotencyDiagnostics.Record(context, "rejected");
                    await session.DisposeAsync();
                }

                context.Features.Set(responseFeature);
                context.Features.Set(bodyFeature);
                responseFeature.StatusCode = buffered.StatusCode;
                responseFeature.ReasonPhrase = buffered.ReasonPhrase;
                // Kestrel's output producer retains the original header dictionary.
                responseFeature.Headers.Clear();
                foreach (var header in buffered.Headers)
                {
                    responseFeature.Headers[header.Key] = header.Value;
                }
                await SendBodyAsync(context, bytes);
            }
            finally
            {
                context.Features.Set(responseFeature);
                context.Features.Set(bodyFeature);
            }
        }
    }

    private async Task<byte[]?> ReadBodyAsync(HttpContext context)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > _options.MaxRequestBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), context.RequestAborted);
        }

        var bytes = buffer.ToArray();
        var replayBody = new MemoryStream(bytes, writable: false);
        context.Request.Body = replayBody;
        context.Response.OnCompleted(() =>
        {
            replayBody.Dispose();
            return Task.CompletedTask;
        });
        return bytes;
    }

    private async Task ReplayAsync(HttpContext context, ProblemDetailsFactory problems, IdempotencyEntry entry, string fingerprint)
    {
        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            await RejectAsync(context, problems, 409, "IdempotencyKeyReuse", "This key was already used with different input.");
            return;
        }

        context.Response.StatusCode = entry.Response.StatusCode;
        if (!string.IsNullOrEmpty(entry.Response.ContentType))
        {
            context.Response.ContentType = entry.Response.ContentType;
        }
        if (!string.IsNullOrEmpty(entry.Response.Location))
        {
            context.Response.Headers.Location = entry.Response.Location;
        }

        context.Response.Headers["Idempotency-Replayed"] = "true";
        _logger.LogInformation("Idempotency operation {Outcome}.", "replayed");
        IdempotencyDiagnostics.Record(context, "replayed");
        await SendBodyAsync(context, entry.Response.Body);
    }

    private static Task SendBodyAsync(HttpContext context, byte[] bytes)
        => bytes.Length == 0
            ? context.Response.StartAsync(context.RequestAborted)
            : context.Response.Body.WriteAsync(bytes, context.RequestAborted).AsTask();

    private async Task RejectAsync(HttpContext context, ProblemDetailsFactory problems, int status, string code, string detail)
    {
        _logger.LogWarning("Idempotency operation {Outcome} with status {StatusCode}.", code, status);
        IdempotencyDiagnostics.Record(context, code);
        var problem = problems.CreateProblemDetails(context, statusCode: status, detail: detail);
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = RequestCorrelation.GetId(context);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }

    private Task RejectBusyAsync(HttpContext context, ProblemDetailsFactory problems)
    {
        context.Response.Headers.RetryAfter = _options.LockTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        return RejectAsync(context, problems, 503, "IdempotencyStoreBusy", "The database is busy. Retry with the same key after Retry-After.");
    }
}
