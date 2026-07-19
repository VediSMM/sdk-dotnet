using System;

namespace VediSMM.Helpers;

/// <summary>Creates cryptographically unpredictable UUID v4 idempotency keys.</summary>
public static class Idempotency
{
    /// <summary>Creates a lower-case UUID v4 key.</summary>
    public static string NewKey() => Guid.NewGuid().ToString("D");
}
