using System.Text.Json;

namespace VediSMM.Errors;

/// <summary>Reports a terminal failed or cancelled publication job.</summary>
public sealed class JobFailedException : VediSmmException
{
    /// <summary>Initializes the job failure.</summary>
    public JobFailedException(JsonElement job)
        : base($"VediSMM publication job {Text(job, "id", "unknown")} ended with status {Text(job, "status", "unknown")}.")
    {
        Job = job.Clone();
    }

    /// <summary>Gets the terminal immutable job representation.</summary>
    public JsonElement Job { get; }

    private static string Text(JsonElement job, string name, string fallback)
    {
        if (!job.TryGetProperty(name, out JsonElement value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }
}
