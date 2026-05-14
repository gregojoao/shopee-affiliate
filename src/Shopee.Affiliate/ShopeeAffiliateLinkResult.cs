namespace Shopee.Affiliate;

public sealed record ShopeeAffiliateLinkResult(
    string AffiliateUrl,
    string ShortLink,
    string ProductTitle,
    string ProductPrice,
    string ProductOriginalPrice,
    string ProductImageUrl,
    string ProductUrl,
    string FinalProductUrl,
    string Platform,
    string ResolvedUrl,
    ShopeeProductOffer? ProductOffer);
