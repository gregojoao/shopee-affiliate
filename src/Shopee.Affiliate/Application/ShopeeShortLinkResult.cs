namespace Shopee.Affiliate.Application;

public sealed record ShopeeShortLinkResult
{
    public required Uri ShortLink { get; init; }

    public string? RawResponse { get; init; }
}
