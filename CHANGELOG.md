# Changelog

All notable changes to **Shopee.Affiliate** are documented in this file. The
project follows [Semantic Versioning](https://semver.org/) and the
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

## [1.1.0] - 2026-05-18

### Added
- New `Shopee.Affiliate.Reports` namespace with a public reporting client.
  - `IShopeeAffiliateReportsClient` / `ShopeeAffiliateReportsClient` consuming
    the `conversionReport` GraphQL query for conversions, sales summary, click
    stats, and link-usage metrics.
  - Request records: `ListShopeeConversionsRequest`,
    `ShopeeSalesSummaryRequest`, `ShopeeClickStatsRequest`,
    `ShopeeLinkUsageRequest`.
  - Response records: `ShopeeConversionPage`, `ShopeeConversion`,
    `ShopeeConversionDetail`, `ShopeeOrderLine`, `ShopeeSalesSummary`,
    `ShopeeTopProduct`, `ShopeeTopShop`, `ShopeeTopSubId`, `ShopeeClickStats`,
    `ShopeeClickPoint`, `ShopeeLinkUsage`, `Money`.
  - Enums: `ShopeeOrderStatus`, `ShopeeConversionStatusFilter`,
    `ShopeeReportGranularity`.
  - `ShopeeAffiliateReportsOptions` (separate AppId/Secret/Endpoint/Timeout for
    reporting traffic).
  - `AddShopeeAffiliateReports` DI extension that registers a named
    `HttpClient` and validates options on start.
- New typed exception hierarchy rooted at `ShopeeAffiliateException`:
  `ShopeeAffiliateAuthException` (Shopee codes `10020/31/32/33/34/35`),
  `ShopeeAffiliateRateLimitException` (`10030`),
  `ShopeeAffiliateNotFoundException`, `ShopeeAffiliateUnsupportedException`.
- New `Shopee.Affiliate.Signing.ShopeeSignatureBuilder` — central SHA256
  authorization header builder reused by the link client and the reports
  client.

### Changed
- `ShopeeAffiliateApiException` now derives from `ShopeeAffiliateException`
  (the new base) and exposes `Code`, `Path`, and `RequestId` properties. The
  type is no longer `sealed`. Existing `catch (ShopeeAffiliateApiException)`
  call sites continue to compile and catch the same failures.
- `Infrastructure.ShopeeAffiliateAuthenticator` delegates to the new
  `ShopeeSignatureBuilder`; the public surface is preserved for SDK callers.

### Unsupported metrics
- `GetClickStatsAsync` and `GetGeneratedLinkUsageAsync` always return
  `Supported=false`: the Shopee Affiliate Open API does not currently expose
  click or link-generation counters. The DTO shape is stable so adopting the
  metric requires no client-code changes once Shopee publishes the query.

### Schema notes (confirmed via live introspection)
- `conversionReport` accepts `Int64` arguments only as literal values, not
  through GraphQL `variables`. The SDK inlines numeric/enum arguments and
  JSON-escapes string arguments (`scrollId`, `orderId`) before serialising the
  query.
- `orderStatus` is the enum `DisplayOrderStatus { ALL UNPAID PENDING COMPLETED
  CANCELLED }`.
- The query exposes no `subId` argument; Sub Id filtering is applied
  client-side against the `utmContent` field returned on each row.
- Monetary fields (`totalCommission`, `netCommission`, `itemPrice`,
  `actualAmount`, `refundAmount`, …) are returned as `String` scalars; the SDK
  parses them with `InvariantCulture`.
- `completeTime` lives on each `orders[].items[]` row, not on the conversion
  root.
- `conversionReport` enforces a maximum window of ~90 days between
  `purchaseTimeStart` and `purchaseTimeEnd`. Wider windows are rejected with
  error code `11001` (`"Params Error : can only query data for the last 3
  months"`) and surface as `ShopeeAffiliateApiException`.

## [1.0.0] - 2026-04-23

Initial public release: SHA256-signed GraphQL client for the Shopee Affiliate
Open API (`generateShortLink`, `productOfferV2`) with strategy-based fallback
and DI integration.
