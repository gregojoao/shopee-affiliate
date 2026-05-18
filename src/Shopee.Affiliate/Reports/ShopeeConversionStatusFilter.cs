namespace Shopee.Affiliate.Reports;

/// <summary>
/// Filter accepted by <c>conversionReport.orderStatus</c>. The SDK maps each
/// value to the official Shopee enum (<c>ALL | UNPAID | PENDING | COMPLETED |
/// CANCELLED</c>); the additional <c>Paid</c>, <c>Shipped</c>, <c>Invalid</c>
/// values map to the closest official bucket so client-side code can stay
/// expressive even when Shopee collapses them server-side.
/// </summary>
public enum ShopeeConversionStatusFilter
{
    All = 0,
    Pending,
    Paid,
    Shipped,
    Completed,
    Cancelled,
    Invalid
}
