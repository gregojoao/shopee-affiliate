using System.Text.RegularExpressions;

namespace Shopee.Affiliate.Domain;

internal static class ShopeeAffiliateUrlParser
{
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
        var pathPatterns = new[]
        {
            @"/product/(?<shopId>\d+)/(?<itemId>\d+)(?:/|$)",
            @"/opaanlp/(?<shopId>\d+)/(?<itemId>\d+)(?:/|$)",
            @"(?:^|[-/])i\.(?<shopId>\d+)\.(?<itemId>\d+)$"
        };

        foreach (var pattern in pathPatterns)
        {
            var match = Regex.Match(decodedPath, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                productIdentity = new ShopeeAffiliateProductIdentity(
                    match.Groups["shopId"].Value,
                    match.Groups["itemId"].Value);
                return true;
            }
        }

        var query = ParseQuery(uri.Query);
        var queryShopId = FirstNonEmpty(
            query.GetValueOrDefault("shopid"),
            query.GetValueOrDefault("shopId"));
        var queryItemId = FirstNonEmpty(
            query.GetValueOrDefault("itemid"),
            query.GetValueOrDefault("itemId"));

        if (!string.IsNullOrWhiteSpace(NormalizeNumericId(queryItemId)))
        {
            productIdentity = new ShopeeAffiliateProductIdentity(
                NormalizeNumericId(queryShopId),
                NormalizeNumericId(queryItemId));
            return true;
        }

        return false;
    }

    public static string NormalizeNumericId(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return Regex.IsMatch(normalized, @"^\d+$") ? normalized : string.Empty;
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
}
