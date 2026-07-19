using System;
using System.Net;

namespace VediSMM.Errors;

/// <summary>Reports a rejected redirect so credentials never cross origins.</summary>
public sealed class RedirectException : VediSmmException
{
    /// <summary>Initializes the redirect failure.</summary>
    public RedirectException(HttpStatusCode statusCode, Uri? location)
        : base($"VediSMM rejected redirect {(int)statusCode} to {(location is null ? "an undisclosed location" : location.GetLeftPart(UriPartial.Authority))}.")
    {
        StatusCode = statusCode;
        Location = location;
    }

    /// <summary>Gets the redirect status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the redirect location, if supplied.</summary>
    public Uri? Location { get; }
}
