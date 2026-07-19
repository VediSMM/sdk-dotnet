using System;

namespace VediSMM.Errors;

/// <summary>Base exception for deterministic SDK failures.</summary>
public abstract class VediSmmException : Exception
{
    /// <summary>Initializes a failure without credential-shaped context.</summary>
    protected VediSmmException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a failure whose safe cause is useful to callers.</summary>
    protected VediSmmException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
