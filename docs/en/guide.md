# VediSMM .NET SDK guide

## Client lifetime and authentication

Create one `VediSmm` facade per application lifetime. The options constructor creates one redirect-denying `SocketsHttpHandler` and owns that connection pool. The constructor accepting a `VediSmmClient` borrows the client, so disposing the facade does not dispose caller-owned state.

Use `AccessToken` for a fixed memory-only token or `TokenProvider` to resolve a rotated token immediately before an authenticated request. They are mutually exclusive. The token provider receives the same finite cancellation deadline as the HTTP exchange.

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

## Streaming media

Set `ContentFactory` to create a fresh `HttpContent` for every eligible upload attempt. Set `ResponseStream` to stream a successful download into a caller-owned `Stream`; the SDK writes to it but does not dispose it.

## Publication jobs

`JobWaiter` uses a monotonic finite timeout and cooperative polling interval. It returns a succeeded representation, throws `JobFailedException` for failed/cancelled jobs, and starts no background poller.

## Webhooks

Pass the exact raw request bytes, timestamp header, and `v1=` signature to `WebhookVerifier.VerifyAsync`. Verification applies timestamp tolerance, HMAC-SHA256, constant-time comparison, and an optional atomic replay claim. `MemoryReplayStore` is safe within one process; distributed systems should implement `IReplayStore` with a shared atomic store.

## Complete API

See the [API reference](api-reference.md) for every one of the 83 user operations and its required scopes.
