using System;

namespace VediSMM.Errors;

/// <summary>Reports an invalid or oversized successful response.</summary>
public sealed class DecodeException : VediSmmException
{
    /// <summary>Initializes the decode failure.</summary>
    public DecodeException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
