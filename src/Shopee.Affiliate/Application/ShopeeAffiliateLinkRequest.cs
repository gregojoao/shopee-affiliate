namespace Shopee.Affiliate.Application;

public sealed record ShopeeAffiliateLinkRequest
{
    public required Uri OriginUrl { get; init; }

    public IReadOnlyList<string> SubIds { get; init; } = Array.Empty<string>();

    public bool ResolveShortUrls { get; init; } = true;

    public ShopeeAffiliateLinkStrategy Strategy { get; init; } = ShopeeAffiliateLinkStrategy.PreferProductOffer;
}
