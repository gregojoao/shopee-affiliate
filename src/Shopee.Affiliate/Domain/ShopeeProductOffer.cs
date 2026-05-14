namespace Shopee.Affiliate;

public sealed record ShopeeProductOffer(
    string AffiliateUrl,
    string ProductTitle,
    string ProductPrice,
    string ProductOriginalPrice,
    string ProductImageUrl,
    string ProductUrl,
    string ImageUrl,
    string ItemId,
    string? ShopId,
    string PriceMin,
    string PriceMax,
    string PriceDiscountRate);
