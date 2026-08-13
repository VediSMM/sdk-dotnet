using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM;

/// <summary>Options for one canonical API call.</summary>
public sealed class CallOptions
{
    private IReadOnlyDictionary<string, string> path =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
    private IReadOnlyDictionary<string, IReadOnlyList<string>> query =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
    private IReadOnlyDictionary<string, IReadOnlyList<string>> headers =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
    private byte[]? rawBody;

    /// <summary>Gets path parameter values before percent encoding.</summary>
    public IReadOnlyDictionary<string, string> Path
    {
        get => path;
        init => path = CopyPath(value);
    }

    /// <summary>Gets repeated query parameter values before percent encoding.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Query
    {
        get => query;
        init => query = CopyLists(value, StringComparer.Ordinal);
    }

    /// <summary>Gets additional non-reserved request headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers
    {
        get => headers;
        init => headers = CopyLists(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets a JSON-serializable body.</summary>
    public object? JsonBody { get; init; }

    /// <summary>Gets a replayable raw body; the client snapshots this array before sending.</summary>
    public byte[]? RawBody
    {
        get => rawBody?.ToArray();
        init => rawBody = value?.ToArray();
    }

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

    /// <summary>Creates immutable options carrying a stable idempotency key.</summary>
    public static CallOptions WithIdempotencyKey(string value) => new() { IdempotencyKey = value };

    /// <summary>Creates immutable options carrying a conditional request ETag.</summary>
    public static CallOptions WithIfMatch(string value) => new() { IfMatch = value };

    /// <summary>Returns an immutable copy with one path value.</summary>
    public CallOptions WithPath(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        var values = Path.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values[name] = Convert.ToString(value, CultureInfo.InvariantCulture)!;
        return Copy(path: values);
    }

    /// <summary>Returns an immutable copy with typed query values replacing matching names.</summary>
    public CallOptions WithQuery(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = Query.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.Ordinal);
        foreach ((string name, object? value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (value is null)
            {
                continue;
            }

            string[] encoded = value switch
            {
                string text => [text],
                IEnumerable<string> items => items.ToArray(),
                _ => [Convert.ToString(value, CultureInfo.InvariantCulture)!],
            };
            result[name] = Array.AsReadOnly(encoded);
        }

        return Copy(query: result);
    }

    /// <summary>Returns an immutable copy with one non-reserved request header.</summary>
    public CallOptions WithHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        var values = Headers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);
        values[name] = Array.AsReadOnly([value]);
        return Copy(headers: values);
    }

    /// <summary>Returns an immutable copy with a JSON body and no conflicting raw body source.</summary>
    public CallOptions WithJsonBody(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Copy(jsonBody: value, replaceBody: true);
    }

    private CallOptions Copy(
        IReadOnlyDictionary<string, string>? path = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? query = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
        object? jsonBody = null,
        bool replaceBody = false) =>
        new()
        {
            Path = path ?? Path,
            Query = query ?? Query,
            Headers = headers ?? Headers,
            JsonBody = replaceBody ? jsonBody : JsonBody,
            RawBody = replaceBody ? null : RawBody,
            ContentFactory = replaceBody ? null : ContentFactory,
            ContentType = replaceBody ? null : ContentType,
            IdempotencyKey = IdempotencyKey,
            IfMatch = IfMatch,
            Timeout = Timeout,
            ResponseStream = ResponseStream,
        };

    private static ReadOnlyDictionary<string, string> CopyPath(IReadOnlyDictionary<string, string>? source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReadOnlyDictionary<string, string>(
            source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static ReadOnlyDictionary<string, IReadOnlyList<string>> CopyLists(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? source,
        IEqualityComparer<string> comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(source.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            comparer));
    }
}
