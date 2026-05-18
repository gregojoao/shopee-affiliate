using Shopee.Affiliate.Reports;

namespace Shopee.Affiliate.Infrastructure.Reports;

/// <summary>
/// Client-side filter for <see cref="ShopeeConversion"/> rows by Sub Id.
/// </summary>
/// <remarks>
/// The Shopee <c>conversionReport</c> query does not accept a Sub Id argument,
/// so the SDK matches against the per-row <c>SubIds</c> list (parsed from
/// <c>utmContent</c>) after the page is fetched.
/// </remarks>
internal static class ShopeeReportsConversionFilter
{
    public static IReadOnlyList<ShopeeConversion> ApplySubId(
        IReadOnlyList<ShopeeConversion> items,
        string? subId)
    {
        if (string.IsNullOrWhiteSpace(subId)) return items;
        return items
            .Where(c => c.SubIds.Any(s => string.Equals(s, subId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
