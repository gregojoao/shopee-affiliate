namespace Shopee.Affiliate.Reports;

/// <summary>
/// Inputs for <see cref="IShopeeAffiliateReportsClient.ListConversionsAsync"/>.
/// </summary>
/// <param name="From">
/// Lower bound (inclusive) of <c>purchaseTime</c>. Converted internally to Unix
/// seconds; the Shopee Affiliate Open API operates in <c>GMT+7</c> (Singapore)
/// wall-clock for display, but the wire format is an absolute epoch so the
/// caller's offset is preserved as-is.
/// </param>
/// <param name="To">Upper bound (inclusive) of <c>purchaseTime</c>.</param>
/// <param name="Status">Optional order-status filter. <c>All</c> when omitted.</param>
/// <param name="SubId">
/// Optional Sub Id to scope the report. <c>conversionReport</c> does not
/// accept a server-side filter for Sub Id, so the SDK applies the filter
/// client-side by matching against <c>subId1..subId5</c> on each row.
/// </param>
/// <param name="Page">
/// 1-based page number. Provided as a convenience: the Affiliate Open API only
/// supports cursor-based pagination via <c>scrollId</c>, so any page greater
/// than 1 triggers internal forward scans until the requested page is reached.
/// Use <paramref name="Cursor"/> for production traversal.
/// </param>
/// <param name="PageSize">Page size. Shopee caps this at 500.</param>
/// <param name="Cursor">
/// Forward cursor previously returned in <see cref="ShopeeConversionPage.NextCursor"/>.
/// Shopee's <c>scrollId</c> expires ~30 seconds after issue.
/// </param>
public sealed record ListShopeeConversionsRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    ShopeeConversionStatusFilter? Status = null,
    string? SubId = null,
    int Page = 1,
    int PageSize = 50,
    string? Cursor = null);
