using FluentAssertions;
using Shopee.Affiliate.Infrastructure;
using System.Globalization;
using System.Text.Json;

namespace Shopee.Affiliate.Tests.Infrastructure;

public sealed class ShopeeAffiliateResponseMapperTests
{
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

        ShopeeAffiliateResponseMapper.ExtractShortLink(document.RootElement)
            .Should().Be(new Uri("https://s.shopee.com.br/api-link"));
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

        var act = () => ShopeeAffiliateResponseMapper.ExtractShortLink(document.RootElement);

        act.Should().Throw<ShopeeAffiliateApiException>()
            .WithMessage("*Invalid Signature*");
    }

    [Fact]
    public void ExtractProductOffer_ReturnsNullWhenNodesAreMissing()
    {
        using var document = JsonDocument.Parse("""{"data":{"productOfferV2":{"nodes":[]}}}""");

        var offer = ShopeeAffiliateResponseMapper.ExtractProductOffer(
            document.RootElement,
            CultureInfo.GetCultureInfo("pt-BR"));

        offer.Should().BeNull();
    }

    [Fact]
    public void ExtractProductOffer_NormalizesInvalidUrlsToNull()
    {
        using var document = JsonDocument.Parse("""
        {
          "data": {
            "productOfferV2": {
              "nodes": [
                {
                  "itemId": 23798776965,
                  "productName": "Product",
                  "productLink": "not-a-url",
                  "offerLink": "not-a-url",
                  "imageUrl": "not-a-url",
                  "priceMin": "334.99",
                  "priceMax": "334.99"
                }
              ]
            }
          }
        }
        """);

        var offer = ShopeeAffiliateResponseMapper.ExtractProductOffer(
            document.RootElement,
            CultureInfo.GetCultureInfo("pt-BR"));

        offer.Should().NotBeNull();
        offer!.AffiliateUrl.Should().BeNull();
        offer.ProductUrl.Should().BeNull();
        offer.ProductImageUrl.Should().BeNull();
        offer.ImageUrl.Should().BeNull();
    }
}
