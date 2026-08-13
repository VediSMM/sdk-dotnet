using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VediSMM;

/// <summary>One successful immutable API response with transport metadata.</summary>
public sealed record ApiResponse(
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    JsonElement? Data,
    string? RequestId,
    string? ETag);

internal static class ApiResponseEnvelopes
{
    private static readonly ConditionalWeakTable<ApiResponse, EnvelopeHolder> Envelopes = new();

    internal static ApiResponse Attach(ApiResponse response, JsonElement envelope)
    {
        Envelopes.Add(response, new EnvelopeHolder(envelope));
        return response;
    }

    internal static bool TryGet(ApiResponse response, out JsonElement envelope)
    {
        if (Envelopes.TryGetValue(response, out EnvelopeHolder? holder))
        {
            envelope = holder.Value;
            return true;
        }

        envelope = default;
        return false;
    }

    private sealed record EnvelopeHolder(JsonElement Value);
}
