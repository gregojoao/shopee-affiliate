# Shopee.Affiliate

[![CI](https://github.com/gregojoao/shopee-affiliate/actions/workflows/ci.yml/badge.svg)](https://github.com/gregojoao/shopee-affiliate/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Shopee.Affiliate.svg)](https://www.nuget.org/packages/Shopee.Affiliate)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-512BD4.svg)](https://dotnet.microsoft.com/)

Small .NET SDK for Shopee Affiliate Open API workflows.

Maintained by **Greco Labs**.

`Shopee.Affiliate` helps affiliate bots, deal monitors, content automations, and back-office tools turn Shopee product URLs into affiliate-ready links while optionally enriching the response with product offer data.

## What It Does

| Capability | Description |
|---|---|
| Affiliate links | Converts Shopee product URLs into affiliate short links through `generateShortLink`. |
| Product offers | Queries affiliate product offer data through `productOfferV2` when product identifiers are available. |
| Product metadata | Extracts title, image URL, product URL, current price, discount rate, and offer link from the API response. |
| Original price estimate | Calculates an approximate original price from `priceMin` and `priceDiscountRate`. |
| URL parsing | Extracts `shopId` and `itemId` from common Shopee URL formats. |
| Short URL resolving | Follows Shopee short URLs before trying product offer lookup. |

## Installation

After the package is published to NuGet:

```bash
dotnet add package Shopee.Affiliate
```

To build from source:

```bash
git clone https://github.com/gregojoao/shopee-affiliate.git
cd shopee-affiliate
dotnet restore
dotnet test
dotnet pack -c Release
```

The package is generated at:

```text
src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.<version>.nupkg
```

## Quick Start

```csharp
using Shopee.Affiliate;

var options = new ShopeeAffiliateOptions
{
    AppId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_APP_ID")!,
    Secret = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_SECRET")!,
    SubIds = new[] { "telegram", "bot" }
};

using var httpClient = new HttpClient();
var client = new ShopeeAffiliateClient(httpClient);

var result = await client.GenerateAffiliateLinkAsync(
    "https://shopee.com.br/product/627750190/23798776965",
    options);

Console.WriteLine(result.AffiliateUrl);
Console.WriteLine(result.ProductTitle);
Console.WriteLine(result.ProductPrice);
Console.WriteLine(result.ProductOriginalPrice);
Console.WriteLine(result.ProductImageUrl);
```

## Configuration

```csharp
var options = new ShopeeAffiliateOptions
{
    AppId = "your-app-id",
    Secret = "your-secret",
    Endpoint = ShopeeAffiliateOptions.DefaultEndpoint,
    SubIds = new[] { "campaign", "channel" },
    ResolveShortUrls = true,
    PreferProductOffer = true,
    FallbackToShortLink = true,
    PriceCultureName = "pt-BR"
};
```

| Option | Default | Purpose |
|---|---:|---|
| `Endpoint` | `https://open-api.affiliate.shopee.com.br/graphql` | Shopee Affiliate Open API endpoint for Brazil. |
| `AppId` | Empty | Shopee Affiliate API app ID. |
| `Secret` | Empty | Shopee Affiliate API secret used for request signing. |
| `SubIds` | Empty | Optional tracking IDs sent to Shopee. |
| `TimeoutMilliseconds` | `90000` | Request timeout. |
| `ResolveShortUrls` | `true` | Resolves short URLs before product offer lookup. |
| `PreferProductOffer` | `true` | Tries `productOfferV2` before falling back to short link generation. |
| `FallbackToShortLink` | `true` | Uses `generateShortLink` when product offer lookup fails or returns no offer link. |
| `PriceCultureName` | `pt-BR` | Culture used to format currency strings. |

## Main APIs

### `GenerateAffiliateLinkAsync`

High-level helper for the usual bot workflow. It tries to extract product identifiers, query the affiliate product offer, and return the best affiliate URL available.

```csharp
ShopeeAffiliateLinkResult result = await client.GenerateAffiliateLinkAsync(url, options);
```

### `GenerateShortLinkAsync`

Calls `generateShortLink` directly.

```csharp
using ShopeeShortLinkResult result = await client.GenerateShortLinkAsync(url, options);
Console.WriteLine(result.ShortLink);
```

### `GetProductOfferAsync`

Calls `productOfferV2` directly for a known product identity.

```csharp
var identity = new ShopeeAffiliateProductIdentity(
    ShopId: "627750190",
    ItemId: "23798776965");

using ShopeeProductOfferResult result = await client.GetProductOfferAsync(identity, options);
Console.WriteLine(result.ProductOffer?.ProductTitle);
```

### `ResolveShopeeUrlAsync`

Follows redirects for Shopee short URLs and returns the final URL when possible.

```csharp
string resolvedUrl = await client.ResolveShopeeUrlAsync(shortUrl, options);
```

## Returned Data

`ShopeeAffiliateLinkResult` contains:

| Property | Description |
|---|---|
| `AffiliateUrl` | Best affiliate URL returned by the SDK. |
| `ShortLink` | Same URL used for compatibility with short-link workflows. |
| `ProductTitle` | Product title when offer lookup succeeds. |
| `ProductPrice` | Formatted current price or price range. |
| `ProductOriginalPrice` | Estimated original price when discount data is available. |
| `ProductImageUrl` | Product image URL. |
| `ProductUrl` | Canonical product URL from Shopee. |
| `FinalProductUrl` | Final resolved product URL. |
| `ResolvedUrl` | URL after redirect resolution. |
| `ProductOffer` | Raw normalized offer data, when available. |

## Supported URL Formats

The SDK can extract `shopId` and `itemId` from these common formats:

```text
https://shopee.com.br/product/{shopId}/{itemId}
https://shopee.com.br/{slug}-i.{shopId}.{itemId}
https://shopee.com.br/opaanlp/{shopId}/{itemId}
https://shopee.com.br/...?...shopid={shopId}&itemid={itemId}
```

When `ResolveShortUrls` is enabled, short URLs are resolved before product identity extraction.

## Authentication

Requests are signed with SHA-256:

```text
Authorization: SHA256 Credential={AppId}, Timestamp={Timestamp}, Signature={Signature}
Signature = SHA256(AppId + Timestamp + Payload + Secret)
```

`Timestamp` is Unix time in seconds. `Payload` must be the exact JSON body sent to the GraphQL endpoint.

## API Notes

Shopee's official Help Center says the Affiliate API can retrieve Shopee, brand, and product offer lists, generate short links, and retrieve conversion reports:

https://help.shopee.sg/portal/10/article/191702-API-Access

For Brazil, the affiliate Open API is exposed as GraphQL. Publicly available documentation and existing integrations use:

- `productOfferV2`
- `generateShortLink`

Important: the public affiliate schema used by this SDK does not expose a dedicated "old price" field. `ProductOriginalPrice` is calculated with:

```text
original = current / (1 - discountRate / 100)
```

This is useful for promotion messages, but it can differ slightly from Shopee's displayed price because `priceDiscountRate` may be rounded.

## Development

```bash
dotnet restore
dotnet test
dotnet pack -c Release
```

## Publishing

Before publishing a new NuGet version:

1. Update `<Version>` and `<PackageReleaseNotes>` in `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.
2. Run the validation:

```bash
dotnet test
dotnet pack -c Release
```

3. Push the package:

```bash
dotnet nuget push src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

See [PUBLISHING.md](PUBLISHING.md) for the full release checklist.

## License

MIT. See [LICENSE](LICENSE).
