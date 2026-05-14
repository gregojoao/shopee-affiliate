using System.Globalization;

namespace Shopee.Affiliate.Application;

public sealed class ShopeeAffiliateOptions
{
    public static readonly Uri DefaultEndpoint = new("https://open-api.affiliate.shopee.com.br/graphql");
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    public Uri Endpoint { get; set; } = DefaultEndpoint;

    public string AppId { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public IReadOnlyList<string> SubIds { get; set; } = Array.Empty<string>();

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    public CultureInfo PriceCulture { get; set; } = CultureInfo.GetCultureInfo("pt-BR");

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

        if (Endpoint is null ||
            (Endpoint.Scheme != Uri.UriSchemeHttp && Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Shopee affiliate Endpoint must be an absolute HTTP/HTTPS URL.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Shopee affiliate Timeout must be greater than zero.");
        }

        if (PriceCulture is null)
        {
            throw new InvalidOperationException("Shopee affiliate PriceCulture is required.");
        }
    }
}
