namespace Shopee.Affiliate;

public sealed class ShopeeAffiliateOptions
{
    public const string DefaultEndpoint = "https://open-api.affiliate.shopee.com.br/graphql";
    public const int DefaultTimeoutMilliseconds = 90_000;

    public string Endpoint { get; set; } = DefaultEndpoint;

    public string AppId { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public IReadOnlyList<string> SubIds { get; set; } = Array.Empty<string>();

    public int TimeoutMilliseconds { get; set; } = DefaultTimeoutMilliseconds;

    public bool ResolveShortUrls { get; set; } = true;

    public bool PreferProductOffer { get; set; } = true;

    public bool FallbackToShortLink { get; set; } = true;

    public string PriceCultureName { get; set; } = "pt-BR";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppId))
        {
            throw new InvalidOperationException("Shopee affiliate AppId is required.");
        }

        if (string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException("Shopee affiliate Secret is required.");
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Shopee affiliate Endpoint must be an absolute HTTP/HTTPS URL.");
        }
    }
}
