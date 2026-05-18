namespace Shopee.Affiliate.Reports;

/// <summary>
/// Normalized Shopee order status returned by <c>conversionReport</c>.
/// </summary>
/// <remarks>
/// Shopee's public order-status enum is documented as
/// <c>UNPAID | PENDING | COMPLETED | CANCELLED</c>. The SDK exposes a broader
/// enum so callers can model the wider e-commerce lifecycle (shipped, refunded)
/// when Shopee surfaces those states through <c>orderStatus</c> in future
/// schema revisions. Unknown values fall back to <see cref="Unknown"/>.
/// See <see href="https://www.affiliateshopee.com.br/documentacao"/>.
/// </remarks>
public enum ShopeeOrderStatus
{
    Unknown = 0,
    Pending,
    Paid,
    Shipped,
    Completed,
    Cancelled,
    Invalid
}
