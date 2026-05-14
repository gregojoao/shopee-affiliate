using FluentAssertions;
using Shopee.Affiliate.Application;
using System.Globalization;

namespace Shopee.Affiliate.Tests.Application;

public sealed class ShopeeAffiliateOptionsTests
{
    [Fact]
    public void Validate_AcceptsCompleteOptions()
    {
        var options = new ShopeeAffiliateOptions
        {
            AppId = "123",
            Secret = "secret",
            Endpoint = new Uri("https://example.test/graphql"),
            Timeout = TimeSpan.FromSeconds(30),
            PriceCulture = CultureInfo.GetCultureInfo("pt-BR")
        };

        var act = options.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ThrowsWhenCredentialsAreMissing()
    {
        var options = new ShopeeAffiliateOptions();

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AppId*");
    }

    [Fact]
    public void Validate_ThrowsWhenEndpointIsNotHttp()
    {
        var options = CreateValidOptions();
        options.Endpoint = new Uri("ftp://example.test/graphql");

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Endpoint*");
    }

    [Fact]
    public void Validate_ThrowsWhenTimeoutIsNotPositive()
    {
        var options = CreateValidOptions();
        options.Timeout = TimeSpan.Zero;

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Timeout*");
    }

    private static ShopeeAffiliateOptions CreateValidOptions()
        => new()
        {
            AppId = "123",
            Secret = "secret",
            Endpoint = new Uri("https://example.test/graphql")
        };
}
