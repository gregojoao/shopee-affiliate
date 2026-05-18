namespace Shopee.Affiliate.Reports;

/// <summary>
/// A single conversion with its full <c>items[]</c> breakdown. Returned by
/// <see cref="IShopeeAffiliateReportsClient.GetConversionAsync"/>.
/// </summary>
public sealed record ShopeeConversionDetail(
    string ConversionId,
    string OrderId,
    ShopeeOrderStatus Status,
    string? ShopId,
    string? ShopName,
    string? ProductId,
    string? ProductTitle,
    string? ProductImageUrl,
    int Quantity,
    Money ItemPrice,
    Money TotalSale,
    Money Commission,
    decimal CommissionRate,
    IReadOnlyList<string> SubIds,
    DateTimeOffset? ClickTime,
    DateTimeOffset PurchaseTime,
    DateTimeOffset? CompleteTime,
    string? Currency,
    string? RawJson,
    IReadOnlyList<ShopeeOrderLine> Lines)
    : ShopeeConversion(
        ConversionId, OrderId, Status, ShopId, ShopName, ProductId, ProductTitle, ProductImageUrl,
        Quantity, ItemPrice, TotalSale, Commission, CommissionRate, SubIds,
        ClickTime, PurchaseTime, CompleteTime, Currency, RawJson);
