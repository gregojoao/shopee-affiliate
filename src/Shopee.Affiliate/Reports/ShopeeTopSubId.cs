namespace Shopee.Affiliate.Reports;

public sealed record ShopeeTopSubId(
    string SubId,
    int Conversions,
    Money Commission);
