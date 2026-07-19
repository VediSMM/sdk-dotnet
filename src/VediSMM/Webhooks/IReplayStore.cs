using System;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM.Webhooks;

/// <summary>Atomically claims webhook event IDs until expiry.</summary>
public interface IReplayStore
{
    /// <summary>Claims an event exactly once.</summary>
    ValueTask<bool> ClaimAsync(string eventId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
