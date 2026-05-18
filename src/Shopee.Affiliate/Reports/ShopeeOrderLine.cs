namespace Shopee.Affiliate.Reports;

/// <summary>
/// One <c>items[]</c> row inside an <c>orders[]</c> node of <c>conversionReport</c>.
/// </summary>
public sealed record ShopeeOrderLine(
    string? ItemId,
    string? ItemName,
    string? ShopId,
    string? ShopName,
    int Quantity,
    Money ItemPrice,
    Money ItemCommission,
    Money RefundAmount,
    string? AttributionType);
