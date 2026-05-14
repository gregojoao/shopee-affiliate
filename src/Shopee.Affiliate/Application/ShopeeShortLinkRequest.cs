namespace Shopee.Affiliate.Application;

public sealed record ShopeeShortLinkRequest
{
    public required Uri OriginUrl { get; init; }

    public IReadOnlyList<string> SubIds { get; init; } = Array.Empty<string>();
}
