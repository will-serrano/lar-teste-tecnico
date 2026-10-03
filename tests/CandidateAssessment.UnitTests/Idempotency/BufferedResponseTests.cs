using System.Text;
using CandidateAssessment.Api.Idempotency;
using CandidateAssessment.Infrastructure.Caching;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;

namespace CandidateAssessment.UnitTests.Idempotency;

public sealed class BufferedResponseTests
{
    private static readonly int[] ExpectedCallbackOrder = [2, 1];

    [Fact]
    public async Task StartAndComplete_ShouldKeepOriginalResponseUnstarted_AndCapturePipeWriter()
    {
        var original = new HttpResponseFeature();
        await using var response = new BufferedResponse(original, 100);
        var order = new List<int>();
        response.OnStarting(_ =>
        {
            order.Add(1);
            response.Headers["Location"] = "/created";
            return Task.CompletedTask;
        }, new object());
        response.OnStarting(_ =>
        {
            order.Add(2);
            return Task.CompletedTask;
        }, new object());
        await response.StartAsync();
        await response.Writer.WriteAsync(Encoding.UTF8.GetBytes("{\"id\":1}"));
        await response.CompleteAsync();
        Assert.False(original.HasStarted);
        Assert.True(response.HasStarted);
        Assert.Equal(ExpectedCallbackOrder, order);
        Assert.Equal("/created", response.Headers["Location"]);
        Assert.Equal("{\"id\":1}", Encoding.UTF8.GetString(response.ToArray()));
    }

    [Fact]
    public async Task Stream_ShouldRejectOversizedResponseBeforeOriginalStarts()
    {
        var original = new HttpResponseFeature();
        await using var response = new BufferedResponse(original, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => response.Stream.WriteAsync(new byte[2], 0, 2));
        Assert.False(original.HasStarted);
        Assert.Empty(response.ToArray());
    }

    [Fact]
    public async Task TransactionalCache_ShouldBypassWritesAndDeferInvalidationUntilCommit()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var underlying = new MemoryCacheService(memory);
        var state = new TransactionalCacheState();
        var cache = new TransactionalCacheService(underlying, state);
        await cache.SetAsync("person", "old", TimeSpan.FromMinutes(1));
        state.Begin();
        Assert.Null(await cache.GetAsync<string>("person"));
        await cache.SetAsync("person", "uncommitted", TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("person");
        Assert.Equal("old", await underlying.GetAsync<string>("person"));
        await state.CommitAsync(underlying);
        Assert.Null(await cache.GetAsync<string>("person"));
        Assert.False(state.IsActive);
    }

    [Fact]
    public async Task TransactionalCache_Rollback_ShouldPreserveConfirmedCache()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var underlying = new MemoryCacheService(memory);
        var state = new TransactionalCacheState();
        var cache = new TransactionalCacheService(underlying, state);
        await cache.SetAsync("person", "confirmed", TimeSpan.FromMinutes(1));
        state.Begin();
        await cache.RemoveAsync("person");
        await cache.SetAsync("person", "rolled-back", TimeSpan.FromMinutes(1));
        state.Reset();
        Assert.Equal("confirmed", await cache.GetAsync<string>("person"));
    }
}
