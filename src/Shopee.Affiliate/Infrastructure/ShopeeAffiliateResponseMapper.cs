using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Shopee.Affiliate.Domain;

namespace Shopee.Affiliate.Infrastructure;

internal static partial class ShopeeAffiliateResponseMapper
{
    public static Uri ExtractShortLink(JsonElement responseBody)
    {
        ThrowGraphQLErrors(responseBody);

        var shortLink = TryGetProperty(responseBody, out var data, "data") &&
                        TryGetProperty(data, out var generateShortLink, "generateShortLink") &&
                        TryGetProperty(generateShortLink, out var shortLinkElement, "shortLink")
            ? GetStringValue(shortLinkElement)
            : string.Empty;

        if (!TryCreateHttpUri(shortLink, out var shortLinkUri))
        {
            throw new ShopeeAffiliateApiException("Shopee API did not return a valid shortLink.");
        }

        return shortLinkUri;
    }

    public static ShopeeProductOffer? ExtractProductOffer(
        JsonElement responseBody,
        CultureInfo priceCulture)
    {
        ThrowGraphQLErrors(responseBody);

        if (!TryGetProperty(responseBody, out var data, "data") ||
            !TryGetProperty(data, out var productOfferV2, "productOfferV2") ||
            !TryGetProperty(productOfferV2, out var nodes, "nodes") ||
            nodes.ValueKind != JsonValueKind.Array ||
            nodes.GetArrayLength() == 0)
        {
            return null;
        }

        var node = nodes[0];
        var affiliateUrl = GetPropertyString(node, "offerLink");
        var productTitle = NormalizeWhitespace(GetPropertyString(node, "productName"));
        var priceMin = GetPropertyString(node, "priceMin");
        var priceMax = GetPropertyString(node, "priceMax");
        var priceDiscountRate = GetPropertyString(node, "priceDiscountRate");
        var imageUrl = GetPropertyString(node, "imageUrl");
        var productUrl = GetPropertyString(node, "productLink");
        var itemId = GetPropertyString(node, "itemId");
        var validAffiliateUrl = GetValidHttpUriOrNull(affiliateUrl);
        var validImageUrl = GetValidHttpUriOrNull(imageUrl);
        var validProductUrl = GetValidHttpUriOrNull(productUrl);

        string? shopId = null;
        if (ShopeeAffiliateUrlParser.TryExtractProductIdentity(productUrl, out var identity))
        {
            shopId = identity.ShopId;
        }

        return new ShopeeProductOffer(
            AffiliateUrl: validAffiliateUrl,
            ProductTitle: productTitle,
            ProductPrice: ShopeePriceFormatter.FormatShopeePriceRange(priceMin, priceMax, priceCulture),
            ProductOriginalPrice: ShopeePriceFormatter.ComputeShopeeOriginalPrice(priceMin, priceMax, priceDiscountRate, priceCulture),
            ProductImageUrl: validImageUrl,
            ProductUrl: validProductUrl,
            ImageUrl: validImageUrl,
            ItemId: itemId,
            ShopId: shopId,
            PriceMin: priceMin,
            PriceMax: priceMax,
            PriceDiscountRate: priceDiscountRate);
    }

    private static void ThrowGraphQLErrors(JsonElement responseBody)
    {
        if (!TryGetProperty(responseBody, out var errors, "errors") ||
            errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0)
        {
            return;
        }

        var messages = errors
            .EnumerateArray()
            .Select(error =>
                GetPropertyString(error, "message") is { Length: > 0 } message
                    ? message
                    : TryGetProperty(error, out var extensions, "extensions")
                        ? GetPropertyString(extensions, "message")
                        : string.Empty)
            .Where(message => !string.IsNullOrWhiteSpace(message));

        throw new ShopeeAffiliateApiException(
            string.Join("; ", messages.DefaultIfEmpty("Shopee API returned GraphQL errors.")));
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, string name)
    {
        property = default;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out property);
    }

    private static string GetPropertyString(JsonElement element, string name)
    {
        return TryGetProperty(element, out var property, name)
            ? GetStringValue(property)
            : string.Empty;
    }

    private static string GetStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static Uri? GetValidHttpUriOrNull(string value)
        => TryCreateHttpUri(value, out var uri) ? uri : null;

    private static bool TryCreateHttpUri(string? value, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string NormalizeWhitespace(string value)
        => WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
