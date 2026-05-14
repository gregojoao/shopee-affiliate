using FluentAssertions;
using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Tests.Domain;

public sealed class ShopeeAffiliateUrlParserTests
{
    [Theory]
    [InlineData("https://shopee.com.br/Cadeira-i.627750190.23798776965?utm_source=test", "627750190", "23798776965")]
    [InlineData("https://shopee.com.br/product/627750190/23798776965", "627750190", "23798776965")]
    [InlineData("https://shopee.com.br/opaanlp/347988064/9212570285?uls_trackid=test", "347988064", "9212570285")]
    [InlineData("https://shopee.com.br/item?shopid=111&itemid=222", "111", "222")]
    [InlineData("https://shopee.com.br/item?shopId=333&itemId=444", "333", "444")]
    public void TryExtractProductIdentity_ReadsProductIdsFromSupportedUrls(
        string url,
        string expectedShopId,
        string expectedItemId)
    {
        var success = ShopeeAffiliateUrlParser.TryExtractProductIdentity(url, out var identity);

        success.Should().BeTrue();
        identity.ShopId.Should().Be(expectedShopId);
        identity.ItemId.Should().Be(expectedItemId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://shopee.com.br/product/1/2")]
    [InlineData("https://shopee.com.br/product/shop/item")]
    [InlineData("https://shopee.com.br/item?shopid=111")]
    public void TryExtractProductIdentity_ReturnsFalseForUnsupportedValues(string value)
    {
        var success = ShopeeAffiliateUrlParser.TryExtractProductIdentity(value, out var identity);

        success.Should().BeFalse();
        identity.ShopId.Should().BeNull();
        identity.ItemId.Should().BeEmpty();
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData(" 123 ", "123")]
    [InlineData("12a3", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeNumericId_ReturnsOnlyTrimmedNumericValues(string? value, string expected)
    {
        ShopeeAffiliateUrlParser.NormalizeNumericId(value).Should().Be(expected);
    }
}
