using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;
using VediSMM.Helpers;
using VediSMM.Webhooks;
using Xunit;

namespace VediSMM.Tests;

public sealed class HelperTests
{
    private static readonly int[] FirstPageItems = { 1, 2 };
    private static readonly int[] SecondPageItems = { 3 };
    private static readonly int[] ExpectedItems = { 1, 2, 3 };

    [Fact]
    public async Task CursorPagerIsLazyAndRejectsLoops()
    {
        var cursors = new List<string?>();
        var pager = new CursorPager<int>((cursor, _) =>
        {
            cursors.Add(cursor);
            return Task.FromResult(cursor is null
                ? new Page<int>(FirstPageItems, "next")
                : new Page<int>(SecondPageItems, null));
        });

        Assert.Empty(cursors);
        var values = new List<int>();
        await foreach (int value in pager)
        {
            values.Add(value);
        }

        Assert.Equal(ExpectedItems, values);
        Assert.Equal(new string?[] { null, "next" }, cursors);

        var looping = new CursorPager<int>((_, _) => Task.FromResult(new Page<int>(Array.Empty<int>(), "same")));
        await Assert.ThrowsAsync<PaginationException>(async () =>
        {
            await foreach (int _ in looping)
            {
            }
        });
    }

    [Fact]
    public async Task JobWaiterIsFiniteAndTyped()
    {
        int calls = 0;
        var waiter = new JobWaiter(
            timeout: TimeSpan.FromSeconds(2),
            pollInterval: TimeSpan.FromMilliseconds(1),
            delayAsync: static (_, _) => Task.CompletedTask);
        JsonElement result = await waiter.WaitAsync(_ => Task.FromResult(Parse(++calls == 1
            ? "{\"id\":1,\"status\":\"running\"}"
            : "{\"id\":1,\"status\":\"succeeded\"}")), TestContext.Current.CancellationToken);
        Assert.Equal("succeeded", result.GetProperty("status").GetString());

        await Assert.ThrowsAsync<JobFailedException>(() => waiter.WaitAsync(_ => Task.FromResult(
            Parse("{\"id\":2,\"status\":\"failed\"}")), TestContext.Current.CancellationToken));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(
            _ => Task.FromResult(Parse("{\"status\":\"running\"}")), cancelled.Token));
    }

    [Fact]
    public void IdempotencyKeysAreUniqueUuidV4()
    {
        string[] keys = Enumerable.Range(0, 64).Select(_ => Idempotency.NewKey()).ToArray();
        Assert.Equal(64, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key =>
        {
            Assert.True(Guid.TryParseExact(key, "D", out Guid parsed));
            Assert.Equal(4, parsed.ToByteArray()[7] >> 4);
        });
    }

    [Fact]
    public async Task WebhookVerifierUsesExactRawBytesAndAtomicReplayClaims()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"message\":\"Привет\"}\n");
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1750000000);
        var store = new MemoryReplayStore(() => now);
        var verification = new WebhookVerification(
            secret: "whsec_test",
            timestamp: "1750000000",
            signature: "v1=3debe3bd870b690538c307a31dba517c3b9b613bf85d3070010f36759b494328",
            body: body,
            tolerance: TimeSpan.FromMinutes(5),
            now: now,
            eventId: "evt_1",
            replayStore: store);

        Assert.True(await WebhookVerifier.VerifyAsync(verification, TestContext.Current.CancellationToken));
        Assert.False(await WebhookVerifier.VerifyAsync(verification, TestContext.Current.CancellationToken));
        Assert.False(await WebhookVerifier.VerifyAsync(new WebhookVerification(
            "whsec_test",
            "1750000000",
            "v1=3debe3bd870b690538c307a31dba517c3b9b613bf85d3070010f36759b494328",
            Encoding.UTF8.GetBytes("changed"),
            TimeSpan.FromMinutes(5),
            DateTimeOffset.FromUnixTimeSeconds(1750000000)), TestContext.Current.CancellationToken));
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
