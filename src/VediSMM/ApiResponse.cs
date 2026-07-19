using System.Collections.Generic;
using System.Net;
using System.Text.Json;

namespace VediSMM;

/// <summary>One successful immutable API response with transport metadata.</summary>
public sealed record ApiResponse(
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    JsonElement? Data,
    string? RequestId,
    string? ETag);
