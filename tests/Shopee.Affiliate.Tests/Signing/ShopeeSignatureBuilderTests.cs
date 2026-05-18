using FluentAssertions;
using Shopee.Affiliate.Signing;

namespace Shopee.Affiliate.Tests.Signing;

public sealed class ShopeeSignatureBuilderTests
{
    [Fact]
    public void CreateSignature_MatchesKnownVector()
    {
        var payload = "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\" }) { shortLink } }\"}";

        ShopeeSignatureBuilder.CreateSignature("123", 1704067200, payload, "secret")
            .Should().Be("eec3e1f8269df06e7090121d358cfcdc5814efaff9773102031b887657998e01");
    }

    [Fact]
    public void BuildAuthorizationHeader_FormatsShopeeSha256Header()
    {
        var header = ShopeeSignatureBuilder.BuildAuthorizationHeader("acme-app", 1704067200, "{}", "secret");

        header.Should().StartWith("SHA256 Credential=acme-app, Timestamp=1704067200, Signature=");
        header.Should().MatchRegex("^SHA256 Credential=acme-app, Timestamp=1704067200, Signature=[a-f0-9]{64}$");
    }

    [Fact]
    public void BuildAuthorizationHeader_ChangesWhenAnyComponentChanges()
    {
        var baseHeader = ShopeeSignatureBuilder.BuildAuthorizationHeader("a", 1, "{}", "s");
        ShopeeSignatureBuilder.BuildAuthorizationHeader("a", 2, "{}", "s").Should().NotBe(baseHeader);
        ShopeeSignatureBuilder.BuildAuthorizationHeader("a", 1, "{\"q\":1}", "s").Should().NotBe(baseHeader);
        ShopeeSignatureBuilder.BuildAuthorizationHeader("a", 1, "{}", "s2").Should().NotBe(baseHeader);
        ShopeeSignatureBuilder.BuildAuthorizationHeader("b", 1, "{}", "s").Should().NotBe(baseHeader);
    }
}
