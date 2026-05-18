namespace Shopee.Affiliate.Reports;

public sealed record ShopeeTopShop(
    string? ShopId,
    string? ShopName,
    int Conversions,
    Money Commission);
