using FluentAssertions;
using Shopee.Affiliate.Domain;
using Shopee.Affiliate.Infrastructure;

namespace Shopee.Affiliate.Tests.Infrastructure;

public sealed class ShopeeAffiliateGraphQlPayloadFactoryTests
{
    [Fact]
    public void BuildGenerateShortLinkPayload_EscapesUrlAndSubIds()
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildGenerateShortLinkPayload(
            "https://s.shopee.com.br/50VRdFsnBr?name=\"chair\"",
            new[] { "telegram", "bot" });

        payload.Should().Be(
            "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr?name=\\\\\\\"chair\\\\\\\"\\\", subIds: [\\\"telegram\\\", \\\"bot\\\"] }) { shortLink } }\"}");
    }

    [Fact]
    public void BuildGenerateShortLinkPayload_OmitsEmptySubIds()
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildGenerateShortLinkPayload(
            "https://s.shopee.com.br/50VRdFsnBr",
            new[] { "", "   " });

        payload.Should().Be(
            "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\" }) { shortLink } }\"}");
    }

    [Fact]
    public void BuildProductOfferPayload_RequestsProductMetadataAndOfferLink()
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(
            new ShopeeAffiliateProductIdentity("627750190", "23798776965"));

        payload.Should().Be(
            "{\"query\":\"{ productOfferV2(shopId: 627750190, itemId: 23798776965, limit: 1) { nodes { itemId productName productLink offerLink imageUrl priceMin priceMax priceDiscountRate } pageInfo { page limit hasNextPage } } }\"}");
    }

    [Fact]
    public void BuildProductOfferPayload_AllowsItemOnlyLookup()
    {
        var payload = ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(
            new ShopeeAffiliateProductIdentity(null, "23798776965"));

        payload.Should().Contain("productOfferV2(itemId: 23798776965, limit: 1)");
    }

    [Fact]
    public void BuildProductOfferPayload_ThrowsWhenItemIdIsInvalid()
    {
        var act = () => ShopeeAffiliateGraphQlPayloadFactory.BuildProductOfferPayload(
            new ShopeeAffiliateProductIdentity("627750190", "not-numeric"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*itemId*");
    }

    [Fact]
    public void ReadSubIdsFromEnvironment_TrimsAndLimitsToFiveEntries()
    {
        ShopeeAffiliateGraphQlPayloadFactory.ReadSubIdsFromEnvironment(" telegram, ,bot,one,two,three,four ")
            .Should().Equal("telegram", "bot", "one", "two", "three");
    }
}
