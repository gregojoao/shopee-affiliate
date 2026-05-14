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
using Shopee.Affiliate.Application;

var options = new ShopeeAffiliateOptions
{
    AppId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_APP_ID")!,
    Secret = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_SECRET")!,
    SubIds = new[] { "telegram", "bot" }
};

using var httpClient = new HttpClient();
var client = new ShopeeAffiliateClient(httpClient, options);

var result = await client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
{
    OriginUrl = new Uri("https://shopee.com.br/product/627750190/23798776965")
});

Console.WriteLine(result.AffiliateUrl);
Console.WriteLine(result.Source);
Console.WriteLine(result.Product?.ProductTitle);
Console.WriteLine(result.Product?.ProductPrice);
Console.WriteLine(result.Product?.ProductOriginalPrice);
Console.WriteLine(result.Product?.ProductImageUrl);
```

## Configuration

You can pass credentials manually:

```csharp
using System.Globalization;
using Shopee.Affiliate.Application;

var options = new ShopeeAffiliateOptions
{
    AppId = "your-app-id",
    Secret = "your-secret",
    Endpoint = ShopeeAffiliateOptions.DefaultEndpoint,
    SubIds = new[] { "campaign", "channel" },
    Timeout = TimeSpan.FromSeconds(90),
    PriceCulture = CultureInfo.GetCultureInfo("pt-BR")
};
```

For ASP.NET Core, Worker Services, or any app using `Microsoft.Extensions.DependencyInjection`, register the SDK once:

```csharp
using Shopee.Affiliate.Infrastructure;

builder.Services.AddShopeeAffiliate(builder.Configuration);
```

Then configure secrets through environment variables, user secrets, Key Vault, or any other configuration provider:

```json
{
  "Shopee": {
    "Affiliate": {
      "AppId": "your-app-id",
      "Secret": "your-secret",
      "SubIds": [ "campaign", "channel" ],
      "Timeout": "00:01:30",
      "PriceCulture": "pt-BR"
    }
  }
}
```

In production, prefer environment variables or a secret manager instead of committing secrets to `appsettings.json`.

After registration, inject the client:

```csharp
using Shopee.Affiliate.Application;

public sealed class DealPublisher(IShopeeAffiliateClient shopee)
{
    public async Task PublishAsync(string productUrl)
    {
        var result = await shopee.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
        {
            OriginUrl = new Uri(productUrl)
        });

        Console.WriteLine(result.AffiliateUrl);
    }
}
```

You can also configure options directly in code:

```csharp
using Shopee.Affiliate.Infrastructure;

builder.Services.AddShopeeAffiliate(options =>
{
    options.AppId = builder.Configuration["SHOPEE_AFFILIATE_APP_ID"]!;
    options.Secret = builder.Configuration["SHOPEE_AFFILIATE_SECRET"]!;
    options.SubIds = new[] { "telegram", "bot" };
});
```

| Option | Default | Purpose |
|---|---:|---|
| `Endpoint` | `https://open-api.affiliate.shopee.com.br/graphql` | Shopee Affiliate Open API endpoint for Brazil. |
| `AppId` | Empty | Shopee Affiliate API app ID. |
| `Secret` | Empty | Shopee Affiliate API secret used for request signing. |
| `SubIds` | Empty | Default tracking IDs sent to Shopee when a request does not provide its own `SubIds`. |
| `Timeout` | `00:01:30` | Request timeout. |
| `PriceCulture` | `pt-BR` | Culture used to format currency strings. |

Per-call behavior lives on request objects:

| Request Property | Default | Purpose |
|---|---:|---|
| `ShopeeAffiliateLinkRequest.ResolveShortUrls` | `true` | Resolves short URLs before product offer lookup. |
| `ShopeeAffiliateLinkRequest.Strategy` | `PreferProductOffer` | Chooses product offer, short link, or product-offer-only behavior. |
| `SubIds` | Empty | Optional tracking IDs for this call. When empty, the client uses `ShopeeAffiliateOptions.SubIds`. |

## Main APIs

