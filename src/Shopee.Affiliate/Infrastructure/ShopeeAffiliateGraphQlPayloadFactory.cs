using Shopee.Affiliate.Domain;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shopee.Affiliate.Infrastructure;

internal static class ShopeeAffiliateGraphQlPayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string BuildGenerateShortLinkPayload(
        string originUrl,
        IEnumerable<string>? subIds = null)
    {
        var sanitizedSubIds = SanitizeSubIds(subIds);
        var subIdsInput = sanitizedSubIds.Count > 0
            ? $", subIds: [{string.Join(", ", sanitizedSubIds.Select(GraphQLString))}]"
            : string.Empty;

        var query = $"mutation {{ generateShortLink(input: {{ originUrl: {GraphQLString(originUrl)}{subIdsInput} }}) {{ shortLink }} }}";
        return JsonSerializer.Serialize(new { query }, JsonOptions);
    }

    public static string BuildProductOfferPayload(ShopeeAffiliateProductIdentity productIdentity)
    {
        ArgumentNullException.ThrowIfNull(productIdentity);

        var itemId = ShopeeAffiliateUrlParser.NormalizeNumericId(productIdentity.ItemId);
        var shopId = ShopeeAffiliateUrlParser.NormalizeNumericId(productIdentity.ShopId);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("A valid Shopee itemId is required.", nameof(productIdentity));
        }

        var filterInput = !string.IsNullOrWhiteSpace(shopId)
            ? $"shopId: {shopId}, itemId: {itemId}, "
            : $"itemId: {itemId}, ";

        var query = "{ productOfferV2(" +
                    filterInput +
                    "limit: 1) { nodes { itemId productName productLink offerLink imageUrl priceMin priceMax priceDiscountRate } pageInfo { page limit hasNextPage } } }";

        return JsonSerializer.Serialize(new { query }, JsonOptions);
    }

    public static IReadOnlyList<string> ReadSubIdsFromEnvironment(string? value)
        => SanitizeSubIds((value ?? string.Empty).Split(',').Select(item => item.Trim()));

    private static List<string> SanitizeSubIds(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Take(5)
            .ToList();
    }

    private static string GraphQLString(string value)
        => JsonSerializer.Serialize(value ?? string.Empty, JsonOptions);
}
