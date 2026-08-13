using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;
using Xunit;

namespace VediSMM.Tests;

public sealed class TransportTests
{
    private static readonly IReadOnlyList<string> SearchValues = Array.AsReadOnly(new[] { "x y", "два" });
    private static readonly IReadOnlyList<string> AuthorizationValues = Array.AsReadOnly(new[] { "override" });
    private static readonly IReadOnlyList<string> UnsafeHeaderValues = Array.AsReadOnly(new[] { "safe\r\ninjected: true" });
    private static readonly IReadOnlyList<string> InjectedFilterValues = Array.AsReadOnly(new[] { "injected" });
    private static readonly IReadOnlyList<int> PagerIds = Array.AsReadOnly(new[] { 1, 2 });

    [Fact]
    public async Task TrackingServicesSendCanonicalRequestsAndPreserveCallOptions()
    {
        var requests = new List<RequestSnapshot>();
        using var client = Client(async (message, cancellationToken) =>
        {
            requests.Add(await RequestSnapshot.CreateAsync(message, cancellationToken));
            return Json(HttpStatusCode.OK, "{\"data\":{}}");
        }, new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);

        CallOptions createOptions = CallOptions.WithIdempotencyKey("create-1")
            .WithQuery(new Dictionary<string, object?> { ["extension"] = "kept" })
            .WithHeader("X-Trace", "trace-1");
        await sdk.TrackingLinks.CreateAsync(
            new Dictionary<string, object?> { ["destination_url"] = "https://example.com/a?x=1#fragment" },
            createOptions,
            TestContext.Current.CancellationToken);
        await sdk.TrackingLinks.DisableAsync(
            42,
            CallOptions.WithIfMatch("\"3\""),
            TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.GeoAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, requests.Count);
        Assert.Equal("POST", requests[0].Method);
        Assert.Equal("/api/v1/tracking-links?extension=kept", requests[0].PathAndQuery);
        Assert.Equal("{\"destination_url\":\"https://example.com/a?x=1#fragment\"}", requests[0].Body);
        Assert.Equal("create-1", requests[0].IdempotencyKey);
        Assert.Equal("trace-1", requests[0].Trace);
        Assert.Equal("POST", requests[1].Method);
        Assert.Equal("/api/v1/tracking-links/42/disable", requests[1].PathAndQuery);
        Assert.Equal("\"3\"", requests[1].IfMatch);
        Assert.Equal("GET", requests[2].Method);
        Assert.Equal(
            "/api/v1/tracking-analytics/geo?from=2026-08-01&to=2026-08-13",
            requests[2].PathAndQuery);
    }

