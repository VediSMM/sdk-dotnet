using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM.Webhooks;

/// <summary>Concurrency-safe single-process replay store.</summary>
public sealed class MemoryReplayStore : IReplayStore
{
    private readonly ConcurrentDictionary<string, long> expiries = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> utcNow;

    /// <summary>Creates a replay store using the system UTC clock.</summary>
    public MemoryReplayStore()
        : this(static () => DateTimeOffset.UtcNow)
    {
    }

    internal MemoryReplayStore(Func<DateTimeOffset> utcNow)
    {
        this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    /// <inheritdoc />
    public ValueTask<bool> ClaimAsync(
        string eventId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        long now = utcNow().UtcTicks;
        long expiry = expiresAt.UtcTicks;
        while (true)
        {
            if (!expiries.TryGetValue(eventId, out long existing))
            {
                return ValueTask.FromResult(expiries.TryAdd(eventId, expiry));
            }

            if (existing > now)
            {
                return ValueTask.FromResult(false);
            }

            if (expiries.TryUpdate(eventId, expiry, existing))
            {
                return ValueTask.FromResult(true);
            }
        }
    }
}
