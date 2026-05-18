namespace Shopee.Affiliate.Reports;

/// <summary>
/// High-level client for the reporting surface of the Shopee Affiliate Open API.
/// </summary>
/// <remarks>
/// <para>
/// All methods sign the request with the same SHA256 scheme used by
/// <c>Shopee.Affiliate.Application.ShopeeAffiliateClient</c>:
/// <c>Authorization: SHA256 Credential={AppId}, Timestamp={ts}, Signature={sha256(AppId+ts+payload+Secret)}</c>.
/// </para>
/// <para>
/// Dates are sent as Unix-seconds; Shopee's Affiliate Open API is anchored in
/// <c>GMT+7</c> (Singapore). The SDK accepts <see cref="DateTimeOffset"/>, which
/// already carries an offset, so the conversion is a straight
/// <c>ToUnixTimeSeconds()</c> and the wall-clock semantics observed by the
/// affiliate dashboard are preserved.
/// </para>
/// </remarks>
public interface IShopeeAffiliateReportsClient
{
    /// <summary>
    /// Lists conversions for the given window via <c>conversionReport</c>.
    /// Pagination is cursor-based; pass <see cref="ShopeeConversionPage.NextCursor"/>
    /// back on subsequent calls.
    /// </summary>
    Task<ShopeeConversionPage> ListConversionsAsync(
        ListShopeeConversionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single conversion that matches <paramref name="orderId"/>.
    /// Backed by <c>conversionReport(orderId: ...)</c>; raises
    /// <see cref="Shopee.Affiliate.Infrastructure.ShopeeAffiliateNotFoundException"/>
    /// when no row is returned.
    /// </summary>
    Task<ShopeeConversionDetail> GetConversionAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates every conversion in the window into a single
    /// <see cref="ShopeeSalesSummary"/>. Click-related metrics are always null;
    /// see <see cref="ShopeeClickStats"/> for the rationale.
    /// </summary>
    Task<ShopeeSalesSummary> GetSalesSummaryAsync(
        ShopeeSalesSummaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns time-bucketed click statistics. Currently always
    /// <c>Supported=false</c> because the Affiliate Open API does not expose a
    /// click endpoint.
    /// </summary>
    Task<ShopeeClickStats> GetClickStatsAsync(
        ShopeeClickStatsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns aggregate link-usage metrics. Currently always
    /// <c>Supported=false</c> because the Affiliate Open API does not expose
    /// link-generation counters.
    /// </summary>
    Task<ShopeeLinkUsage> GetGeneratedLinkUsageAsync(
        ShopeeLinkUsageRequest request,
        CancellationToken cancellationToken = default);
}
