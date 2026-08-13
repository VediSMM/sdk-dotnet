using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;
using VediSMM.Helpers;

namespace VediSMM.Services;

/// <summary>Shared delegation boundary for generated domain services.</summary>
public abstract class ServiceBase
{
    private static readonly HashSet<string> FocusedQueryNames = new(StringComparer.Ordinal)
    {
        "from",
        "to",
        "link_id",
        "post_id",
        "network",
        "cursor",
        "limit",
    };
    private readonly VediSmmClient client;

    private protected ServiceBase(VediSmmClient client)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private protected Task<ApiResponse> CallAsync(
        string operationId,
        CallOptions? options,
        CancellationToken cancellationToken) =>
        client.CallAsync(operationId, options, cancellationToken);

    private protected static CallOptions FocusedOptions(CallOptions? options)
    {
        CallOptions effective = options ?? new CallOptions();
        string? reserved = effective.Query.Keys.FirstOrDefault(FocusedQueryNames.Contains);
        if (reserved is not null)
        {
            throw new ConfigurationException($"Focused tracking service owns query parameter: {reserved}.");
        }

        return effective;
    }

    private protected static CallOptions RequireIdempotencyKey(CallOptions options)
    {
        string? value = options.IdempotencyKey;
        if (value is null || value.Length is < 8 or > 200
            || value.Any(character => character is < (char)33 or > (char)126))
        {
            throw new ConfigurationException(
                "Tracking link creation requires an 8-200 character Idempotency-Key.");
        }

        return options;
    }

    private protected static CallOptions RequireIfMatch(CallOptions options)
    {
        string? value = options.IfMatch;
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => character is < (char)33 or (char)127))
        {
            throw new ConfigurationException(
                "Tracking link mutation requires a non-blank single-line If-Match value.");
        }

        return options;
    }

    private protected static Dictionary<string, object?> WithoutCursor(
        IReadOnlyDictionary<string, object?> values) =>
        values.Where(pair => !pair.Key.Equals("cursor", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private protected static string? Cursor(IReadOnlyDictionary<string, object?> values) =>
        values.TryGetValue("cursor", out object? cursor)
            ? Convert.ToString(cursor, CultureInfo.InvariantCulture)
            : null;

    private protected static IReadOnlyDictionary<string, object?> WithCursor(
        IReadOnlyDictionary<string, object?> values,
        string? cursor)
    {
        var result = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (cursor is not null)
        {
            result["cursor"] = cursor;
        }

        return result;
    }

    private protected static Page<JsonElement> Page(ApiResponse response)
    {
        if (!ApiResponseEnvelopes.TryGet(response, out JsonElement body)
            || body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.Array
            || !body.TryGetProperty("meta", out JsonElement meta)
            || meta.ValueKind != JsonValueKind.Object)
        {
            throw new DecodeException("VediSMM returned an invalid tracking cursor page.");
        }

        JsonElement[] items = data.EnumerateArray().Select(item => item.Clone()).ToArray();
        string? nextCursor = meta.TryGetProperty("next_cursor", out JsonElement next)
            && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        return new Page<JsonElement>(Array.AsReadOnly(items), nextCursor);
    }
}
