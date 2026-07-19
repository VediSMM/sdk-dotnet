using System;

namespace VediSMM.Webhooks;

/// <summary>Exact raw-byte inputs for one webhook verification.</summary>
public sealed class WebhookVerification
{
    private readonly byte[] body;

    /// <summary>Creates immutable verification inputs.</summary>
    public WebhookVerification(
        string secret,
        string timestamp,
        string signature,
        byte[] body,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null,
        string? eventId = null,
        IReplayStore? replayStore = null)
    {
        Secret = secret;
        Timestamp = timestamp;
        Signature = signature;
        this.body = body is null
            ? throw new ArgumentNullException(nameof(body))
            : (byte[])body.Clone();
        Tolerance = tolerance;
        Now = now;
        EventId = eventId;
        ReplayStore = replayStore;
    }

    /// <summary>Gets the memory-only signing secret.</summary>
    public string Secret { get; }

    /// <summary>Gets the decimal Unix timestamp header.</summary>
    public string Timestamp { get; }

    /// <summary>Gets the <c>v1=</c> hexadecimal signature.</summary>
    public string Signature { get; }

    /// <summary>Gets a defensive copy of the exact request body.</summary>
    public byte[] Body => (byte[])body.Clone();

    /// <summary>Gets the accepted timestamp tolerance.</summary>
    public TimeSpan? Tolerance { get; }

    /// <summary>Gets the verification clock instant.</summary>
    public DateTimeOffset? Now { get; }

    /// <summary>Gets the optional replay event ID.</summary>
    public string? EventId { get; }

    /// <summary>Gets the optional atomic replay store.</summary>
    public IReplayStore? ReplayStore { get; }

    /// <inheritdoc />
    public override string ToString() => "WebhookVerification([REDACTED])";
}
