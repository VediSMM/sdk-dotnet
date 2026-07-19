namespace VediSMM.Errors;

/// <summary>Reports unsafe or inconsistent client/call configuration.</summary>
public sealed class ConfigurationException : VediSmmException
{
    /// <summary>Initializes the failure.</summary>
    public ConfigurationException(string message)
        : base(message)
    {
    }
}
