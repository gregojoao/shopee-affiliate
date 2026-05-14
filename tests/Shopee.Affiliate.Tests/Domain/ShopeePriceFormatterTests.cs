using System.Globalization;
using FluentAssertions;
using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Tests.Domain;

public sealed class ShopeePriceFormatterTests
{
    [Fact]
    public void FormatShopeePriceRange_FormatsSinglePricesAndRanges()
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        ShopeePriceFormatter.FormatShopeePriceRange("334.99", "334.99", culture).Should().Be("R$ 334,99");
        ShopeePriceFormatter.FormatShopeePriceRange("1000", "1299.9", culture).Should().Be("R$ 1.000,00 - R$ 1.299,90");
        ShopeePriceFormatter.FormatShopeePriceRange("", "", culture).Should().BeEmpty();
    }

    [Theory]
    [InlineData("R$ 1.234,56", "R$ 1.234,56")]
    [InlineData("1234.56", "R$ 1.234,56")]
    [InlineData("invalid", "")]
    public void FormatShopeePriceRange_ParsesCommonShopeePriceFormats(string price, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        ShopeePriceFormatter.FormatShopeePriceRange(price, price, culture).Should().Be(expected);
    }

    [Fact]
    public void ComputeShopeeOriginalPrice_ReturnsEstimatedOriginalPrice()
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        ShopeePriceFormatter.ComputeShopeeOriginalPrice("334.99", "334.99", "20", culture)
            .Should().Be("R$ 418,74");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("not-a-number")]
    public void ComputeShopeeOriginalPrice_ReturnsEmptyWhenDiscountIsInvalid(string? discountRate)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        ShopeePriceFormatter.ComputeShopeeOriginalPrice("334.99", "334.99", discountRate, culture)
            .Should().BeEmpty();
    }
}
