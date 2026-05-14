using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shopee.Affiliate;

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
            .Validate(options =>
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
            }, "Shopee affiliate options are invalid.")
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
            .Validate(options =>
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
            }, $"Configuration section '{sectionName}' contains invalid Shopee affiliate options.")
            .ValidateOnStart();

        return services.AddShopeeAffiliateCore();
    }

    private static IServiceCollection AddShopeeAffiliateCore(this IServiceCollection services)
    {
        services.AddHttpClient<ShopeeAffiliateClient>();
        services.AddTransient<IShopeeAffiliateService, ShopeeAffiliateService>();
        return services;
    }
}
