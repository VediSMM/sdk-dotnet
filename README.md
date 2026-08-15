# VediSMM .NET SDK

Native .NET 8+ client for all 94 operations of the VediSMM public user API v1. It provides a reusable secure HTTP transport, `Task`/`CancellationToken`, typed exceptions, cursor pagination, finite job polling, streaming, idempotency helpers, and raw-byte webhook verification. Administrative endpoints are intentionally absent.

[Русская документация](README.ru.md) · [English guide](docs/en/guide.md) · [API reference](docs/en/api-reference.md) · [VediSMM API docs](https://vedismm.ru/docs/api)

## Requirements

- .NET 8 or .NET 10 SDK

## Install from the immutable GitHub tag

Until NuGet trusted publishing is configured, build the exact immutable source tag into a local package directory:

```bash
git clone --branch v0.2.0 --depth 1 https://github.com/VediSMM/sdk-dotnet.git
cd sdk-dotnet
dotnet pack src/VediSMM/VediSMM.csproj --configuration Release --output ./artifacts
```

Then add the native package to a consumer project:

```bash
dotnet add package VediSMM --version 0.2.0 --source ../sdk-dotnet/artifacts
```

## Quick start

```csharp
using VediSMM;

using var sdk = new VediSmm(new VediSmmClientOptions
{
    AccessToken = Environment.GetEnvironmentVariable("VEDISMM_ACCESS_TOKEN"),
});

ApiResponse response = await sdk.Accounts.ListAccountsAsync(
    cancellationToken: cancellationToken);
Console.WriteLine(response.Data);
```

Reuse one `VediSmm` instance for the application lifetime. A facade created from options owns and disposes its safe connection pool. A facade created from an existing `VediSmmClient` borrows it and never disposes it.

The facade includes focused `TrackingLinks` and `TrackingAnalytics` services:

```csharp
ApiResponse created = await sdk.TrackingLinks.CreateAsync(
    new Dictionary<string, object?>
    {
        ["destination_url"] = "https://example.com/article?a=1#part",
    },
    CallOptions.WithIdempotencyKey("create-link-1"),
    cancellationToken);
ApiResponse summary = await sdk.TrackingAnalytics.SummaryAsync(
    new Dictionary<string, object?>
    {
        ["from"] = "2026-08-01",
        ["to"] = "2026-08-13",
    },
    cancellationToken: cancellationToken);
```

Destinations remain unchanged strings. Validation, shortening, attribution, and redirects are server-owned. There is intentionally no destination-update method; create a new link for another destination. Disable/archive calls require `CallOptions.WithIfMatch(...)` before any HTTP I/O. Post create/update bodies can explicitly nest `TrackingSettings`; the SDK never injects tracking settings or rewrites post URLs.

## Safety defaults

- production URL is `https://vedismm.ru/api/v1/`;
- redirects are rejected and Bearer credentials never cross origins;
- tokens stay memory-only and are redacted from errors and diagnostic text;
- credential resolution, HTTP work, retries, and body reads share one finite deadline;
- successful and error buffers have strict upper bounds;
- retries are bounded and restricted to idempotent or explicitly idempotent calls;
- `Retry-After`, `Idempotency-Key`, `If-Match`, request IDs, and ETags are preserved;
- uploads can create a fresh stream per retry and downloads can stream to a caller-owned sink;
- webhook signatures use exact raw bytes, HMAC-SHA256, constant-time comparison, timestamp tolerance, and optional atomic replay protection.

See the [guide](docs/en/guide.md) for tracking settings, analytics, authentication, errors, pagination, media, jobs, and webhooks.

## Contract

- API contract: `1.1.0`
- Contract SHA-256: `08e7b140c93a1ef16aa54de509799388e8b01da36b93e47e9733a36e57e40f8a`
- User operations: `94/94`
- License: MIT
