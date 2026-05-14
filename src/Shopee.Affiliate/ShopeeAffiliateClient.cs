using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shopee.Affiliate;

public sealed class ShopeeAffiliateClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly HttpClient _httpClient;
    private readonly Func<long> _nowSeconds;

    public ShopeeAffiliateClient(HttpClient httpClient, Func<long>? nowSeconds = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _nowSeconds = nowSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async Task<ShopeeAffiliateLinkResult> GenerateAffiliateLinkAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        if (!IsValidHttpUrl(originUrl))
        {
            throw new ArgumentException("A valid Shopee origin URL is required.", nameof(originUrl));
        }

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var token = timeoutCts.Token;
        var resolvedUrl = options.ResolveShortUrls
            ? await ResolveShopeeUrlAsync(originUrl, options, token)
            : originUrl;

        ShopeeProductOffer? productOffer = null;
        var productIdentity =
            TryExtractProductIdentity(resolvedUrl, out var resolvedIdentity) ? resolvedIdentity :
            TryExtractProductIdentity(originUrl, out var originIdentity) ? originIdentity :
            null;

        if (options.PreferProductOffer && productIdentity is not null)
        {
            try
            {
                using var productOfferResult = await GetProductOfferAsync(productIdentity, options, token);
                productOffer = productOfferResult.ProductOffer;

                if (!string.IsNullOrWhiteSpace(productOffer?.AffiliateUrl))
                {
                    return CreateAffiliateResult(productOffer.AffiliateUrl, originUrl, resolvedUrl, productOffer);
                }
            }
            catch when (options.FallbackToShortLink)
            {
                productOffer = null;
            }
        }

        if (!options.FallbackToShortLink)
        {
            throw new ShopeeAffiliateApiException("Shopee API did not return a product offer link.");
        }

        using var shortLinkResult = await GenerateShortLinkAsync(originUrl, options, token);
        return CreateAffiliateResult(shortLinkResult.ShortLink, originUrl, resolvedUrl, productOffer);
    }

    public async Task<ShopeeShortLinkResult> GenerateShortLinkAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        if (!IsValidHttpUrl(originUrl))
        {
            throw new ArgumentException("A valid Shopee origin URL is required.", nameof(originUrl));
        }

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var payload = BuildGenerateShortLinkPayload(originUrl, options.SubIds);
        var responseBody = await PostGraphQLAsync(payload, options, timeoutCts.Token);
        var shortLink = ExtractShortLink(responseBody.RootElement);

        return new ShopeeShortLinkResult(shortLink, payload, responseBody);
    }

    public async Task<ShopeeProductOfferResult> GetProductOfferAsync(
        ShopeeAffiliateProductIdentity productIdentity,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        var payload = BuildProductOfferPayload(productIdentity);
        var responseBody = await PostGraphQLAsync(payload, options, timeoutCts.Token);
        var productOffer = ExtractProductOffer(responseBody.RootElement, options.PriceCultureName);

        return new ShopeeProductOfferResult(productOffer, payload, responseBody);
    }

    public async Task<string> ResolveShopeeUrlAsync(
        string originUrl,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidHttpUrl(originUrl))
        {
            throw new ArgumentException("A valid Shopee origin URL is required.", nameof(originUrl));
        }

        using var timeoutCts = CreateTimeoutTokenSource(options, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, originUrl);
        request.Headers.UserAgent.ParseAdd("Shopee.Affiliate/0.1");

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return response.RequestMessage?.RequestUri?.ToString() is { Length: > 0 } finalUrl &&
                   IsValidHttpUrl(finalUrl)
                ? finalUrl
                : originUrl;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee URL resolve timed out after {options.TimeoutMilliseconds}ms.");
        }
        catch
        {
            return originUrl;
        }
    }

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

        var itemId = NormalizeNumericId(productIdentity.ItemId);
        var shopId = NormalizeNumericId(productIdentity.ShopId);

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

    public static string BuildAuthorizationHeader(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        var signature = CreateSignature(appId, timestamp, payload, secret);
        return $"SHA256 Credential={appId}, Timestamp={timestamp}, Signature={signature}";
    }

    public static string CreateSignature(
        string appId,
        long timestamp,
        string payload,
        string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{appId}{timestamp}{payload}{secret}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ExtractShortLink(JsonElement responseBody)
    {
        ThrowGraphQLErrors(responseBody);

        var shortLink = TryGetProperty(responseBody, out var data, "data") &&
                        TryGetProperty(data, out var generateShortLink, "generateShortLink") &&
                        TryGetProperty(generateShortLink, out var shortLinkElement, "shortLink")
            ? GetStringValue(shortLinkElement)
            : string.Empty;

        if (!IsValidHttpUrl(shortLink))
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
        if (TryExtractProductIdentity(productUrl, out var identity))
        {
            shopId = identity.ShopId;
        }

        return new ShopeeProductOffer(
            AffiliateUrl: IsValidHttpUrl(affiliateUrl) ? affiliateUrl : string.Empty,
            ProductTitle: productTitle,
            ProductPrice: FormatShopeePriceRange(priceMin, priceMax, priceCultureName),
            ProductOriginalPrice: ComputeShopeeOriginalPrice(priceMin, priceMax, priceDiscountRate, priceCultureName),
            ProductImageUrl: IsValidHttpUrl(imageUrl) ? imageUrl : string.Empty,
            ProductUrl: IsValidHttpUrl(productUrl) ? productUrl : string.Empty,
            ImageUrl: IsValidHttpUrl(imageUrl) ? imageUrl : string.Empty,
            ItemId: itemId,
            ShopId: shopId,
            PriceMin: priceMin,
            PriceMax: priceMax,
            PriceDiscountRate: priceDiscountRate);
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

    public static IReadOnlyList<string> ReadSubIdsFromEnvironment(string? value)
        => SanitizeSubIds((value ?? string.Empty).Split(',').Select(item => item.Trim()));

    public static string FormatShopeePriceRange(
        string? priceMin,
        string? priceMax,
        string priceCultureName = "pt-BR")
    {
        var min = ParseShopeePrice(priceMin);
        var max = ParseShopeePrice(priceMax);

        if (min is null)
        {
            return string.Empty;
        }

        var formattedMin = FormatCurrency(min.Value, priceCultureName);
        if (max is null || Math.Abs(max.Value - min.Value) < 0.005m)
        {
            return formattedMin;
        }

        return $"{formattedMin} - {FormatCurrency(max.Value, priceCultureName)}";
    }

    public static string ComputeShopeeOriginalPrice(
        string? priceMin,
        string? priceMax,
        string? priceDiscountRate,
        string priceCultureName = "pt-BR")
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
        return FormatCurrency(min.Value / factor, priceCultureName);
    }

    private async Task<JsonDocument> PostGraphQLAsync(
        string payload,
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken)
    {
        var timestamp = _nowSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            BuildAuthorizationHeader(options.AppId, timestamp, payload, options.Secret));
        request.Headers.UserAgent.ParseAdd("Shopee.Affiliate/0.1");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);

            JsonDocument responseBody;
            try
            {
                responseBody = string.IsNullOrWhiteSpace(text)
                    ? JsonDocument.Parse("{}")
                    : JsonDocument.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new ShopeeAffiliateApiException(
                    $"Shopee API returned non-JSON response: {Truncate(text, 500)}",
                    ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                using (responseBody)
                {
                    throw new ShopeeAffiliateApiException(
                        $"Shopee API HTTP {(int)response.StatusCode}: {Truncate(text, 1000)}");
                }
            }

            return responseBody;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateApiException($"Shopee API request timed out after {options.TimeoutMilliseconds}ms.");
        }
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(
        ShopeeAffiliateOptions options,
        CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(options.TimeoutMilliseconds, 1)));
        return timeoutCts;
    }

    private static ShopeeAffiliateLinkResult CreateAffiliateResult(
        string affiliateUrl,
        string originUrl,
        string resolvedUrl,
        ShopeeProductOffer? productOffer)
    {
        return new ShopeeAffiliateLinkResult(
            AffiliateUrl: affiliateUrl,
            ShortLink: affiliateUrl,
            ProductTitle: productOffer?.ProductTitle ?? string.Empty,
            ProductPrice: productOffer?.ProductPrice ?? string.Empty,
            ProductOriginalPrice: productOffer?.ProductOriginalPrice ?? string.Empty,
            ProductImageUrl: productOffer?.ProductImageUrl ?? productOffer?.ImageUrl ?? string.Empty,
            ProductUrl: productOffer?.ProductUrl ?? resolvedUrl ?? originUrl,
            FinalProductUrl: productOffer?.ProductUrl ?? resolvedUrl ?? originUrl,
            Platform: "Shopee",
            ResolvedUrl: resolvedUrl ?? string.Empty,
            ProductOffer: productOffer);
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

    private static List<string> SanitizeSubIds(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Take(5)
            .ToList();
    }

    private static string NormalizeNumericId(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return Regex.IsMatch(normalized, @"^\d+$") ? normalized : string.Empty;
    }

    private static string GraphQLString(string value)
        => JsonSerializer.Serialize(value ?? string.Empty, JsonOptions);

    private static bool IsValidHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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

    private static string FormatCurrency(decimal value, string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(
            string.IsNullOrWhiteSpace(cultureName) ? "pt-BR" : cultureName);
        return value.ToString("C", culture);
    }

    private static string NormalizeWhitespace(string value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

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

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
