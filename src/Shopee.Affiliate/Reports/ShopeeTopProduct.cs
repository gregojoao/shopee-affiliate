namespace Shopee.Affiliate.Reports;

public sealed record ShopeeTopProduct(
    string? ProductId,
    string? ProductTitle,
    int Conversions,
    Money Commission);
