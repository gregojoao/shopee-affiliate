namespace Shopee.Affiliate.Domain;

public sealed record ShopeeProductOffer(
    Uri? AffiliateUrl,
    string ProductTitle,
    string ProductPrice,
    string ProductOriginalPrice,
    Uri? ProductImageUrl,
    Uri? ProductUrl,
    Uri? ImageUrl,
    string ItemId,
    string? ShopId,
    string PriceMin,
    string PriceMax,
    string PriceDiscountRate);
