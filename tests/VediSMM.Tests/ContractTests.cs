using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Services;
using Xunit;

namespace VediSMM.Tests;

public sealed partial class ContractTests
{
    [Fact]
    public void CatalogAndServicesOwnExactlyAllCanonicalUserOperations()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "contract", "sdk-operations.json")));
        string[] ids = manifest.RootElement.GetProperty("operations")
            .EnumerateArray()
            .Select(operation => operation.GetProperty("operation_id").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(83, ids.Length);
        Assert.Equal(ids, OperationCatalog.All.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(ids, id => id.Contains("admin", StringComparison.OrdinalIgnoreCase));

        var owned = typeof(VediSmm)
            .Assembly.GetTypes()
            .Where(type => type.Namespace == "VediSMM.Services")
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => method.GetCustomAttribute<ApiOperationAttribute>()?.OperationId)
            .Where(id => id is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ids, owned);
        Assert.Equal(17, VediSmm.ServiceTypes.Count);
    }

    [Fact]
    public async Task EveryGeneratedServiceMethodExecutesItsCanonicalOperation()
    {
        int calls = 0;
        using var client = new VediSmmClient(
            new VediSmmClientOptions { AccessToken = "memory-token" },
            new SuccessHandler((_, _) =>
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":{}}"),
                });
            }));
        using var sdk = new VediSmm(client);

        foreach ((string propertyName, Type serviceType) in VediSmm.ServiceTypes)
        {
            object service = typeof(VediSmm).GetProperty(propertyName)!.GetValue(sdk)!;
            foreach (MethodInfo method in serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                         .Where(candidate => candidate.GetCustomAttribute<ApiOperationAttribute>() is not null))
            {
                string operationId = method.GetCustomAttribute<ApiOperationAttribute>()!.OperationId;
                ApiOperation operation = OperationCatalog.All[operationId];
                var path = PathParameter().Matches(operation.Path)
                    .Select(match => match.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(name => name, _ => "fixture", StringComparer.Ordinal);
                var options = new CallOptions { Path = path };
                Task<ApiResponse> pending = Assert.IsType<Task<ApiResponse>>(method.Invoke(
                    service,
                    new object?[] { options, TestContext.Current.CancellationToken }));
                await pending;
            }
        }

        Assert.Equal(83, calls);
    }

    [GeneratedRegex("\\{([^}]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PathParameter();

    private sealed class SuccessHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
