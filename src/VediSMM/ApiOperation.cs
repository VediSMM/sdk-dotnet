using System.Collections.Generic;

namespace VediSMM;

/// <summary>Immutable metadata for one canonical public API operation.</summary>
public sealed record ApiOperation(
    string OperationId,
    string Method,
    string Path,
    bool Authenticated,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> RequestContentTypes,
    IReadOnlyList<string> Capabilities);
