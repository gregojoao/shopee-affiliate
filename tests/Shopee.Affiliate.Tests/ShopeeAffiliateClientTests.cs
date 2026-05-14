using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Shopee.Affiliate.Tests;

public sealed class ShopeeAffiliateClientTests
{
    [Fact]
    public void BuildGenerateShortLinkPayload_EscapesUrlAndSubIds()
    {
        var payload = ShopeeAffiliateClient.BuildGenerateShortLinkPayload(
            "https://s.shopee.com.br/50VRdFsnBr?name=\"chair\"",
            new[] { "telegram", "bot" });

        payload.Should().Be(
            "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr?name=\\\\\\\"chair\\\\\\\"\\\", subIds: [\\\"telegram\\\", \\\"bot\\\"] }) { shortLink } }\"}");
    }

    [Fact]
    public void BuildGenerateShortLinkPayload_OmitsEmptySubIds()
    {
        var payload = ShopeeAffiliateClient.BuildGenerateShortLinkPayload("https://s.shopee.com.br/50VRdFsnBr");

        payload.Should().Be(
            "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\" }) { shortLink } }\"}");
    }

    [Fact]
    public void BuildProductOfferPayload_RequestsProductMetadataAndOfferLink()
    {
        var payload = ShopeeAffiliateClient.BuildProductOfferPayload(
            new ShopeeAffiliateProductIdentity("627750190", "23798776965"));

        payload.Should().Be(
            "{\"query\":\"{ productOfferV2(shopId: 627750190, itemId: 23798776965, limit: 1) { nodes { itemId productName productLink offerLink imageUrl priceMin priceMax priceDiscountRate } pageInfo { page limit hasNextPage } } }\"}");
    }

    [Fact]
    public void CreateSignature_UsesSha256OverAppIdTimestampPayloadAndSecret()
    {
        var payload = "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\" }) { shortLink } }\"}";

        ShopeeAffiliateClient.CreateSignature("123", 1704067200, payload, "secret")
            .Should().Be("eec3e1f8269df06e7090121d358cfcdc5814efaff9773102031b887657998e01");
    }

    [Fact]
    public void BuildAuthorizationHeader_FormatsShopeeSha256Header()
    {
        var header = ShopeeAffiliateClient.BuildAuthorizationHeader("123", 1704067200, "{}", "secret");

        header.Should().MatchRegex("^SHA256 Credential=123, Timestamp=1704067200, Signature=[a-f0-9]{64}$");
    }

    [Fact]
    public void ExtractShortLink_ReadsGraphQlShortLink()
    {
        using var document = JsonDocument.Parse("""
        {
          "data": {
            "generateShortLink": {
              "shortLink": "https://s.shopee.com.br/api-link"
            }
          }
        }
        """);

        ShopeeAffiliateClient.ExtractShortLink(document.RootElement)
            .Should().Be("https://s.shopee.com.br/api-link");
    }

    [Fact]
    public void ExtractProductOffer_ReadsAffiliateLinkTitleAndPrice()
    {
        using var document = JsonDocument.Parse("""
        {
          "data": {
            "productOfferV2": {
              "nodes": [
                {
                  "itemId": 23798776965,
                  "productName": " Cadeira de Escritorio Zinnia   Venecia SL ",
                  "productLink": "https://shopee.com.br/product/627750190/23798776965",
                  "offerLink": "https://s.shopee.com.br/offer-link",
                  "imageUrl": "https://cf.shopee.com.br/file/image",
                  "priceMin": "334.99",
                  "priceMax": "334.99",
                  "priceDiscountRate": 20
                }
              ]
            }
          }
        }
        """);

        var productOffer = ShopeeAffiliateClient.ExtractProductOffer(document.RootElement);

        productOffer.Should().BeEquivalentTo(new ShopeeProductOffer(
            AffiliateUrl: "https://s.shopee.com.br/offer-link",
            ProductTitle: "Cadeira de Escritorio Zinnia Venecia SL",
            ProductPrice: "R$ 334,99",
            ProductOriginalPrice: "R$ 418,74",
            ProductImageUrl: "https://cf.shopee.com.br/file/image",
            ProductUrl: "https://shopee.com.br/product/627750190/23798776965",
            ImageUrl: "https://cf.shopee.com.br/file/image",
            ItemId: "23798776965",
            ShopId: "627750190",
            PriceMin: "334.99",
            PriceMax: "334.99",
            PriceDiscountRate: "20"));
    }

    [Fact]
    public void ExtractShortLink_ThrowsGraphQlErrors()
    {
        using var document = JsonDocument.Parse("""
        {
          "errors": [
            { "message": "Invalid Signature" }
          ]
        }
        """);

        var act = () => ShopeeAffiliateClient.ExtractShortLink(document.RootElement);

        act.Should().Throw<ShopeeAffiliateApiException>()
            .WithMessage("*Invalid Signature*");
    }

