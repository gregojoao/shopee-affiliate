namespace Shopee.Affiliate.Reports;

public sealed record ShopeeClickPoint(
    DateTimeOffset Bucket,
    int Clicks,
    int Conversions,
    Money Commission);
