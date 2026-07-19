using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;

namespace VediSMM.Webhooks;

/// <summary>Constant-time HMAC-SHA256 raw-byte webhook verifier.</summary>
public static class WebhookVerifier
{
    private static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Verifies signature, timestamp tolerance, and optional replay claim.</summary>
    public static async ValueTask<bool> VerifyAsync(
        WebhookVerification verification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verification);
        if (string.IsNullOrWhiteSpace(verification.Secret))
        {
            throw new ConfigurationException("Webhook secret must not be blank.");
        }

        TimeSpan tolerance = verification.Tolerance ?? DefaultTolerance;
        if (tolerance < TimeSpan.Zero)
        {
            throw new ConfigurationException("Webhook tolerance must not be negative.");
        }

        if (!long.TryParse(verification.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
        {
            return false;
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        DateTimeOffset now = verification.Now ?? DateTimeOffset.UtcNow;
        TimeSpan age = now >= timestamp ? now - timestamp : timestamp - now;
        if (age > tolerance || !TrySignature(verification.Signature, out byte[] provided))
        {
            return false;
        }

        byte[] timestampBytes = Encoding.ASCII.GetBytes(verification.Timestamp);
        byte[] body = verification.Body;
        byte[] payload = new byte[timestampBytes.Length + 1 + body.Length];
        Buffer.BlockCopy(timestampBytes, 0, payload, 0, timestampBytes.Length);
        payload[timestampBytes.Length] = (byte)'.';
        Buffer.BlockCopy(body, 0, payload, timestampBytes.Length + 1, body.Length);
        byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(verification.Secret), payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            return false;
        }

        bool replayRequested = verification.EventId is not null || verification.ReplayStore is not null;
        if (!replayRequested)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(verification.EventId) || verification.ReplayStore is null)
        {
            throw new ConfigurationException("EventId and ReplayStore must be provided together.");
        }

        return await verification.ReplayStore.ClaimAsync(
            verification.EventId,
            now + tolerance,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool TrySignature(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (value is null || value.Length != 67 || !value.StartsWith("v1=", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(value.AsSpan(3));
            return bytes.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
