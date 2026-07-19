using System;

namespace VediSMM.Services;

/// <summary>Associates one idiomatic service method with its canonical operation ID.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiOperationAttribute(string operationId) : Attribute
{
    /// <summary>Gets the canonical operation ID.</summary>
    public string OperationId { get; } = operationId ?? throw new ArgumentNullException(nameof(operationId));
}
