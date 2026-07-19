using System;

namespace VediSMM.Errors;

/// <summary>Reports an HTTP transport failure.</summary>
public class TransportException : VediSmmException
{
    /// <summary>Initializes a redacted transport failure.</summary>
    public TransportException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a transport failure with a safe cause.</summary>
    public TransportException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
