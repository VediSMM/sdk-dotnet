using System;
using System.Net;
using System.Text.Json;

namespace VediSMM.Errors;

/// <summary>Represents an unsuccessful API status and bounded Problem Details metadata.</summary>
public class ApiException : VediSmmException
{
    /// <summary>Initializes the API failure.</summary>
    public ApiException(
        HttpStatusCode statusCode,
        string code,
        string detail,
        JsonElement? validationErrors,
        string? requestId,
        TimeSpan? retryAfter)
        : base($"VediSMM API error {(int)statusCode} ({code}): {detail}")
    {
        StatusCode = statusCode;
        Code = code;
        Detail = detail;
        ValidationErrors = validationErrors?.Clone();
        RequestId = requestId;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the stable API code.</summary>
    public string Code { get; }

    /// <summary>Gets the bounded redacted detail.</summary>
    public string Detail { get; }

    /// <summary>Gets field-level validation errors, if present.</summary>
    public JsonElement? ValidationErrors { get; }

    /// <summary>Gets the server request ID.</summary>
    public string? RequestId { get; }

    /// <summary>Gets the parsed Retry-After delay.</summary>
    public TimeSpan? RetryAfter { get; }
}
