using System;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM;

/// <summary>Secure configuration for one reusable client.</summary>
public sealed class VediSmmClientOptions
{
    /// <summary>Gets or sets the API v1 base address.</summary>
    public Uri BaseAddress { get; init; } = new("https://vedismm.ru/api/v1/", UriKind.Absolute);

    /// <summary>Gets or sets a fixed memory-only Bearer token.</summary>
    public string? AccessToken { get; init; }

    /// <summary>Gets or sets an async memory-only rotated-token provider.</summary>
    public Func<CancellationToken, ValueTask<string?>>? TokenProvider { get; init; }

    /// <summary>Gets or sets the finite default request timeout.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum buffered successful response size.</summary>
    public int MaximumResponseBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>Gets or sets the maximum error body size retained for diagnostics.</summary>
    public int MaximumErrorBytes { get; init; } = 64 * 1024;

    /// <summary>Gets or sets the bounded total number of eligible attempts.</summary>
    public int MaximumAttempts { get; init; } = 3;

    /// <summary>Gets or sets the upper bound for one retry delay.</summary>
    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; init; } = Task.Delay;
}
