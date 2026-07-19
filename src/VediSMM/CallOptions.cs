using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM;

/// <summary>Options for one canonical API call.</summary>
public sealed class CallOptions
{
    /// <summary>Gets path parameter values before percent encoding.</summary>
    public IReadOnlyDictionary<string, string> Path { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets repeated query parameter values before percent encoding.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Query { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Gets additional non-reserved request headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a JSON-serializable body.</summary>
    public object? JsonBody { get; init; }

    /// <summary>Gets a replayable raw body; the client snapshots this array before sending.</summary>
    public byte[]? RawBody { get; init; }

    /// <summary>Gets a factory that creates a fresh replayable streaming body for every attempt.</summary>
    public Func<CancellationToken, ValueTask<HttpContent>>? ContentFactory { get; init; }

    /// <summary>Gets the request content type for raw or factory content.</summary>
    public string? ContentType { get; init; }

    /// <summary>Gets a stable idempotency key.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Gets the optimistic-concurrency ETag.</summary>
    public string? IfMatch { get; init; }

    /// <summary>Gets an optional per-call timeout.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Gets a caller-owned response stream for unbuffered successful downloads.</summary>
    public Stream? ResponseStream { get; init; }
}
