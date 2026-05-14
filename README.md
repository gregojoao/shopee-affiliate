# Shopee.Affiliate

A small .NET client for the Shopee Affiliate Open API.

The NuGet package targets .NET 8 and .NET 10.

Maintained by Greco Labs.

It focuses on the workflow most affiliate bots need:

- Convert Shopee URLs into affiliate short links.
- Query product offers through `productOfferV2`.
- Extract product title, current price, image URL, product URL, and offer link.
- Calculate an approximate original price from `priceMin` and `priceDiscountRate`.
- Parse Shopee product identifiers from common URL formats.

## Status

This package is ready for an initial NuGet release, but the API itself requires Shopee Affiliate Open API credentials.

The default endpoint targets Brazil:

```text
https://open-api.affiliate.shopee.com.br/graphql
```

You can override the endpoint through `ShopeeAffiliateOptions.Endpoint` for other Shopee affiliate regions.

## API Notes

Shopee's official Help Center says the Affiliate API can retrieve Shopee, brand, and product offer lists, generate short links, and retrieve conversion reports:

https://help.shopee.sg/portal/10/article/191702-API-Access

For Brazil, the affiliate Open API is exposed as GraphQL. Publicly available documentation and the existing bot integration use these operations:

- `productOfferV2`
- `generateShortLink`

`productOfferV2` exposes the product fields this library maps:

- `itemId`
- `productName`
- `productLink`
- `offerLink`
- `imageUrl`
- `priceMin`
- `priceMax`
- `priceDiscountRate`
- `shopId`

Important: the public affiliate schema does not expose a dedicated "old price" field. `ProductOriginalPrice` is calculated with:

```text
original = current / (1 - discountRate / 100)
```

This is useful for promotion messages, but it can differ slightly from Shopee's displayed price because `priceDiscountRate` is rounded.

## Installation

After the package is published:

```bash
dotnet add package Shopee.Affiliate
```

To build a local package:

```bash
dotnet pack -c Release
```

The package will be generated at:

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

## Main Types

### `ShopeeAffiliateClient`

The main API client. It provides:

- `GenerateAffiliateLinkAsync`
- `GenerateShortLinkAsync`
- `GetProductOfferAsync`
- `ResolveShopeeUrlAsync`

### `ShopeeAffiliateOptions`

Configuration object with:

- `Endpoint`
- `AppId`
- `Secret`
- `SubIds`
- `TimeoutMilliseconds`
- `ResolveShortUrls`
- `PreferProductOffer`
- `FallbackToShortLink`
- `PriceCultureName`

### `ShopeeAffiliateLinkResult`

Normalized result with:

- `AffiliateUrl`
- `ShortLink`
- `ProductTitle`
- `ProductPrice`
- `ProductOriginalPrice`
- `ProductImageUrl`
- `ProductUrl`
- `FinalProductUrl`
- `ResolvedUrl`
- `ProductOffer`

## URL Formats

The library can extract `shopId` and `itemId` from:

```text
https://shopee.com.br/product/{shopId}/{itemId}
https://shopee.com.br/{slug}-i.{shopId}.{itemId}
https://shopee.com.br/opaanlp/{shopId}/{itemId}
https://shopee.com.br/...?...shopid={shopId}&itemid={itemId}
```

Short URLs can be resolved before querying `productOfferV2` when `ResolveShortUrls` is enabled.

## Authentication

Shopee Affiliate Open API requests are signed with SHA-256:

```text
Authorization: SHA256 Credential={AppId}, Timestamp={Timestamp}, Signature={Signature}
Signature = SHA256(AppId + Timestamp + Payload + Secret)
```

`Timestamp` is Unix time in seconds. `Payload` must be the exact JSON body sent to the GraphQL endpoint.

## Development

```bash
dotnet restore
dotnet test
dotnet pack -c Release
```

## Publishing

Before publishing to NuGet:

1. Update the package version in `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.
2. Create a release package:

```bash
dotnet test
dotnet pack -c Release
```

3. Push to NuGet:

```bash
dotnet nuget push src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

## License

MIT
