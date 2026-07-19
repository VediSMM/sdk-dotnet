using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? IdempotencyKey,
        string Body)
    {
        public static async Task<RequestSnapshot> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? idempotencyValues);
            return new RequestSnapshot(
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                idempotencyValues?.SingleOrDefault(),
                body);
        }
    }
}
