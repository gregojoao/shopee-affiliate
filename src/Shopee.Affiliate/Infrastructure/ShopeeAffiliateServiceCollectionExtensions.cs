using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shopee.Affiliate.Application;

namespace Shopee.Affiliate.Infrastructure;

public static class ShopeeAffiliateServiceCollectionExtensions
{
    public const string DefaultConfigurationSectionName = "Shopee:Affiliate";

    public static IServiceCollection AddShopeeAffiliate(
        this IServiceCollection services,
        Action<ShopeeAffiliateOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<ShopeeAffiliateOptions>()
            .Configure(configureOptions)
            .Validate(AreOptionsValid, "Shopee affiliate options are invalid.")
            .ValidateOnStart();

        return services.AddShopeeAffiliateCore();
    }

    public static IServiceCollection AddShopeeAffiliate(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);
        services.AddOptions<ShopeeAffiliateOptions>()
            .Bind(section)
            .Validate(AreOptionsValid, $"Configuration section '{sectionName}' contains invalid Shopee affiliate options.")
            .ValidateOnStart();

        return services.AddShopeeAffiliateCore();
    }

    private static IServiceCollection AddShopeeAffiliateCore(this IServiceCollection services)
    {
        services.AddHttpClient<IShopeeAffiliateClient, ShopeeAffiliateClient>();
        return services;
    }

    private static bool AreOptionsValid(ShopeeAffiliateOptions options)
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
