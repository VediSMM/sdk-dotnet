# Changelog

## 0.2.0 — 2026-08-15

- Added immutable tracking-link create/list/get/disable/archive services and lazy cursor iteration; destination update remains intentionally absent.
- Added all six tracking-analytics endpoints, nullable JSON-map filters, GeoJSON preservation, and lazy link/post/source pagers.
- Added immutable `CallOptions` factories/composition with pre-I/O `Idempotency-Key` and `If-Match` validation and reserved-filter protection.
- Added canonical post `TrackingSettings` and `PostTrackingRequestOptions` without client-side URL rewriting or injected defaults.
- Synchronized generated sources and documentation with API contract 1.1.0: 94 operations and generic ETag/service-unavailable capabilities.

## 0.1.1 — 2026-07-20

- Send authenticated credentials through both `Authorization: Bearer` and the API's `X-API-Token` proxy fallback.
- Reserve `X-API-Token` so custom headers cannot replace the SDK-managed credential.

## 0.1.0 — 2026-07-19

- Added all 83 public user API v1 operations across 17 native services.
- Added secure reusable HTTP transport, strict deadlines, bounded safe retries, streaming, typed Problem Details, and transport metadata.
- Added cursor pagination, publication job polling, UUID v4 idempotency keys, and raw-byte webhook verification with replay protection.
- Added English and Russian documentation, package artifacts, contract verification, analyzers, coverage gates, and .NET 8/10 CI.
