using FluentAssertions;
using Shopee.Affiliate.Infrastructure;

namespace Shopee.Affiliate.Tests.Infrastructure;

public sealed class ShopeeAffiliateAuthenticatorTests
{
    [Fact]
    public void CreateSignature_UsesSha256OverAppIdTimestampPayloadAndSecret()
    {
        var payload = "{\"query\":\"mutation { generateShortLink(input: { originUrl: \\\"https://s.shopee.com.br/50VRdFsnBr\\\" }) { shortLink } }\"}";

        ShopeeAffiliateAuthenticator.CreateSignature("123", 1704067200, payload, "secret")
            .Should().Be("eec3e1f8269df06e7090121d358cfcdc5814efaff9773102031b887657998e01");
    }

    [Fact]
    public void BuildAuthorizationHeader_FormatsShopeeSha256Header()
    {
        var header = ShopeeAffiliateAuthenticator.BuildAuthorizationHeader("123", 1704067200, "{}", "secret");

        header.Should().MatchRegex("^SHA256 Credential=123, Timestamp=1704067200, Signature=[a-f0-9]{64}$");
    }
}
