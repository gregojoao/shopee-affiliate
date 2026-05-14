using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shopee.Affiliate;

internal static class ShopeeAffiliateResponseMapper
{
    public static string ExtractShortLink(JsonElement responseBody)
    {
        ThrowGraphQLErrors(responseBody);

        var shortLink = TryGetProperty(responseBody, out var data, "data") &&
                        TryGetProperty(data, out var generateShortLink, "generateShortLink") &&
                        TryGetProperty(generateShortLink, out var shortLinkElement, "shortLink")
            ? GetStringValue(shortLinkElement)
            : string.Empty;

        if (!ShopeeAffiliateUrlParser.IsValidHttpUrl(shortLink))
        {
            throw new ShopeeAffiliateApiException("Shopee API did not return a valid shortLink.");
        }

        return shortLink;
    }

    public static ShopeeProductOffer? ExtractProductOffer(
        JsonElement responseBody,
        string priceCultureName = "pt-BR")
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

        string? shopId = null;
        if (ShopeeAffiliateUrlParser.TryExtractProductIdentity(productUrl, out var identity))
        {
            shopId = identity.ShopId;
        }

        return new ShopeeProductOffer(
            AffiliateUrl: ShopeeAffiliateUrlParser.IsValidHttpUrl(affiliateUrl) ? affiliateUrl : string.Empty,
            ProductTitle: productTitle,
            ProductPrice: ShopeePriceFormatter.FormatShopeePriceRange(priceMin, priceMax, priceCultureName),
            ProductOriginalPrice: ShopeePriceFormatter.ComputeShopeeOriginalPrice(priceMin, priceMax, priceDiscountRate, priceCultureName),
            ProductImageUrl: ShopeeAffiliateUrlParser.IsValidHttpUrl(imageUrl) ? imageUrl : string.Empty,
            ProductUrl: ShopeeAffiliateUrlParser.IsValidHttpUrl(productUrl) ? productUrl : string.Empty,
            ImageUrl: ShopeeAffiliateUrlParser.IsValidHttpUrl(imageUrl) ? imageUrl : string.Empty,
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

    private static string NormalizeWhitespace(string value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
