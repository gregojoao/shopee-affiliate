using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shopee.Affiliate.Reports;

namespace Shopee.Affiliate.Infrastructure;

/// <summary>
/// DI extensions for <see cref="ShopeeAffiliateReportsClient"/>. Registers a
/// named HttpClient (<c>ShopeeAffiliateReports</c>) so callers can tune
/// resilience and headers independently of the link-generation client.
/// </summary>
public static class ShopeeAffiliateReportsServiceCollectionExtensions
{
    public const string DefaultConfigurationSectionName = "Shopee:Affiliate:Reports";
    public const string HttpClientName = "ShopeeAffiliateReports";

    public static IServiceCollection AddShopeeAffiliateReports(
        this IServiceCollection services,
        Action<ShopeeAffiliateReportsOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<ShopeeAffiliateReportsOptions>()
            .Configure(configureOptions)
            .Validate(AreOptionsValid, "Shopee affiliate reports options are invalid.")
            .ValidateOnStart();

        return services.AddShopeeAffiliateReportsCore();
    }

    public static IServiceCollection AddShopeeAffiliateReports(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);
        services.AddOptions<ShopeeAffiliateReportsOptions>()
            .Bind(section)
            .Validate(AreOptionsValid, $"Configuration section '{sectionName}' contains invalid Shopee affiliate reports options.")
            .ValidateOnStart();

        return services.AddShopeeAffiliateReportsCore();
    }

    private static IServiceCollection AddShopeeAffiliateReportsCore(this IServiceCollection services)
    {
        services.AddHttpClient<IShopeeAffiliateReportsClient, ShopeeAffiliateReportsClient>(HttpClientName);
        return services;
    }

    private static bool AreOptionsValid(ShopeeAffiliateReportsOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
