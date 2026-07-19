using System;
using System.Net;
using System.Text.Json;

namespace VediSMM.Errors;

/// <summary>Represents HTTP 412 optimistic-concurrency failure.</summary>
public sealed class PreconditionFailedException : ApiException
{
    /// <summary>Initializes the precondition failure.</summary>
    public PreconditionFailedException(string code, string detail, JsonElement? errors, string? requestId, TimeSpan? retryAfter)
        : base(HttpStatusCode.PreconditionFailed, code, detail, errors, requestId, retryAfter)
    {
    }
}