    [Fact]
    public async Task TrackingServicesExposeEveryLifecycleAnalyticsAndPagerOperation()
    {
        var requests = new List<RequestSnapshot>();
        int linkPages = 0;
        using var client = Client(async (message, cancellationToken) =>
        {
            requests.Add(await RequestSnapshot.CreateAsync(message, cancellationToken));
            if (message.RequestUri!.AbsolutePath.EndsWith("/tracking-links", StringComparison.Ordinal)
                && message.Method == HttpMethod.Get)
            {
                linkPages++;
                string cursor = linkPages == 1 ? "\"next+/=\"" : "null";
                int id = linkPages;
                return Json(HttpStatusCode.OK,
                    $"{{\"data\":[{{\"id\":{id}}}],\"meta\":{{\"next_cursor\":{cursor},\"has_more\":{(linkPages == 1 ? "true" : "false")},\"limit\":1}}}}");
            }

            return Json(HttpStatusCode.OK, "{\"data\":[],\"meta\":{\"next_cursor\":null,\"has_more\":false,\"limit\":1}}");
        }, new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);
        var filter = new Dictionary<string, object?>
        {
            ["from"] = "2026-08-01",
            ["to"] = "2026-08-13",
            ["link_id"] = null,
            ["post_id"] = 9,
            ["network"] = null,
        };

        await sdk.TrackingLinks.ListAsync(new Dictionary<string, object?> { ["limit"] = 1 }, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingLinks.GetAsync(42, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingLinks.ArchiveAsync(42, CallOptions.WithIfMatch("\"4\""), TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.SummaryAsync(filter, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.TimeseriesAsync(filter, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.LinksAsync(filter, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.PostsAsync(filter, cancellationToken: TestContext.Current.CancellationToken);
        await sdk.TrackingAnalytics.SourcesAsync(filter, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(sdk.TrackingAnalytics.IterateLinks(filter));
        Assert.NotNull(sdk.TrackingAnalytics.IteratePosts(filter));
        Assert.NotNull(sdk.TrackingAnalytics.IterateSources(filter));

        linkPages = 0;
        var ids = new List<int>();
        await foreach (JsonElement item in sdk.TrackingLinks.Iterate(
            new Dictionary<string, object?> { ["cursor"] = "initial", ["limit"] = 1 })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            ids.Add(item.GetProperty("id").GetInt32());
        }

        Assert.Equal(PagerIds, ids);
        Assert.Contains(requests, request => request.PathAndQuery.Contains("cursor=initial", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.PathAndQuery.Contains("cursor=next%2B%2F%3D", StringComparison.Ordinal));
        await foreach (JsonElement _ in sdk.TrackingAnalytics.IterateLinks(filter)
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            Assert.Fail("The analytics fixture page is expected to be empty.");
        }
        Assert.Contains(requests, request => request.PathAndQuery.StartsWith(
            "/api/v1/tracking-analytics/links?", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.PathAndQuery ==
            "/api/v1/tracking-analytics/summary?from=2026-08-01&to=2026-08-13&post_id=9");
    }

    [Fact]
    public async Task TrackingRequiredHeadersAndReservedFiltersFailBeforeHttpIo()
    {
        int calls = 0;
        int providerCalls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        }, new VediSmmClientOptions
        {
            TokenProvider = _ =>
            {
                providerCalls++;
                return ValueTask.FromResult<string?>("tracking-token");
            },
        });
        using var sdk = new VediSmm(client);

        await Assert.ThrowsAsync<ConfigurationException>(() => sdk.TrackingLinks.CreateAsync(
            new Dictionary<string, object?> { ["destination_url"] = "https://example.com" },
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ConfigurationException>(() => sdk.TrackingLinks.CreateAsync(
            new Dictionary<string, object?> { ["destination_url"] = "https://example.com" },
            CallOptions.WithIdempotencyKey("short"),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ConfigurationException>(() => sdk.TrackingLinks.DisableAsync(
            42,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ConfigurationException>(() => sdk.TrackingAnalytics.GeoAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            new CallOptions
            {
                Query = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["from"] = InjectedFilterValues,
                },
            },
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ConfigurationException>(() => sdk.TrackingLinks.ListAsync(
            new Dictionary<string, object?>(),
            new CallOptions
            {
                Query = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["limit"] = InjectedFilterValues,
                },
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(0, calls);
        Assert.Equal(0, providerCalls);
    }

    [Fact]
    public void CallOptionsCompositionIsImmutableAndPreservesUnrelatedMetadata()
    {
        var mutablePath = new Dictionary<string, string> { ["existing"] = "one" };
        var mutableQueryValues = new List<string> { "value" };
        var mutableQuery = new Dictionary<string, IReadOnlyList<string>> { ["extension"] = mutableQueryValues };
        var mutableHeaders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Trace"] = mutableQueryValues,
        };
        byte[] mutableRaw = [1, 2, 3];
        using var sink = new MemoryStream();
        var original = new CallOptions
        {
            Path = mutablePath,
            Query = mutableQuery,
            Headers = mutableHeaders,
            RawBody = mutableRaw,
            ContentType = "application/octet-stream",
            IdempotencyKey = "immutable-key",
            IfMatch = "\"v1\"",
            Timeout = TimeSpan.FromSeconds(12),
            ResponseStream = sink,
        };

        mutablePath["existing"] = "changed";
        mutableQueryValues[0] = "changed";
        mutableRaw[0] = 9;
        byte[] exposedRaw = original.RawBody!;
        exposedRaw[1] = 9;

        CallOptions composed = original
            .WithPath("id", 42)
            .WithQuery(new Dictionary<string, object?> { ["nullable"] = null, ["limit"] = 5 })
            .WithHeader("X-Request-Extension", "kept")
            .WithJsonBody(new Dictionary<string, object?> { ["nullable"] = null });

        Assert.Equal("one", original.Path["existing"]);
        Assert.Equal("value", original.Query["extension"][0]);
        Assert.Equal(1, original.RawBody![0]);
        Assert.Equal(2, original.RawBody![1]);
        Assert.False(original.Path.ContainsKey("id"));
        Assert.Equal("42", composed.Path["id"]);
        Assert.Equal("5", composed.Query["limit"][0]);
        Assert.False(composed.Query.ContainsKey("nullable"));
        Assert.Equal("value", composed.Headers["X-Trace"][0]);
        Assert.Equal("kept", composed.Headers["X-Request-Extension"][0]);
        Assert.Equal("immutable-key", composed.IdempotencyKey);
        Assert.Equal("\"v1\"", composed.IfMatch);
        Assert.Equal(TimeSpan.FromSeconds(12), composed.Timeout);
        Assert.Same(sink, composed.ResponseStream);
        Assert.Null(composed.RawBody);
        Assert.Null(composed.ContentFactory);
        Assert.Null(composed.ContentType);
        Assert.NotNull(composed.JsonBody);
    }

    [Fact]
    public async Task TrackingCancellationAndGeoJsonRemainTransportNative()
    {
        using var delayed = Client(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json(HttpStatusCode.OK, "{}");
        }, new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var delayedSdk = new VediSmm(delayed);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delayedSdk.TrackingAnalytics.GeoAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            cancellationToken: cancellation.Token));

        const string geoJson = "{\"data\":{\"countries\":[{\"country_code\":\"RU\",\"human_clicks\":3}],\"cities\":{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[37.6,55.8]},\"properties\":{\"country_code\":\"RU\",\"region_name\":\"Moscow\",\"city_name\":\"Moscow\",\"human_clicks\":3}}]}},\"meta\":{\"filter\":{\"link_id\":null}}}";
        using var client = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, geoJson)),
            new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);
        ApiResponse response = await sdk.TrackingAnalytics.GeoAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            cancellationToken: TestContext.Current.CancellationToken);

        JsonElement cities = response.Data!.Value.GetProperty("cities");
        Assert.Equal("FeatureCollection", cities.GetProperty("type").GetString());
        Assert.Equal(37.6, cities.GetProperty("features")[0].GetProperty("geometry").GetProperty("coordinates")[0].GetDouble());
        Assert.Equal(JsonValueKind.Null,
            Envelope(response).GetProperty("meta").GetProperty("filter").GetProperty("link_id").ValueKind);
    }

    [Fact]
    public async Task TrackingPreCancellationAndPagerCancellationPerformNoHttpIo()
    {
        int calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.OK,
                "{\"data\":[],\"meta\":{\"next_cursor\":null,\"has_more\":false,\"limit\":1}}"));
        }, new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var filter = new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sdk.TrackingAnalytics.GeoAsync(
            filter,
            cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (JsonElement _ in sdk.TrackingAnalytics.IterateLinks(filter)
                .WithCancellation(cancellation.Token))
            {
            }
        });

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AnalyticsPreservesNullableFieldsAndOpenFutureNetworkValues()
    {
        const string body = "{\"data\":[{\"post_id\":1,\"post_target_id\":null,\"title\":null,\"network\":\"future_network\",\"human_clicks\":1,\"unique_visitors\":1,\"unknown_bot_clicks\":0,\"known_bot_clicks\":0}],\"meta\":{\"filter\":{\"from\":\"2026-08-01\",\"to\":\"2026-08-13\",\"link_id\":null,\"post_id\":null,\"network\":null},\"next_cursor\":null,\"has_more\":false,\"limit\":20}}";
        using var client = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)),
            new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);

        ApiResponse response = await sdk.TrackingAnalytics.PostsAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            cancellationToken: TestContext.Current.CancellationToken);

        JsonElement row = response.Data!.Value[0];
        Assert.Equal("future_network", row.GetProperty("network").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("post_target_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("title").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            Envelope(response).GetProperty("meta").GetProperty("filter").GetProperty("network").ValueKind);
    }

    [Fact]
    public async Task ReservedTrackingFiltersUseExactCanonicalCaseOnly()
    {
        RequestSnapshot? recorded = null;
        using var client = Client(async (message, cancellationToken) =>
        {
            recorded = await RequestSnapshot.CreateAsync(message, cancellationToken);
            return Json(HttpStatusCode.OK, "{\"data\":{}}");
        }, new VediSmmClientOptions { AccessToken = "tracking-token" });
        using var sdk = new VediSmm(client);

        await sdk.TrackingAnalytics.GeoAsync(
            new Dictionary<string, object?> { ["from"] = "2026-08-01", ["to"] = "2026-08-13" },
            new CallOptions
            {
                Query = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["From"] = InjectedFilterValues,
                },
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(recorded);
        Assert.Contains("From=injected", recorded.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("from=2026-08-01", recorded.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EncodesRequestAndReturnsTransportMetadata()
    {
        RequestSnapshot? request = null;
        using var client = Client(async (message, cancellationToken) =>
        {
            request = await RequestSnapshot.CreateAsync(message, cancellationToken);
            var response = Json(HttpStatusCode.OK, "{\"data\":{\"id\":7}}", "req-7");
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v7\"");
            return response;
        }, new VediSmmClientOptions { AccessToken = "memory-token" });

        ApiResponse response = await client.CallAsync("getAccount", new CallOptions
        {
            Path = new Dictionary<string, string> { ["id"] = "a/b" },
            Query = new Dictionary<string, IReadOnlyList<string>> { ["search"] = SearchValues },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(request);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("memory-token", request.AuthorizationParameter);
        Assert.Equal("memory-token", request.FallbackToken);
        Assert.Contains("/accounts/a%2Fb", request.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("search=x%20y", request.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("search=%D0%B4%D0%B2%D0%B0", request.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("req-7", response.RequestId);
        Assert.Equal("\"v7\"", response.ETag);
        Assert.Equal(7, response.Data?.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task RejectsRedirects()
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://attacker.example/steal") },
        }));

        await Assert.ThrowsAsync<RedirectException>(() => client.CallAsync(
            "ping", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RetriesOnlyReplayableIdempotentCommandsWithStableIdentity()
    {
        var requests = new List<RequestSnapshot>();
        using var client = Client(async (message, cancellationToken) =>
        {
            requests.Add(await RequestSnapshot.CreateAsync(message, cancellationToken));
            return requests.Count == 1
                ? Json(HttpStatusCode.ServiceUnavailable, "{\"code\":\"busy\",\"detail\":\"retry\"}")
                : Json(HttpStatusCode.Accepted, "{\"data\":{\"status\":\"queued\"}}");
        }, new VediSmmClientOptions
        {
            AccessToken = "token",
            MaximumAttempts = 2,
            DelayAsync = static (_, _) => Task.CompletedTask,
        });

        ApiResponse response = await client.CallAsync("publishPost", new CallOptions
        {
            Path = new Dictionary<string, string> { ["id"] = "42" },
            IdempotencyKey = "stable-key",
            JsonBody = new { version = 1 },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("stable-key", request.IdempotencyKey));
        Assert.Equal(requests[0].Body, requests[1].Body);

        int unsafeCalls = 0;
        using var unsafeClient = Client((_, _) =>
        {
            unsafeCalls++;
            return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{\"code\":\"busy\"}"));
        }, new VediSmmClientOptions { MaximumAttempts = 3 });
        await Assert.ThrowsAsync<ApiException>(() => unsafeClient.CallAsync("login", new CallOptions
        {
            JsonBody = new { email = "user@example.com" },
        }, TestContext.Current.CancellationToken));
        Assert.Equal(1, unsafeCalls);
    }

    [Fact]
    public async Task MapsBoundedProblemDetailsAndRateLimitMetadata()
    {
        using var client = Client((_, _) =>
        {
            HttpResponseMessage response = Json(
                HttpStatusCode.TooManyRequests,
                "{\"code\":\"rate_limited\",\"detail\":\"Slow down\",\"errors\":{\"field\":[\"bad\"]}}",
                "req-rate");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
            return Task.FromResult(response);
        });

        RateLimitException failure = await Assert.ThrowsAsync<RateLimitException>(() => client.CallAsync(
            "ping", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("rate_limited", failure.Code);
        Assert.Equal("req-rate", failure.RequestId);
        Assert.Equal(TimeSpan.FromSeconds(12), failure.RetryAfter);
        Assert.Equal("bad", failure.ValidationErrors?.GetProperty("field")[0].GetString());
    }

    [Fact]
    public async Task BoundsBodiesAndHonorsCancellation()
    {
        using var bounded = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{\"data\":\"0123456789\"}")),
            new VediSmmClientOptions { MaximumResponseBytes = 8 });
        await Assert.ThrowsAsync<DecodeException>(() => bounded.CallAsync(
            "ping", cancellationToken: TestContext.Current.CancellationToken));

        using var delayed = Client(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return Json(HttpStatusCode.OK, "{}");
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delayed.CallAsync("ping", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task RedactsProviderFailuresAndRejectsUnsafeConfiguration()
    {
        using var client = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")), new VediSmmClientOptions
        {
            TokenProvider = _ => throw new InvalidOperationException("secret-provider-value"),
        });
        TransportException failure = await Assert.ThrowsAsync<TransportException>(() => client.CallAsync(
            "getMe", cancellationToken: TestContext.Current.CancellationToken));
        Assert.DoesNotContain("secret-provider-value", failure.ToString(), StringComparison.Ordinal);
        Assert.Null(failure.InnerException);

        Assert.Throws<ConfigurationException>(() => new VediSmmClient(new VediSmmClientOptions
        {
            BaseAddress = new Uri("http://example.com/api/v1/"),
        }));

        using var safe = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));
        await Assert.ThrowsAsync<ConfigurationException>(() => safe.CallAsync("ping", new CallOptions
        {
            Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = AuthorizationValues,
            },
        }, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ConfigurationException>(() => safe.CallAsync("ping", new CallOptions
        {
            Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-API-Token"] = AuthorizationValues,
            },
        }, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ConfigurationException>(() => safe.CallAsync("ping", new CallOptions
        {
            Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Test"] = UnsafeHeaderValues,
            },
        }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestDeadlineIncludesTokenResolution()
    {
        using var client = Client((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")), new VediSmmClientOptions
        {
            RequestTimeout = TimeSpan.FromMilliseconds(20),
            TokenProvider = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "never";
            },
        });

        await Assert.ThrowsAsync<RequestTimeoutException>(() => client.CallAsync(
            "getMe", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReflectedTokensAreRedactedFromEveryProblemField()
    {
        using var client = Client((_, _) => Task.FromResult(Json(
            HttpStatusCode.BadRequest,
            "{\"code\":\"memory-token\",\"detail\":\"memory-token\",\"errors\":{\"token\":[\"memory-token\"]}}")),
            new VediSmmClientOptions { AccessToken = "memory-token" });

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => client.CallAsync(
            "getMe", cancellationToken: TestContext.Current.CancellationToken));
        Assert.DoesNotContain("memory-token", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("memory-token", failure.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("memory-token", failure.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("memory-token", failure.ValidationErrors?.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FacadeDisposesOnlySdkOwnedClients()
    {
        int calls = 0;
        using var injected = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        });
        var borrowedFacade = new VediSmm(injected);
        borrowedFacade.Dispose();
        await injected.CallAsync("ping", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);

        var ownedFacade = new VediSmm(new VediSmmClientOptions
        {
            BaseAddress = new Uri("http://localhost/api/v1/"),
        });
        ownedFacade.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => ownedFacade.Client.CallAsync(
            "ping", cancellationToken: TestContext.Current.CancellationToken));
    }

    private static VediSmmClient Client(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        VediSmmClientOptions? options = null) =>
        new(options ?? new VediSmmClientOptions(), new ScriptedHandler(send));

    private static JsonElement Envelope(ApiResponse response)
    {
        Assert.True(ApiResponseEnvelopes.TryGet(response, out JsonElement envelope));
        return envelope;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string? requestId = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (requestId is not null)
        {
            response.Headers.TryAddWithoutValidation("X-Request-ID", requestId);
        }

        return response;
    }

    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed record RequestSnapshot(
        string Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? FallbackToken,
        string? IdempotencyKey,
        string? IfMatch,
        string? Trace,
        string Body)
    {
        public string PathAndQuery => Uri.PathAndQuery;

        public static async Task<RequestSnapshot> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? idempotencyValues);
            request.Headers.TryGetValues("If-Match", out IEnumerable<string>? ifMatchValues);
            request.Headers.TryGetValues("X-Trace", out IEnumerable<string>? traceValues);
            request.Headers.TryGetValues("X-API-Token", out IEnumerable<string>? fallbackValues);
            return new RequestSnapshot(
                request.Method.Method,
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                fallbackValues?.SingleOrDefault(),
                idempotencyValues?.SingleOrDefault(),
                ifMatchValues?.SingleOrDefault(),
                traceValues?.SingleOrDefault(),
                body);
        }
    }
}
