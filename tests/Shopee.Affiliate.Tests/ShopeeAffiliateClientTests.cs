using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shopee.Affiliate.Application;
using Shopee.Affiliate.Infrastructure;
using System.Globalization;
using System.Net;
using System.Text;

namespace Shopee.Affiliate.Tests;

public sealed class ShopeeAffiliateClientTests
{
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
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        var result = await client.GenerateShortLinkAsync(new ShopeeShortLinkRequest
        {
            OriginUrl = new Uri("https://s.shopee.com.br/50VRdFsnBr")
        });

        result.ShortLink.Should().Be(new Uri("https://s.shopee.com.br/api-link"));
        result.RawResponse.Should().Contain("generateShortLink");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.Url.Should().Be("https://example.test/graphql");
        capturedRequest.ContentType.Should().Be("application/json; charset=utf-8");
        capturedRequest.Authorization.Should().MatchRegex("^SHA256 Credential=123, Timestamp=1704067200, Signature=[a-f0-9]{64}$");
        capturedRequest.Body.Should().Be(
            "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\", subIds: [\\\"telegram\\\"] }) { shortLink } }\"}");
    }

    [Fact]
    public async Task GenerateShortLinkAsync_UsesRequestSubIdsWhenProvided()
    {
        CapturedRequest? capturedRequest = null;
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            capturedRequest = CapturedRequest.From(request);
            return ShortLinkResponse("https://s.shopee.com.br/request-subid");
        }));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        await client.GenerateShortLinkAsync(new ShopeeShortLinkRequest
        {
            OriginUrl = new Uri("https://s.shopee.com.br/50VRdFsnBr"),
            SubIds = new[] { "campaign", "channel" }
        });

        capturedRequest!.Body.Should().Contain("subIds: [\\\"campaign\\\", \\\"channel\\\"]");
        capturedRequest.Body.Should().NotContain("telegram");
    }

    [Fact]
    public async Task GenerateAffiliateLinkAsync_UsesProductOfferForProductUrls()
    {
        var calls = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            calls.Add(CapturedRequest.From(request));
            return ProductOfferResponse("""
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
                  ],
                  "pageInfo": {
                    "page": 1,
                    "limit": 1,
                    "hasNextPage": false
                  }
                }
              }
            }
            """);
        }));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        var result = await client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
        {
            OriginUrl = new Uri("https://shopee.com.br/Cadeira-i.627750190.23798776965"),
            ResolveShortUrls = false
        });

        result.Source.Should().Be(ShopeeAffiliateLinkSource.ProductOffer);
        result.AffiliateUrl.Should().Be(new Uri("https://s.shopee.com.br/offer-link"));
        result.ResolvedOriginUrl.Should().Be(new Uri("https://shopee.com.br/Cadeira-i.627750190.23798776965"));
        result.Product.Should().NotBeNull();
        result.Product!.ProductTitle.Should().Be("Cadeira de Escritorio Zinnia Venecia SL");
        result.Product.ProductPrice.Should().Be("R$ 334,99");
        result.Product.ProductOriginalPrice.Should().Be("R$ 418,74");
        result.Product.ProductImageUrl.Should().Be(new Uri("https://cf.shopee.com.br/file/image"));
        result.Product.ProductUrl.Should().Be(new Uri("https://shopee.com.br/product/627750190/23798776965"));
        calls.Should().ContainSingle();
        calls[0].Body.Should().Contain("productOfferV2");
        calls[0].Body.Should().NotContain("generateShortLink");
    }

    [Fact]
    public async Task GenerateAffiliateLinkAsync_FallsBackToShortLinkWhenPreferredProductOfferFails()
    {
        var calls = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            calls.Add(CapturedRequest.From(request));
            return calls.Count == 1
                ? GraphQlErrorResponse("Invalid product")
                : ShortLinkResponse("https://s.shopee.com.br/fallback-link");
        }));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        var result = await client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
        {
            OriginUrl = new Uri("https://shopee.com.br/Cadeira-i.627750190.23798776965"),
            ResolveShortUrls = false
        });

        result.Source.Should().Be(ShopeeAffiliateLinkSource.ShortLink);
        result.AffiliateUrl.Should().Be(new Uri("https://s.shopee.com.br/fallback-link"));
        calls.Should().HaveCount(2);
        calls[0].Body.Should().Contain("productOfferV2");
        calls[1].Body.Should().Contain("generateShortLink");
    }

    [Fact]
    public async Task GenerateAffiliateLinkAsync_ProductOfferOnlyThrowsWhenOfferLookupFails()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ => GraphQlErrorResponse("Invalid product")));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        var act = () => client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
        {
            OriginUrl = new Uri("https://shopee.com.br/Cadeira-i.627750190.23798776965"),
            ResolveShortUrls = false,
            Strategy = ShopeeAffiliateLinkStrategy.ProductOfferOnly
        });

        await act.Should().ThrowAsync<ShopeeAffiliateApiException>()
            .WithMessage("*Invalid product*");
    }

    [Fact]
    public async Task GenerateAffiliateLinkAsync_ShortLinkOnlySkipsProductOfferLookup()
    {
        var calls = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            calls.Add(CapturedRequest.From(request));
            return ShortLinkResponse("https://s.shopee.com.br/short-only");
        }));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(), () => 1704067200);

        var result = await client.GenerateAffiliateLinkAsync(new ShopeeAffiliateLinkRequest
        {
            OriginUrl = new Uri("https://shopee.com.br/Cadeira-i.627750190.23798776965"),
            ResolveShortUrls = false,
            Strategy = ShopeeAffiliateLinkStrategy.ShortLinkOnly
        });

        result.Source.Should().Be(ShopeeAffiliateLinkSource.ShortLink);
        result.AffiliateUrl.Should().Be(new Uri("https://s.shopee.com.br/short-only"));
        calls.Should().ContainSingle();
        calls[0].Body.Should().Contain("generateShortLink");
        calls[0].Body.Should().NotContain("productOfferV2");
    }

    [Fact]
    public async Task GetProductOfferAsync_ReturnsNormalizedProductOffer()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ => ProductOfferResponse("""
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
              ]
            }
          }
        }
        """)));
        var client = new ShopeeAffiliateClient(httpClient, CreateOptions(priceCulture: CultureInfo.GetCultureInfo("en-US")));

        var productOffer = await client.GetProductOfferAsync(new ShopeeProductOfferRequest
        {
            ProductIdentity = new("627750190", "23798776965")
        });

        productOffer.Should().NotBeNull();
        productOffer!.AffiliateUrl.Should().Be(new Uri("https://s.shopee.com.br/offer-link"));
        productOffer.ProductTitle.Should().Be("Cadeira de Escritorio Zinnia Venecia SL");
        productOffer.ProductPrice.Should().Be("$334.99");
        productOffer.ProductUrl.Should().Be(new Uri("https://shopee.com.br/product/627750190/23798776965"));
        productOffer.ShopId.Should().Be("627750190");
        productOffer.ItemId.Should().Be("23798776965");
    }

    [Fact]
    public void AddShopeeAffiliate_ConfiguresClientAndOptionsWithDelegate()
    {
        var services = new ServiceCollection();

        services.AddShopeeAffiliate(options =>
        {
            options.Endpoint = new Uri("https://example.test/graphql");
            options.AppId = "123";
            options.Secret = "secret";
            options.SubIds = new[] { "telegram", "bot" };
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ShopeeAffiliateOptions>>().Value;

        provider.GetRequiredService<IShopeeAffiliateClient>().Should().BeOfType<ShopeeAffiliateClient>();
        options.Endpoint.Should().Be(new Uri("https://example.test/graphql"));
        options.AppId.Should().Be("123");
        options.Secret.Should().Be("secret");
        options.SubIds.Should().Equal("telegram", "bot");
    }

    [Fact]
    public void AddShopeeAffiliate_BindsOptionsFromConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Shopee:Affiliate:Endpoint"] = "https://example.test/graphql",
                ["Shopee:Affiliate:AppId"] = "123",
                ["Shopee:Affiliate:Secret"] = "secret",
                ["Shopee:Affiliate:SubIds:0"] = "telegram",
                ["Shopee:Affiliate:SubIds:1"] = "bot",
                ["Shopee:Affiliate:Timeout"] = "00:01:30",
                ["Shopee:Affiliate:PriceCulture"] = "en-US"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddShopeeAffiliate(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ShopeeAffiliateOptions>>().Value;

        options.Endpoint.Should().Be(new Uri("https://example.test/graphql"));
        options.AppId.Should().Be("123");
        options.Secret.Should().Be("secret");
        options.SubIds.Should().Equal("telegram", "bot");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(90));
        options.PriceCulture.Name.Should().Be("en-US");
    }

    private static ShopeeAffiliateOptions CreateOptions(CultureInfo? priceCulture = null)
    {
        return new ShopeeAffiliateOptions
        {
            Endpoint = new Uri("https://example.test/graphql"),
            AppId = "123",
            Secret = "secret",
            SubIds = new[] { "telegram" },
            PriceCulture = priceCulture ?? CultureInfo.GetCultureInfo("pt-BR")
        };
    }

    private static HttpResponseMessage ShortLinkResponse(string shortLink)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent($$"""
            {
              "data": {
                "generateShortLink": {
                  "shortLink": "{{shortLink}}"
                }
              }
            }
            """)
        };

    private static HttpResponseMessage ProductOfferResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent(json)
        };

    private static HttpResponseMessage GraphQlErrorResponse(string message)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent($$"""
            {
              "errors": [
                { "message": "{{message}}" }
              ]
            }
            """)
        };

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
