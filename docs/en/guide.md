# VediSMM .NET SDK guide

## Client lifetime and authentication

Create one `VediSmm` facade per application lifetime. The options constructor creates one redirect-denying `SocketsHttpHandler` and owns that connection pool. The constructor accepting a `VediSmmClient` borrows the client, so disposing the facade does not dispose caller-owned state.

Use `AccessToken` for a fixed memory-only token or `TokenProvider` to resolve a rotated token immediately before an authenticated request. They are mutually exclusive. The token provider receives the same finite cancellation deadline as the HTTP exchange.

Authenticated calls send the same in-memory credential through both
`Authorization: Bearer` and VediSMM's `X-API-Token` proxy fallback. The API
gives `Authorization` priority. Both headers are SDK-managed and must never be
logged.

## Calls and cancellation

Every domain operation returns `Task<ApiResponse>` and accepts a `CancellationToken`:

```csharp
var options = new CallOptions
{
    Query = new Dictionary<string, IReadOnlyList<string>>
    {
        ["from"] = new[] { "2026-07-01" },
        ["to"] = new[] { "2026-07-19" },
    },
};
ApiResponse summary = await sdk.Analytics.GetAnalyticsSummaryAsync(options, cancellationToken);
```

Path and query values are percent-encoded by the SDK. Do not pre-encode them. Header names and values are checked for injection, and security-sensitive headers remain SDK-owned.

## Errors

Unsuccessful responses never become normal results:

- `ApiException` preserves status, stable code, redacted detail, validation errors, request ID, and `Retry-After`;
- `RateLimitException` identifies HTTP 429;
- `PreconditionFailedException` identifies HTTP 412;
- `RedirectException`, `RequestTimeoutException`, `TransportException`, and `DecodeException` remain distinct;
- caller cancellation remains `OperationCanceledException`.

Error bodies and successful JSON buffers are bounded. Token-shaped values reflected by a server are removed from all exposed Problem Details fields.

## Idempotency and optimistic concurrency

```csharp
var command = new CallOptions
{
    Path = new Dictionary<string, string> { ["id"] = "42" },
    IdempotencyKey = Idempotency.NewKey(),
    IfMatch = "\"post-v7\"",
    JsonBody = new { version = 7 },
};
ApiResponse job = await sdk.Jobs.PublishPostAsync(command, cancellationToken);
```

The SDK snapshots replayable JSON/raw bodies and retains the same idempotency key across bounded retries. A mutating request without an idempotency guarantee is never retried.

## Cursor pagination

`CursorPager<T>` fetches lazily, passes only the opaque prior cursor to your callback, detects empty/repeated cursors, and propagates cancellation. The callback remains explicit so applications choose their own response model.

## Tracking links and click analytics

`TrackingLinks` exposes create, list, get, disable, and archive. Destination update is deliberately absent because a destination is immutable. Create requires `CallOptions.WithIdempotencyKey(...)`; disable and archive require `CallOptions.WithIfMatch(...)`. These focused methods reject their canonical path/query fields if they are also injected through generic `CallOptions`, while preserving unrelated headers, extensions, deadlines, sinks, and cancellation tokens.

```csharp
ApiResponse link = await sdk.TrackingLinks.CreateAsync(
    new Dictionary<string, object?>
    {
        ["destination_url"] = "https://example.com/raw?a=1#part",
    },
    CallOptions.WithIdempotencyKey("create-link-1"),
    cancellationToken);
await sdk.TrackingLinks.DisableAsync(
    link.Data!.Value.GetProperty("id").GetInt64(),
    CallOptions.WithIfMatch(link.ETag!),
    cancellationToken);
```

`TrackingAnalytics` provides summary, timeseries, links, posts, sources, and geo views. `IterateLinks`, `IteratePosts`, `IterateSources`, and `TrackingLinks.Iterate` use the existing lazy `CursorPager<JsonElement>` convention and preserve opaque cursors. Nullable filter/map values are omitted from query strings. Geo city rows remain the server's privacy-thresholded GeoJSON `FeatureCollection`; coordinates stay `[longitude, latitude]` and the SDK performs no enrichment.

For post create/update JSON, explicitly nest server-owned behavior under `options.tracking`:

```csharp
var options = new PostTrackingRequestOptions
{
    Tracking = new TrackingSettings
    {
        ShortenLinks = false,
        AddSource = false,
    },
};
```

The JSON names are `shorten_links` and `add_source`. Both defaults are false. The SDK never inserts these settings, rewrites URLs, or duplicates server attribution rules.

## Streaming media

Set `ContentFactory` to create a fresh `HttpContent` for every eligible upload attempt. Set `ResponseStream` to stream a successful download into a caller-owned `Stream`; the SDK writes to it but does not dispose it.

## Publication jobs

`JobWaiter` uses a monotonic finite timeout and cooperative polling interval. It returns a succeeded representation, throws `JobFailedException` for failed/cancelled jobs, and starts no background poller.

## Webhooks

Pass the exact raw request bytes, timestamp header, and `v1=` signature to `WebhookVerifier.VerifyAsync`. Verification applies timestamp tolerance, HMAC-SHA256, constant-time comparison, and an optional atomic replay claim. `MemoryReplayStore` is safe within one process; distributed systems should implement `IReplayStore` with a shared atomic store.

## Complete API

See the [API reference](api-reference.md) for every one of the 94 user operations and its required scopes.