    [Theory]
    [InlineData("https://shopee.com.br/Cadeira-i.627750190.23798776965?utm_source=test", "627750190", "23798776965")]
    [InlineData("https://shopee.com.br/product/627750190/23798776965", "627750190", "23798776965")]
    [InlineData("https://shopee.com.br/opaanlp/347988064/9212570285?uls_trackid=test", "347988064", "9212570285")]
    public void TryExtractProductIdentity_ReadsProductIdsFromUrls(
        string url,
        string expectedShopId,
        string expectedItemId)
    {
        var success = ShopeeAffiliateClient.TryExtractProductIdentity(url, out var identity);

        success.Should().BeTrue();
        identity.ShopId.Should().Be(expectedShopId);
        identity.ItemId.Should().Be(expectedItemId);
    }

    [Fact]
    public void FormatShopeePriceRange_FormatsSinglePricesAndRanges()
    {
        ShopeeAffiliateClient.FormatShopeePriceRange("334.99", "334.99").Should().Be("R$ 334,99");
        ShopeeAffiliateClient.FormatShopeePriceRange("1000", "1299.9").Should().Be("R$ 1.000,00 - R$ 1.299,90");
        ShopeeAffiliateClient.FormatShopeePriceRange("", "").Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateShortLinkAsync_SignsAndPostsPayload()
    {
        CapturedRequest? capturedRequest = null;
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            capturedRequest = CapturedRequest.From(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "data": {
                    "generateShortLink": {
                      "shortLink": "https://s.shopee.com.br/api-link"
                    }
                  }
                }
                """)
            };
        }));
        var client = new ShopeeAffiliateClient(httpClient, () => 1704067200);

        using var result = await client.GenerateShortLinkAsync(
            "https://s.shopee.com.br/50VRdFsnBr",
            CreateOptions());

        result.ShortLink.Should().Be("https://s.shopee.com.br/api-link");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.Url.Should().Be("https://example.test/graphql");
        capturedRequest.ContentType.Should().Be("application/json; charset=utf-8");
        capturedRequest.Authorization.Should().MatchRegex("^SHA256 Credential=123, Timestamp=1704067200, Signature=[a-f0-9]{64}$");
        capturedRequest.Body.Should().Be(ShopeeAffiliateClient.BuildGenerateShortLinkPayload(
            "https://s.shopee.com.br/50VRdFsnBr",
            new[] { "telegram" }));
    }

    [Fact]
    public async Task GenerateAffiliateLinkAsync_UsesProductOfferForProductUrls()
    {
        var calls = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            calls.Add(CapturedRequest.From(request));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "data": {
                    "productOfferV2": {
                      "nodes": [
                        {
                          "itemId": 23798776965,
                          "productName": "Cadeira de Escritorio Zinnia Venecia SL",
                          "productLink": "https://shopee.com.br/product/627750190/23798776965",
                          "offerLink": "https://s.shopee.com.br/offer-link",
                          "imageUrl": "https://cf.shopee.com.br/file/image",
                          "priceMin": "334.99",
                          "priceMax": "334.99"
                        }
                      ],
                      "pageInfo": {
                        "page": 1,
                        "limit": 1,
                        "hasNextPage": false
                      }
                    }
                  }
                }
                """)
            };
        }));
        var client = new ShopeeAffiliateClient(httpClient, () => 1704067200);

        var result = await client.GenerateAffiliateLinkAsync(
            "https://shopee.com.br/Cadeira-i.627750190.23798776965",
            CreateOptions(resolveShortUrls: false));

        result.AffiliateUrl.Should().Be("https://s.shopee.com.br/offer-link");
        result.ProductTitle.Should().Be("Cadeira de Escritorio Zinnia Venecia SL");
        result.ProductPrice.Should().Be("R$ 334,99");
        result.ProductImageUrl.Should().Be("https://cf.shopee.com.br/file/image");
        result.FinalProductUrl.Should().Be("https://shopee.com.br/product/627750190/23798776965");
        calls.Should().ContainSingle();
        calls[0].Body.Should().Contain("productOfferV2");
        calls[0].Body.Should().NotContain("generateShortLink");
    }

    [Fact]
    public void ReadSubIdsFromEnvironment_TrimsAndLimitsToFiveEntries()
    {
        ShopeeAffiliateClient.ReadSubIdsFromEnvironment(" telegram, ,bot,one,two,three,four ")
            .Should().Equal("telegram", "bot", "one", "two", "three");
    }

    private static ShopeeAffiliateOptions CreateOptions(bool resolveShortUrls = true)
    {
        return new ShopeeAffiliateOptions
        {
            Endpoint = "https://example.test/graphql",
            AppId = "123",
            Secret = "secret",
            SubIds = new[] { "telegram" },
            ResolveShortUrls = resolveShortUrls
        };
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string Url,
        string? Authorization,
        string? ContentType,
        string Body)
    {
        public static CapturedRequest From(HttpRequestMessage request)
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
            return new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.TryGetValues("Authorization", out var authorization)
                    ? authorization.SingleOrDefault()
                    : null,
                request.Content?.Headers.ContentType?.ToString(),
                body);
        }
    }
}
