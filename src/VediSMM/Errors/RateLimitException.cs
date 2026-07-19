using System;
using System.Net;
using System.Text.Json;

namespace VediSMM.Errors;

/// <summary>Represents HTTP 429.</summary>
public sealed class RateLimitException : ApiException
{
    /// <summary>Initializes the rate-limit failure.</summary>
    public RateLimitException(string code, string detail, JsonElement? errors, string? requestId, TimeSpan? retryAfter)
        : base(HttpStatusCode.TooManyRequests, code, detail, errors, requestId, retryAfter)
    {
    }
}
