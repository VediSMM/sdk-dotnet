using System.Collections.Generic;

namespace VediSMM.Helpers;

/// <summary>One immutable cursor page.</summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
