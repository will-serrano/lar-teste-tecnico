using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace CandidateAssessment.Api.Idempotency;

internal sealed class BufferedResponse : IHttpResponseFeature, IHttpResponseBodyFeature, IAsyncDisposable
{
    private readonly IHttpResponseFeature _original;
    private readonly List<(Func<object, Task> Callback, object State)> _starting = new();
    private readonly LimitedStream _stream;
    private readonly PipeWriter _writer;
    private bool _completed;

    public BufferedResponse(IHttpResponseFeature original, int limit)
    {
        _original = original;
        StatusCode = original.StatusCode;
        ReasonPhrase = original.ReasonPhrase;
        Headers = new HeaderDictionary();
        foreach (var header in original.Headers)
        {
            Headers[header.Key] = header.Value;
        }

        _stream = new LimitedStream(limit);
        _writer = PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public int StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public IHeaderDictionary Headers { get; set; }

    public bool HasStarted { get; private set; }

    public Stream Body { get => _stream; set => throw new NotSupportedException("Replace the body feature instead."); }

    public Stream Stream => _stream;

    public PipeWriter Writer => _writer;

    public byte[] ToArray() => _stream.ToArray();

    public void OnStarting(Func<object, Task> callback, object state)
    {
        if (HasStarted)
        {
            throw new InvalidOperationException("The buffered response has already started.");
        }

        _starting.Add((callback, state));
    }

    public void OnCompleted(Func<object, Task> callback, object state) => _original.OnCompleted(callback, state);

    public void DisableBuffering()
        => throw new NotSupportedException("Streaming is not supported for an idempotent operation.");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasStarted)
        {
            return;
        }

        for (var i = _starting.Count - 1; i >= 0; i--)
        {
            await _starting[i].Callback(_starting[i].State);
        }

        HasStarted = true;
    }

    public async Task CompleteAsync()
    {
        if (_completed)
        {
            return;
        }

        await StartAsync();
        await _writer.CompleteAsync();
        _completed = true;
    }

    public async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(path);
        file.Position = offset;
        var remaining = count ?? file.Length - offset;
        var bytes = new byte[8192];
        while (remaining > 0)
        {
            var read = await file.ReadAsync(bytes.AsMemory(0, (int)Math.Min(remaining, bytes.Length)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await _stream.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_completed)
            {
                await _writer.CompleteAsync();
            }
        }
        finally
        {
            await _stream.DisposeAsync();
        }
    }

    private sealed class LimitedStream : MemoryStream
    {
        private readonly int _limit;

        public LimitedStream(int limit) => _limit = limit;

        private void Check(int count)
        {
            if (Position + count > _limit)
            {
                throw new InvalidOperationException("The idempotent response exceeded the configured buffer limit.");
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }

        public override void SetLength(long value)
        {
            if (value > _limit)
            {
                throw new InvalidOperationException("The idempotent response exceeded the configured buffer limit.");
            }

            base.SetLength(value);
        }
    }
}
