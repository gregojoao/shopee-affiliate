using Shopee.Affiliate.Application;
using Shopee.Affiliate.Infrastructure;
using Shopee.Affiliate.Reports;
using System.Globalization;

// ---------------------------------------------------------------------------
//  Live smoke-test for the Shopee.Affiliate SDK.
//  Reads credentials from env vars; NEVER writes them to disk or stdout.
//
//  Required env vars:
//    SHOPEE_AFFILIATE_APP_ID
//    SHOPEE_AFFILIATE_SECRET
//
//  Optional:
//    SHOPEE_AFFILIATE_TRACKING_ID   (used to scope reports by SubId)
//    SHOPEE_SMOKE_DAYS              (defaults to 30)
// ---------------------------------------------------------------------------

var appId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_APP_ID");
var secret = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_SECRET");
var trackingId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_TRACKING_ID");
var days = int.TryParse(Environment.GetEnvironmentVariable("SHOPEE_SMOKE_DAYS"), out var d) ? d : 30;

if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret))
{
    Console.Error.WriteLine("Set SHOPEE_AFFILIATE_APP_ID and SHOPEE_AFFILIATE_SECRET first.");
    return 1;
}

Console.WriteLine($"AppId:    {Mask(appId!)}");
Console.WriteLine($"SubId:    {trackingId ?? "(none)"}");
Console.WriteLine($"Window:   last {days} day(s)");
Console.WriteLine();

var to = DateTimeOffset.UtcNow;
var from = to.AddDays(-days);

// Introspection short-circuit: SHOPEE_SMOKE_MODE=introspect prints the
// conversionReport schema and exits.
if (string.Equals(Environment.GetEnvironmentVariable("SHOPEE_SMOKE_MODE"), "introspect", StringComparison.OrdinalIgnoreCase))
{
    await Introspect(appId!, secret!);
    return 0;
}

using var httpClient = new HttpClient();
var reportsOptions = new ShopeeAffiliateReportsOptions
{
    AppId = appId!,
    Secret = secret!,
    Timeout = TimeSpan.FromSeconds(30)
};
var reports = new ShopeeAffiliateReportsClient(httpClient, reportsOptions);

// 1) ListConversionsAsync (first page only)
await RunSafelyAsync("ListConversionsAsync", async () =>
{
    var page = await reports.ListConversionsAsync(new ListShopeeConversionsRequest(
        From: from,
        To: to,
        Status: ShopeeConversionStatusFilter.All,
        SubId: trackingId,
        PageSize: 20));

    Console.WriteLine($"  items: {page.Items.Count}");
    Console.WriteLine($"  hasMore: {page.HasMore}");
    Console.WriteLine($"  nextCursor: {(string.IsNullOrEmpty(page.NextCursor) ? "(none)" : "<set>")}");
    foreach (var c in page.Items.Take(5))
    {
        Console.WriteLine(
            $"    order={c.OrderId,-16} status={c.Status,-10} " +
            $"sale={Fmt(c.TotalSale)} comm={Fmt(c.Commission)} " +
            $"product={Trunc(c.ProductTitle, 40)}");
    }
    if (page.Items.Count > 5) Console.WriteLine($"    ... and {page.Items.Count - 5} more");
});

// 2) GetSalesSummaryAsync (full aggregation)
await RunSafelyAsync("GetSalesSummaryAsync", async () =>
{
    var summary = await reports.GetSalesSummaryAsync(new ShopeeSalesSummaryRequest(from, to, SubId: trackingId));
    Console.WriteLine($"  supported:        {summary.Supported}");
    Console.WriteLine($"  conversions:      {summary.Conversions}");
    Console.WriteLine($"  grossRevenue:     {Fmt(summary.GrossRevenue)}");
    Console.WriteLine($"  commission:       {Fmt(summary.Commission)}");
    Console.WriteLine($"  avgCommissionPct: {summary.AvgCommissionRate * 100m:0.00}%");
    Console.WriteLine($"  byStatus:         {string.Join(", ", summary.ByStatus.Select(kv => $"{kv.Key}={kv.Value}"))}");
    if (summary.TopProducts.Count > 0)
    {
        Console.WriteLine("  topProducts:");
        foreach (var p in summary.TopProducts.Take(3))
        {
            Console.WriteLine($"    {p.ProductId,-14} comm={Fmt(p.Commission)} convs={p.Conversions} title={Trunc(p.ProductTitle, 40)}");
        }
    }
    if (summary.TopSubIds.Count > 0)
    {
        Console.WriteLine("  topSubIds:");
        foreach (var s in summary.TopSubIds.Take(3))
        {
            Console.WriteLine($"    {s.SubId,-20} comm={Fmt(s.Commission)} convs={s.Conversions}");
        }
    }
});

// 3) GetClickStatsAsync (should always be Supported=false)
await RunSafelyAsync("GetClickStatsAsync (expected Supported=false)", async () =>
{
    var clicks = await reports.GetClickStatsAsync(new ShopeeClickStatsRequest(
        From: from, To: to, Granularity: ShopeeReportGranularity.Day, SubId: trackingId));
    Console.WriteLine($"  supported:         {clicks.Supported}");
    Console.WriteLine($"  unsupportedReason: {clicks.UnsupportedReason}");
});

// 4) GetGeneratedLinkUsageAsync (should always be Supported=false)
await RunSafelyAsync("GetGeneratedLinkUsageAsync (expected Supported=false)", async () =>
{
    var usage = await reports.GetGeneratedLinkUsageAsync(new ShopeeLinkUsageRequest(from, to, SubId: trackingId));
    Console.WriteLine($"  supported:         {usage.Supported}");
    Console.WriteLine($"  unsupportedReason: {usage.UnsupportedReason}");
});

