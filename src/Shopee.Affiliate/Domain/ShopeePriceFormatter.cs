using System.Globalization;
using System.Text.RegularExpressions;

namespace Shopee.Affiliate.Domain;

internal static class ShopeePriceFormatter
{
    public static string FormatShopeePriceRange(
        string? priceMin,
        string? priceMax,
        CultureInfo priceCulture)
    {
        var min = ParseShopeePrice(priceMin);
        var max = ParseShopeePrice(priceMax);

        if (min is null)
        {
            return string.Empty;
        }

        var formattedMin = FormatCurrency(min.Value, priceCulture);
        if (max is null || Math.Abs(max.Value - min.Value) < 0.005m)
        {
            return formattedMin;
        }

        return $"{formattedMin} - {FormatCurrency(max.Value, priceCulture)}";
    }

    public static string ComputeShopeeOriginalPrice(
        string? priceMin,
        string? priceMax,
        string? priceDiscountRate,
        CultureInfo priceCulture)
    {
        _ = priceMax;

        if (!decimal.TryParse(priceDiscountRate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) ||
            rate <= 0 ||
            rate >= 100)
        {
            return string.Empty;
        }

        var min = ParseShopeePrice(priceMin);
        if (min is null)
        {
            return string.Empty;
        }

        var factor = (100 - rate) / 100;
        return FormatCurrency(min.Value / factor, priceCulture);
    }

    private static decimal? ParseShopeePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var raw = value.Trim();
        var normalized = Regex.Replace(raw, @"[^\d,.]", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (Regex.IsMatch(raw, @"^\d+\.\d+$") &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantDecimal))
        {
            return invariantDecimal;
        }

        normalized = normalized.Replace(".", string.Empty).Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatCurrency(decimal value, CultureInfo culture)
    {
        return value.ToString("C", culture ?? CultureInfo.GetCultureInfo("pt-BR"));
    }
}