Use `IShopeeAffiliateClient` when credentials are registered through DI. Use `ShopeeAffiliateClient` directly when you want to provide `ShopeeAffiliateOptions` in code.

### `GenerateAffiliateLinkAsync`

High-level helper for the usual bot workflow. It tries to extract product identifiers, query the affiliate product offer, and return the best affiliate URL available.

```csharp
ShopeeAffiliateLinkResult result = await client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
{
    OriginUrl = new Uri(url),
    Strategy = ShopeeAffiliateLinkStrategy.PreferProductOffer
});
```

Available strategies:

| Strategy | Behavior |
|---|---|
| `PreferProductOffer` | Tries `productOfferV2`; falls back to `generateShortLink` if the product offer lookup fails or returns no offer link. |
| `ShortLinkOnly` | Calls `generateShortLink` directly. |
| `ProductOfferOnly` | Requires a valid product offer link and throws `ShopeeAffiliateApiException` when it is unavailable. |

### `GenerateShortLinkAsync`

Calls `generateShortLink` directly.

```csharp
ShopeeShortLinkResult result = await client.GenerateShortLinkAsync(new ShopeeShortLinkRequest
{
    OriginUrl = new Uri(url)
});

Console.WriteLine(result.ShortLink);
```

### `GetProductOfferAsync`

Calls `productOfferV2` directly for a known product identity.

```csharp
using Shopee.Affiliate.Domain;

var identity = new ShopeeAffiliateProductIdentity(
    ShopId: "627750190",
    ItemId: "23798776965");

ShopeeProductOffer? offer = await client.GetProductOfferAsync(new ShopeeProductOfferRequest
{
    ProductIdentity = identity
});

Console.WriteLine(offer?.ProductTitle);
```

### `ResolveShopeeUrlAsync`

Follows redirects for Shopee short URLs and returns the final URL when possible.

```csharp
Uri resolvedUrl = await client.ResolveShopeeUrlAsync(new Uri(shortUrl));
```

## Architecture

The SDK is organized with a small DDD-inspired structure:

| Layer | Responsibility |
|---|---|
| `Domain` | Product identity parsing, price formatting, and affiliate offer value objects. |
| `Application` | Public use cases and service abstractions such as `ShopeeAffiliateClient`, `IShopeeAffiliateClient`, requests, options, and results. |
| `Infrastructure` | GraphQL payloads, Shopee authentication, response mapping, HTTP integration, exceptions, and DI registration. |

Public namespaces follow the physical project structure: use `Shopee.Affiliate.Application` for clients/options/results, `Shopee.Affiliate.Domain` for value objects, and `Shopee.Affiliate.Infrastructure` for DI registration and infrastructure exceptions.

## Returned Data

`ShopeeAffiliateLinkResult` contains:

| Property | Description |
|---|---|
| `AffiliateUrl` | Best affiliate URL returned by the SDK. |
| `Source` | Indicates whether the affiliate URL came from `ProductOffer` or `ShortLink`. |
| `ResolvedOriginUrl` | URL after redirect resolution, when resolution was requested. |
| `Product` | Normalized product offer data, when product offer lookup succeeds. |

`ShopeeProductOffer` contains:

| Property | Description |
|---|---|
| `AffiliateUrl` | Product offer affiliate URL. |
| `ProductTitle` | Product title. |
| `ProductPrice` | Formatted current price or price range. |
| `ProductOriginalPrice` | Estimated original price when discount data is available. |
| `ProductImageUrl` | Product image URL. |
| `ProductUrl` | Canonical product URL from Shopee. |
| `ItemId` / `ShopId` | Shopee product identifiers. |

## Supported URL Formats

The SDK can extract `shopId` and `itemId` from these common formats:

```text
https://shopee.com.br/product/{shopId}/{itemId}
https://shopee.com.br/{slug}-i.{shopId}.{itemId}
https://shopee.com.br/opaanlp/{shopId}/{itemId}
https://shopee.com.br/...?...shopid={shopId}&itemid={itemId}
```

When `ShopeeAffiliateLinkRequest.ResolveShortUrls` is enabled, short URLs are resolved before product identity extraction.

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
