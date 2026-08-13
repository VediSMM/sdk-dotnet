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
using VediSMM.Helpers;
using VediSMM.Services;
using Xunit;

namespace VediSMM.Tests;

public sealed partial class ContractTests
{
    private static readonly IReadOnlyList<string> ExistingCallOptionProperties = Array.AsReadOnly(new[]
    {
        "Path", "Query", "Headers", "JsonBody", "RawBody", "ContentFactory", "ContentType",
        "IdempotencyKey", "IfMatch", "Timeout", "ResponseStream",
    });

    [Fact]
    public void ExistingApiResponseAndCallOptionsPublicShapesRemainCompatible()
    {
        ConstructorInfo constructor = Assert.Single(typeof(ApiResponse).GetConstructors());
        Assert.Equal(5, constructor.GetParameters().Length);
        MethodInfo deconstruct = Assert.Single(typeof(ApiResponse).GetMethods(), method =>
            method.Name == "Deconstruct" && method.GetParameters().Length == 5);
        Assert.Equal(5, deconstruct.GetParameters().Length);

        Assert.NotNull(typeof(CallOptions).GetConstructor(Type.EmptyTypes));
        Assert.All(ExistingCallOptionProperties, name => Assert.NotNull(typeof(CallOptions).GetProperty(name)));
        Type callback = typeof(Func<string?, CancellationToken, Task<Page<int>>>);
        Assert.NotNull(typeof(CursorPager<int>).GetConstructor([callback]));
    }

    [Fact]
    public async Task InternalPagingMetadataDoesNotChangeApiResponseValueSemantics()
    {
        using var client = new VediSmmClient(
            new VediSmmClientOptions(),
            new SuccessHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"id\":7},\"meta\":{\"next_cursor\":null}}"),
            })));

        ApiResponse actual = await client.CallAsync(
            "ping",
            cancellationToken: TestContext.Current.CancellationToken);
        var expected = new ApiResponse(
            actual.StatusCode,
            actual.Headers,
            actual.Data,
            actual.RequestId,
            actual.ETag);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain("Envelope", actual.ToString(), StringComparison.Ordinal);
    }

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

        Assert.Equal(94, ids.Length);
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
        Assert.Equal(19, VediSmm.ServiceTypes.Count);
    }

    [Fact]
    public void ManifestCarriesExactCanonicalTrackingCapabilities()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "contract", "sdk-operations.json")));

        Assert.Equal("1.1.0", manifest.RootElement.GetProperty("contract_version").GetString());
        Assert.Equal(
            "08e7b140c93a1ef16aa54de509799388e8b01da36b93e47e9733a36e57e40f8a",
            manifest.RootElement.GetProperty("contract_sha256").GetString());

        Dictionary<string, JsonElement> operations = manifest.RootElement.GetProperty("operations")
            .EnumerateArray()
            .ToDictionary(operation => operation.GetProperty("operation_id").GetString()!, StringComparer.Ordinal);
        Assert.Equal(94, operations.Count);
        Assert.Equal(11, operations.Values.Count(operation =>
            operation.GetProperty("tag").GetString()!.StartsWith("Tracking ", StringComparison.Ordinal)));
        Assert.Equal(
            ["authenticated", "etag_response", "idempotency_key", "service_unavailable"],
            Values(operations["createTrackingLink"], "capabilities"));
        Assert.Equal(
            ["authenticated", "etag_response", "optimistic_concurrency", "service_unavailable"],
            Values(operations["disableTrackingLink"], "capabilities"));
        Assert.Equal(
            ["authenticated", "cursor_pagination", "service_unavailable"],
            Values(operations["listTrackingAnalyticsLinks"], "capabilities"));
    }

    [Fact]
    public void FocusedTrackingSurfaceOwnsFiveLifecycleAndSixAnalyticsOperationsWithoutUpdate()
    {
        Type links = VediSmm.ServiceTypes["TrackingLinks"];
        Type analytics = VediSmm.ServiceTypes["TrackingAnalytics"];
        MethodInfo[] linkOperations = links.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<ApiOperationAttribute>() is not null)
            .ToArray();
        MethodInfo[] analyticsOperations = analytics.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<ApiOperationAttribute>() is not null)
            .ToArray();

        Assert.Equal(5, linkOperations.Length);
        Assert.Equal(6, analyticsOperations.Length);
        Assert.DoesNotContain(links.GetMethods(BindingFlags.Instance | BindingFlags.Public), method =>
            method.Name.Contains("Update", StringComparison.Ordinal));
        Assert.All(linkOperations.Concat(analyticsOperations), method => Assert.Contains(
            method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken)));
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
                object?[] arguments = method.GetParameters().Select(parameter =>
                {
                    if (parameter.ParameterType == typeof(CallOptions))
                    {
                        return (object?)new CallOptions
                        {
                            Path = path,
                            IdempotencyKey = "contract-operation-1",
                            IfMatch = "\"contract\"",
                        };
                    }

                    if (parameter.ParameterType == typeof(CancellationToken))
                    {
                        return TestContext.Current.CancellationToken;
                    }

                    if (parameter.ParameterType == typeof(long))
                    {
                        return 1L;
                    }

                    return new Dictionary<string, object?>();
                }).ToArray();
                Task<ApiResponse> pending = Assert.IsType<Task<ApiResponse>>(method.Invoke(service, arguments));
                await pending;
            }
        }

        Assert.Equal(94, calls);
    }

    [Fact]
    public void PostTrackingSettingsUseCanonicalExplicitJsonNames()
    {
        var body = new Dictionary<string, object?>
        {
            ["options"] = new PostTrackingRequestOptions
            {
                Tracking = new TrackingSettings(),
            },
            ["nullable_extension"] = null,
        };

        string json = JsonSerializer.Serialize(body);

        Assert.Equal(
            "{\"options\":{\"tracking\":{\"shorten_links\":false,\"add_source\":false}},\"nullable_extension\":null}",
            json);
    }

    private static string[] Values(JsonElement operation, string property) =>
        operation.GetProperty(property).EnumerateArray().Select(value => value.GetString()!).ToArray();

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
