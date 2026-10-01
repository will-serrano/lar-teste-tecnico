using System.Diagnostics;
using System.Text.Json;
using CandidateAssessment.Application.Exceptions;
using CandidateAssessment.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Api.Middleware;

/// <summary>
/// Translates exceptions to RFC 7807 ProblemDetails.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        ProblemDetailsFactory problemDetailsFactory)
    {
        _next = next;
        _logger = logger;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problem) = MapException(context, exception);

        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} (traceId: {TraceId})",
                context.Request.Method,
                context.Request.Path,
                problem.Extensions["traceId"]);
        }
        else
        {
            _logger.LogInformation(
                "{ExceptionType} for {Method} {Path} -> {StatusCode}: {Message}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                statusCode,
                exception.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problem);
        await context.Response.WriteAsync(json);
    }

    private (int StatusCode, ProblemDetails Problem) MapException(
        HttpContext context,
        Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        return exception switch
        {
            ApplicationValidationException applicationEx => MapApplicationValidation(
                context,
                applicationEx,
                traceId),
            DomainException domainEx => MapDomainException(context, domainEx, traceId),
            DbUpdateException dbEx => MapDbUpdateException(context, dbEx, traceId),
            _ => MapUnexpected(context, exception, traceId),
        };
    }

    private (int StatusCode, ProblemDetails Problem) MapApplicationValidation(
        HttpContext context,
        ApplicationValidationException exception,
        string traceId)
    {
        var hasNotFound = exception.Errors.Any(e =>
            string.Equals(e.Code, "PersonNotFound", StringComparison.Ordinal)
            || string.Equals(e.Code, "PhoneNotFound", StringComparison.Ordinal));

        var hasConflict = exception.Errors.Any(e =>
            string.Equals(e.Code, "CpfAlreadyExists", StringComparison.Ordinal)
            || string.Equals(e.Code, "PhoneAlreadyExists", StringComparison.Ordinal)
            || string.Equals(e.Code, "Conflict", StringComparison.Ordinal));

        var hasUnauthorized = exception.Errors.Any(e =>
            string.Equals(e.Code, "InvalidCredentials", StringComparison.Ordinal));

        var statusCode = hasUnauthorized
            ? StatusCodes.Status401Unauthorized
            : hasNotFound
                ? StatusCodes.Status404NotFound
                : hasConflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

        var type = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        };

        var title = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status404NotFound => "Resource not found",
            StatusCodes.Status409Conflict => "Conflict",
            _ => "Validation failed",
        };

        var problem = _problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode,
            title,
            type);

        problem.Detail = exception.Message;
        problem.Extensions["traceId"] = traceId;

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            var errors = exception.Errors
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Code) ? "ValidationError" : e.Code)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Message).ToArray());

            problem.Extensions["errors"] = errors;
        }

        return (statusCode, problem);
    }

    private (int StatusCode, ProblemDetails Problem) MapDomainException(
        HttpContext context,
        DomainException exception,
        string traceId)
    {
        var problem = _problemDetailsFactory.CreateProblemDetails(
            context,
            StatusCodes.Status409Conflict,
            "Domain rule violated",
            "https://tools.ietf.org/html/rfc7231#section-6.5.8");

        problem.Detail = exception.Message;
        problem.Extensions["traceId"] = traceId;

        return (StatusCodes.Status409Conflict, problem);
    }

    private (int StatusCode, ProblemDetails Problem) MapDbUpdateException(
        HttpContext context,
        DbUpdateException exception,
        string traceId)
    {
        // SQLite reports UNIQUE constraint violations with "UNIQUE constraint failed".
        // Other providers use similar distinguishable messages. If the inner exception
        // matches, this is a concurrency conflict (e.g. duplicate CPF or duplicate
        // phone number inserted simultaneously); otherwise treat as unexpected.
        var isConstraintViolation = exception.InnerException is not null
            && exception.InnerException.Message.Contains(
                "UNIQUE constraint failed",
                StringComparison.OrdinalIgnoreCase);

        if (!isConstraintViolation)
        {
            return MapUnexpected(context, exception, traceId);
        }

        var problem = _problemDetailsFactory.CreateProblemDetails(
            context,
            StatusCodes.Status409Conflict,
            "Conflict",
            "https://tools.ietf.org/html/rfc7231#section-6.5.8");

        problem.Detail = "A record with the same unique value already exists.";
        problem.Extensions["traceId"] = traceId;

        return (StatusCodes.Status409Conflict, problem);
    }

    private (int StatusCode, ProblemDetails Problem) MapUnexpected(
        HttpContext context,
        Exception exception,
        string traceId)
    {
        var problem = _problemDetailsFactory.CreateProblemDetails(
            context,
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "https://tools.ietf.org/html/rfc7231#section-6.6.1");

        problem.Detail = "An unexpected error occurred.";
        problem.Extensions["traceId"] = traceId;

        return (StatusCodes.Status500InternalServerError, problem);
    }
}
