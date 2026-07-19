using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;

namespace VediSMM.Helpers;

/// <summary>Finite cooperative publication-job polling.</summary>
public sealed class JobWaiter
{
    private static readonly HashSet<string> FailedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "failed",
        "cancelled",
        "canceled",
    };

    private readonly TimeSpan timeout;
    private readonly TimeSpan pollInterval;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    /// <summary>Creates a finite job waiter.</summary>
    /// <param name="timeout">Total monotonic timeout.</param>
    /// <param name="pollInterval">Delay between representations.</param>
    /// <param name="delayAsync">Optional cooperative delay implementation.</param>
    public JobWaiter(
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        this.timeout = Positive(timeout ?? TimeSpan.FromMinutes(2), nameof(timeout));
        this.pollInterval = Positive(pollInterval ?? TimeSpan.FromSeconds(1), nameof(pollInterval));
        this.delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>Waits for a succeeded job or throws a typed terminal failure.</summary>
    /// <param name="fetchAsync">Current-job callback.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The immutable succeeded job representation.</returns>
    public async Task<JsonElement> WaitAsync(
        Func<CancellationToken, Task<JsonElement>> fetchAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetchAsync);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopwatch.Elapsed >= timeout)
            {
                throw new RequestTimeoutException("VediSMM publication job polling timed out.", null);
            }

            JsonElement job = (await fetchAsync(cancellationToken).ConfigureAwait(false)).Clone();
            string status = job.TryGetProperty("status", out JsonElement statusValue)
                ? statusValue.GetString() ?? string.Empty
                : string.Empty;
            if (status.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return job;
            }

            if (FailedStatuses.Contains(status))
            {
                throw new JobFailedException(job);
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new RequestTimeoutException("VediSMM publication job polling timed out.", null);
            }

            await delayAsync(remaining < pollInterval ? remaining : pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TimeSpan Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, "Value must be positive.");
        }

        return value;
    }
}
