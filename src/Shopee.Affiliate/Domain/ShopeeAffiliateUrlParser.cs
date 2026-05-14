using System.Text.RegularExpressions;

namespace Shopee.Affiliate.Domain;

internal static partial class ShopeeAffiliateUrlParser
{
    private static readonly Regex[] ProductPathPatterns =
    [
        ProductPathRegex(),
        OpenApiPathRegex(),
        LegacyProductPathRegex()
    ];

    public static bool IsValidHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static bool TryExtractProductIdentity(
        string value,
        out ShopeeAffiliateProductIdentity productIdentity)
    {
        productIdentity = new ShopeeAffiliateProductIdentity(null, string.Empty);

        if (!IsValidHttpUrl(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var decodedPath = Uri.UnescapeDataString(uri.AbsolutePath);
        if (TryExtractFromPath(decodedPath, out productIdentity))
        {
            return true;
        }

        return TryExtractFromQuery(uri.Query, out productIdentity);
    }

    public static string NormalizeNumericId(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return NumericIdRegex().IsMatch(normalized) ? normalized : string.Empty;
    }

    private static bool TryExtractFromPath(
        string decodedPath,
        out ShopeeAffiliateProductIdentity productIdentity)
    {
        foreach (var pattern in ProductPathPatterns)
        {
            var match = pattern.Match(decodedPath);
            if (match.Success)
            {
                productIdentity = new ShopeeAffiliateProductIdentity(
                    match.Groups["shopId"].Value,
                    match.Groups["itemId"].Value);
                return true;
            }
        }

        productIdentity = new ShopeeAffiliateProductIdentity(null, string.Empty);
        return false;
    }

    private static bool TryExtractFromQuery(
        string uriQuery,
        out ShopeeAffiliateProductIdentity productIdentity)
    {
        var query = ParseQuery(uriQuery);
        var queryShopId = FirstNonEmpty(
            query.GetValueOrDefault("shopid"),
            query.GetValueOrDefault("shopId"));
        var queryItemId = FirstNonEmpty(
            query.GetValueOrDefault("itemid"),
            query.GetValueOrDefault("itemId"));
        var itemId = NormalizeNumericId(queryItemId);

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            productIdentity = new ShopeeAffiliateProductIdentity(
                NormalizeNumericId(queryShopId),
                itemId);
            return true;
        }

        productIdentity = new ShopeeAffiliateProductIdentity(null, string.Empty);
        return false;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => Uri.UnescapeDataString(parts[0]))
            .ToDictionary(
                group => group.Key,
                group => Uri.UnescapeDataString(group.First()[1]),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    [GeneratedRegex(@"/product/(?<shopId>\d+)/(?<itemId>\d+)(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ProductPathRegex();

    [GeneratedRegex(@"/opaanlp/(?<shopId>\d+)/(?<itemId>\d+)(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex OpenApiPathRegex();

    [GeneratedRegex(@"(?:^|[-/])i\.(?<shopId>\d+)\.(?<itemId>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyProductPathRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericIdRegex();
}