// 5) Auth negative path — call with a deliberately wrong secret
await RunSafelyAsync("Negative: wrong secret should throw ShopeeAffiliateAuthException", async () =>
{
    var badClient = new ShopeeAffiliateReportsClient(httpClient, new ShopeeAffiliateReportsOptions
    {
        AppId = appId!,
        Secret = "deliberately-wrong-secret-for-negative-test",
        Timeout = TimeSpan.FromSeconds(15)
    });

    try
    {
        await badClient.ListConversionsAsync(new ListShopeeConversionsRequest(from, to, PageSize: 1));
        Console.WriteLine("  UNEXPECTED: bad secret was accepted by the API.");
    }
    catch (ShopeeAffiliateAuthException ex)
    {
        Console.WriteLine($"  ✓ ShopeeAffiliateAuthException — code={ex.Code} message=\"{ex.Message}\"");
    }
    catch (ShopeeAffiliateException ex)
    {
        Console.WriteLine($"  Got {ex.GetType().Name} (code={ex.Code}) instead of AuthException — still informative.");
    }
});

// 6) Sanity: the link client still works (regression guard)
await RunSafelyAsync("Link client regression: GenerateShortLinkAsync", async () =>
{
    var linkOptions = new ShopeeAffiliateOptions
    {
        AppId = appId!,
        Secret = secret!,
        SubIds = string.IsNullOrEmpty(trackingId) ? Array.Empty<string>() : new[] { trackingId }
    };
    var linkClient = new ShopeeAffiliateClient(httpClient, linkOptions);
    var result = await linkClient.GenerateShortLinkAsync(new ShopeeShortLinkRequest
    {
        OriginUrl = new Uri("https://shopee.com.br/")
    });
    Console.WriteLine($"  shortLink: {result.ShortLink}");
});

return 0;

static async Task RunSafelyAsync(string name, Func<Task> action)
{
    Console.WriteLine($"=== {name} ===");
    try
    {
        await action();
    }
    catch (ShopeeAffiliateException ex)
    {
        Console.Error.WriteLine($"  ✗ {ex.GetType().Name} — code={ex.Code} message=\"{ex.Message}\"");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  ✗ {ex.GetType().Name} — {ex.Message}");
    }
    Console.WriteLine();
}

static string Mask(string value)
{
    if (string.IsNullOrEmpty(value)) return string.Empty;
    if (value.Length <= 4) return new string('*', value.Length);
    return $"{value[..2]}***{value[^2..]}";
}

static string Fmt(Money money)
    => $"{money.Currency} {money.Amount.ToString("0.00", CultureInfo.InvariantCulture)}";

static string Trunc(string? value, int max)
    => string.IsNullOrEmpty(value)
        ? "(null)"
        : value.Length <= max ? value : value[..max] + "…";

static async Task Introspect(string appId, string secret)
{
    // Quick schema-probe helper. Set SHOPEE_SMOKE_TYPES="Type1,Type2" to dump
    // specific GraphQL types, or leave unset to dump the conversionReport args.
    using var http = new HttpClient();
    var typesEnv = Environment.GetEnvironmentVariable("SHOPEE_SMOKE_TYPES");
    if (!string.IsNullOrWhiteSpace(typesEnv))
    {
        foreach (var typeName in typesEnv.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            await DumpType(http, appId, secret, typeName.Trim());
        }
        return;
    }
    await DumpConversionReportArgs(http, appId, secret);
}

static async Task DumpType(HttpClient http, string appId, string secret, string typeName)
{
    var query = "{ __type(name: \"" + typeName + "\") { name kind enumValues { name } fields { name type { kind name ofType { kind name ofType { kind name } } } } } }";
    await PostAndPrint(http, appId, secret, query, $"--- {typeName} ---");
}

static async Task DumpConversionReportArgs(HttpClient http, string appId, string secret)
{
    var query = "{ __schema { queryType { fields { name args { name type { kind name ofType { kind name ofType { kind name } } } } } } } }";
    var body = System.Text.Json.JsonSerializer.Serialize(new { query });
    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var hashInput = System.Text.Encoding.UTF8.GetBytes($"{appId}{ts}{body}{secret}");
    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(hashInput)).ToLowerInvariant();
    using var req = new HttpRequestMessage(HttpMethod.Post, "https://open-api.affiliate.shopee.com.br/graphql");
    req.Headers.TryAddWithoutValidation("Authorization", $"SHA256 Credential={appId}, Timestamp={ts}, Signature={hash}");
    req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    var resp = await http.SendAsync(req);
    var text = await resp.Content.ReadAsStringAsync();
    using var doc = System.Text.Json.JsonDocument.Parse(text);
    foreach (var q in doc.RootElement.GetProperty("data").GetProperty("__schema").GetProperty("queryType").GetProperty("fields").EnumerateArray())
    {
        if (q.GetProperty("name").GetString() == "conversionReport")
        {
            Console.WriteLine("--- conversionReport args ---");
            Console.WriteLine(q.GetRawText());
        }
    }
}

static async Task PostAndPrint(HttpClient http, string appId, string secret, string query, string heading)
{
    var body = System.Text.Json.JsonSerializer.Serialize(new { query });
    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var hashInput = System.Text.Encoding.UTF8.GetBytes($"{appId}{ts}{body}{secret}");
    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(hashInput)).ToLowerInvariant();
    using var req = new HttpRequestMessage(HttpMethod.Post, "https://open-api.affiliate.shopee.com.br/graphql");
    req.Headers.TryAddWithoutValidation("Authorization", $"SHA256 Credential={appId}, Timestamp={ts}, Signature={hash}");
    req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    var resp = await http.SendAsync(req);
    Console.WriteLine(heading);
    Console.WriteLine(await resp.Content.ReadAsStringAsync());
    Console.WriteLine();
}
