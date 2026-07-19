namespace VediSMM.Errors;

/// <summary>Reports an invalid cursor sequence.</summary>
public sealed class PaginationException : VediSmmException
{
    /// <summary>Initializes the pagination failure.</summary>
    public PaginationException(string message)
        : base(message)
    {
    }
}
