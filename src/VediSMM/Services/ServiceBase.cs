using System;
using System.Threading;
using System.Threading.Tasks;

namespace VediSMM.Services;

/// <summary>Shared delegation boundary for generated domain services.</summary>
public abstract class ServiceBase
{
    private readonly VediSmmClient client;

    private protected ServiceBase(VediSmmClient client)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private protected Task<ApiResponse> CallAsync(
        string operationId,
        CallOptions? options,
        CancellationToken cancellationToken) =>
        client.CallAsync(operationId, options, cancellationToken);
}
