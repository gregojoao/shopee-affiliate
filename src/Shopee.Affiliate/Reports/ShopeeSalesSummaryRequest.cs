namespace Shopee.Affiliate.Reports;

/// <summary>
/// Inputs for <see cref="IShopeeAffiliateReportsClient.GetSalesSummaryAsync"/>.
/// The summary is aggregated client-side by pulling every page of the
/// underlying <c>conversionReport</c>; keep the window tight on chatty AppIds.
/// </summary>
/// <param name="From">Lower bound (inclusive) of <c>purchaseTime</c>.</param>
/// <param name="To">
/// Upper bound (inclusive) of <c>purchaseTime</c>. Shopee enforces a maximum
/// window of ~90 days between <see cref="From"/> and <see cref="To"/>; wider
/// ranges are rejected with error code <c>11001</c>
/// (<c>"Params Error : can only query data for the last 3 months"</c>),
/// surfaced as <see cref="Shopee.Affiliate.Infrastructure.ShopeeAffiliateApiException"/>.
/// </param>
/// <param name="SubId">Optional Sub Id filter applied client-side against <c>utmContent</c>.</param>
public sealed record ShopeeSalesSummaryRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? SubId = null);
