# Changelog

## 0.1.1 — 2026-07-20

- Send authenticated credentials through both `Authorization: Bearer` and the API's `X-API-Token` proxy fallback.
- Reserve `X-API-Token` so custom headers cannot replace the SDK-managed credential.

## 0.1.0 — 2026-07-19

- Added all 83 public user API v1 operations across 17 native services.
- Added secure reusable HTTP transport, strict deadlines, bounded safe retries, streaming, typed Problem Details, and transport metadata.
- Added cursor pagination, publication job polling, UUID v4 idempotency keys, and raw-byte webhook verification with replay protection.
- Added English and Russian documentation, package artifacts, contract verification, analyzers, coverage gates, and .NET 8/10 CI.
