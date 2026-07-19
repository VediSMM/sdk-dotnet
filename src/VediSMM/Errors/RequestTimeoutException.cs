using System;

namespace VediSMM.Errors;

/// <summary>Reports expiry of the finite SDK request deadline.</summary>
public sealed class RequestTimeoutException : TimeoutException
{
    /// <summary>Initializes the timeout.</summary>
    public RequestTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
