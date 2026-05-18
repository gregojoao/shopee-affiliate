using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shopee.Affiliate.Infrastructure;
using Shopee.Affiliate.Reports;
using System.Globalization;
using System.Net;
using System.Text;

namespace Shopee.Affiliate.Tests.Reports;

public sealed class ShopeeAffiliateReportsClientTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 17, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListConversionsAsync_BuildsCorrectAuthHeaderAndUsesUnixSeconds()
    {
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => OkJson(SingleNodePayload(orderId: "OD-1", purchaseTime: 1716000000)));

        var page = await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero)));

        page.Items.Should().HaveCount(1);
        page.Items[0].OrderId.Should().Be("OD-1");
        capture.Last!.Method.Should().Be(HttpMethod.Post);
        capture.Last.Authorization.Should().MatchRegex("^SHA256 Credential=app-123, Timestamp=1779013800, Signature=[a-f0-9]{64}$");
        capture.Last.Body.Should().Contain("purchaseTimeStart: 1777593600");
        capture.Last.Body.Should().Contain("purchaseTimeEnd: 1780271999");
        capture.Last.Body.Should().Contain("conversionReport");
    }

    [Fact]
    public async Task ListConversionsAsync_TranslatesGmtPlus7CallerOffsetIntoAbsoluteEpoch()
    {
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => OkJson(SingleNodePayload()));

        var gmt7 = TimeSpan.FromHours(7);
        await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: new DateTimeOffset(2026, 5, 1, 7, 0, 0, gmt7),
            To: new DateTimeOffset(2026, 5, 2, 7, 0, 0, gmt7)));

        capture.Last!.Body.Should().Contain("purchaseTimeStart: 1777593600");
        capture.Last.Body.Should().Contain("purchaseTimeEnd: 1777680000");
    }

    [Fact]
    public async Task ListConversionsAsync_PaginatesViaScrollIdAcrossTwoPages()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            OkJson(SingleNodePayload(orderId: "OD-1", hasNextPage: true, scrollId: "cursor-2")),
            OkJson(SingleNodePayload(orderId: "OD-2", hasNextPage: false, scrollId: null))
        });
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => responses.Dequeue());

        var firstPage = await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1),
            To: FixedNow));
        firstPage.HasMore.Should().BeTrue();
        firstPage.NextCursor.Should().Be("cursor-2");
        firstPage.Items[0].OrderId.Should().Be("OD-1");

        var secondPage = await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1),
            To: FixedNow,
            Cursor: firstPage.NextCursor));
        secondPage.HasMore.Should().BeFalse();
        secondPage.Items[0].OrderId.Should().Be("OD-2");

        capture.All.Should().HaveCount(2);
        capture.All[1].Body.Should().Contain("scrollId: \\\"cursor-2\\\"");
    }

    [Fact]
    public async Task GetSalesSummaryAsync_AggregatesAcrossPagesIncludingTopProductsAndSubIds()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            OkJson(MultiNodePayload(new[]
            {
                new SyntheticConversion("OD-1", ProductId: "100", ProductTitle: "Cadeira", ShopId: "500", ShopName: "Shop 1", TotalCommission: 10m, SubIds: new[] { "tg", "promo" }, Status: "PENDING"),
                new SyntheticConversion("OD-2", ProductId: "100", ProductTitle: "Cadeira", ShopId: "500", ShopName: "Shop 1", TotalCommission: 15m, SubIds: new[] { "tg" }, Status: "COMPLETED")
            }, hasNextPage: true, scrollId: "next-1")),
            OkJson(MultiNodePayload(new[]
            {
                new SyntheticConversion("OD-3", ProductId: "200", ProductTitle: "Mesa", ShopId: "600", ShopName: "Shop 2", TotalCommission: 30m, SubIds: new[] { "promo" }, Status: "COMPLETED")
            }, hasNextPage: false, scrollId: null))
        });
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => responses.Dequeue());

        var summary = await client.GetSalesSummaryAsync(new ShopeeSalesSummaryRequest(
            From: FixedNow.AddDays(-7),
            To: FixedNow));

        summary.Supported.Should().BeTrue();
        summary.Conversions.Should().Be(3);
        summary.Commission.Amount.Should().Be(55m);
        summary.Commission.Currency.Should().Be("BRL");
        summary.ByStatus[ShopeeOrderStatus.Pending].Should().Be(1);
        summary.ByStatus[ShopeeOrderStatus.Completed].Should().Be(2);
        summary.TopProducts.Should().HaveCountGreaterThan(0);
        summary.TopProducts[0].ProductId.Should().Be("200");
        summary.TopShops[0].ShopId.Should().Be("600");
        summary.TopSubIds.Select(s => s.SubId).Should().Contain(new[] { "tg", "promo" });
        summary.Clicks.Should().BeNull();
        summary.ConversionRate.Should().BeNull();
        capture.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClickStatsAsync_AlwaysReturnsUnsupportedSentinel_HourAndDay()
    {
        var client = CreateClient(new RequestCapture(), _ => OkJson("{}"));

        foreach (var granularity in new[] { ShopeeReportGranularity.Day, ShopeeReportGranularity.Hour })
        {
            var stats = await client.GetClickStatsAsync(new ShopeeClickStatsRequest(
                From: FixedNow.AddDays(-1), To: FixedNow, Granularity: granularity));

            stats.Supported.Should().BeFalse();
            stats.Points.Should().BeEmpty();
            stats.Granularity.Should().Be(granularity);
            stats.UnsupportedReason.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetGeneratedLinkUsageAsync_ReturnsSupportedFalseWithoutThrowing()
    {
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => OkJson("{}"));

        var usage = await client.GetGeneratedLinkUsageAsync(new ShopeeLinkUsageRequest(
            From: FixedNow.AddDays(-1), To: FixedNow));

        usage.Supported.Should().BeFalse();
        usage.LinksGenerated.Should().Be(0);
        usage.CommissionAttributed.Amount.Should().Be(0m);
        capture.All.Should().BeEmpty();
    }

    [Theory]
    [InlineData("10020", typeof(ShopeeAffiliateAuthException))]
    [InlineData("10031", typeof(ShopeeAffiliateAuthException))]
    [InlineData("10035", typeof(ShopeeAffiliateAuthException))]
    [InlineData("10030", typeof(ShopeeAffiliateRateLimitException))]
    [InlineData("11000", typeof(ShopeeAffiliateApiException))]
    [InlineData("11001", typeof(ShopeeAffiliateApiException))]
    public async Task GraphQlErrorCodes_AreTranslatedToTypedExceptions(string code, Type exceptionType)
    {
        var client = CreateClient(new RequestCapture(), _ => OkJson($$"""
        {
          "errors": [
            {
              "message": "boom",
              "extensions": { "code": "{{code}}" },
              "path": ["conversionReport"]
            }
          ]
        }
        """));

        var act = () => client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1), To: FixedNow));

        var exception = (await act.Should().ThrowAsync<ShopeeAffiliateException>()).Which;
        exception.Should().BeOfType(exceptionType);
        exception.Code.Should().Be(code);
        exception.Path.Should().Equal("conversionReport");
    }

    [Fact]
    public async Task Http429_BecomesRateLimitExceptionAndIsNotRetried()
    {
        var calls = 0;
        var client = CreateClient(new RequestCapture(), _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(string.Empty) };
        });

        var act = () => client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1), To: FixedNow));

        await act.Should().ThrowAsync<ShopeeAffiliateRateLimitException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Http500_IsRetriedOnceThenSurfaced()
    {
        var calls = 0;
        var client = CreateClient(new RequestCapture(), _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };
        });

        var act = () => client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1), To: FixedNow));

        await act.Should().ThrowAsync<ShopeeAffiliateApiException>();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetConversionAsync_ThrowsNotFoundWhenNoNodes()
    {
        var client = CreateClient(new RequestCapture(), _ => OkJson("""
        { "data": { "conversionReport": { "nodes": [], "pageInfo": { "hasNextPage": false } } } }
        """));

        var act = () => client.GetConversionAsync("MISSING-ORDER");

        await act.Should().ThrowAsync<ShopeeAffiliateNotFoundException>();
    }

    [Fact]
    public async Task GetConversionAsync_ReturnsDetailWithOrderLines()
    {
        var client = CreateClient(new RequestCapture(), _ => OkJson(SingleNodePayload(
            orderId: "OD-DETAIL",
            includeMultipleItems: true)));

        var detail = await client.GetConversionAsync("OD-DETAIL");

        detail.OrderId.Should().Be("OD-DETAIL");
        detail.Lines.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task SecretAndSignatureNeverAppearInLoggedScopes()
    {
        var captured = new List<string>();
        var logger = new CapturingLogger(captured);
        var capture = new RequestCapture();
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") }
                : OkJson(SingleNodePayload());
        });
        var httpClient = new HttpClient(new RequestCaptureHandler(capture, handler));
        var client = new ShopeeAffiliateReportsClient(
            httpClient,
            CreateOptions(),
            () => FixedNow,
            logger);

        await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1), To: FixedNow));

        captured.Should().NotBeEmpty();
        foreach (var line in captured)
        {
            line.Should().NotContain("super-secret");
            line.Should().NotContain("SHA256 Credential=");
        }
    }

    [Fact]
    public async Task ListConversionsAsync_StatusFilter_MapsToShopeeEnum()
    {
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => OkJson(SingleNodePayload()));

        await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1),
            To: FixedNow,
            Status: ShopeeConversionStatusFilter.Completed));

        capture.Last!.Body.Should().Contain("orderStatus: COMPLETED");
    }

    [Fact]
    public async Task ListConversionsAsync_DoesNotIncludeOptionalVariablesWhenAbsent()
    {
        var capture = new RequestCapture();
        var client = CreateClient(capture, _ => OkJson(SingleNodePayload()));

        await client.ListConversionsAsync(new ListShopeeConversionsRequest(
            From: FixedNow.AddDays(-1),
            To: FixedNow));

        capture.Last!.Body.Should().NotContain("subId:");
        capture.Last.Body.Should().NotContain("scrollId:");
    }

    private static ShopeeAffiliateReportsClient CreateClient(
        RequestCapture capture,
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new RequestCaptureHandler(capture, new DelegateHandler(handler)));
        return new ShopeeAffiliateReportsClient(
            httpClient,
            CreateOptions(),
            clock: () => FixedNow,
            logger: NullLogger<ShopeeAffiliateReportsClient>.Instance);
    }

    private static ShopeeAffiliateReportsOptions CreateOptions()
    {
        return new ShopeeAffiliateReportsOptions
        {
            Endpoint = new Uri("https://example.test/graphql"),
            AppId = "app-123",
            Secret = "super-secret",
            Timeout = TimeSpan.FromSeconds(2)
        };
    }

    private static HttpResponseMessage OkJson(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string SingleNodePayload(
        string orderId = "OD-001",
        long purchaseTime = 1716000000L,
        long? clickTime = 1715999000L,
        long? completeTime = null,
        bool hasNextPage = false,
        string? scrollId = null,
        bool includeMultipleItems = false)
    {
        var completePart = completeTime is null ? "null" : completeTime.Value.ToString();
        var items = includeMultipleItems
            ? $$"""
              { "itemId": 1001, "itemName": "Cadeira",  "imageUrl": "https://cf.shopee.com.br/file/img-1", "shopId": 501, "shopName": "Shop 1", "itemPrice": "100.00", "actualAmount": "100.00", "qty": 1, "itemTotalCommission": "7.00", "refundAmount": "0.00", "attributionType": "wide", "completeTime": {{completePart}} },
              { "itemId": 1002, "itemName": "Almofada", "imageUrl": "https://cf.shopee.com.br/file/img-2", "shopId": 501, "shopName": "Shop 1", "itemPrice": "50.00",  "actualAmount": "100.00", "qty": 2, "itemTotalCommission": "5.00", "refundAmount": "0.00", "attributionType": "wide", "completeTime": {{completePart}} }
              """
            : $$"""
              { "itemId": 1001, "itemName": "Cadeira", "imageUrl": "https://cf.shopee.com.br/file/img-1", "shopId": 501, "shopName": "Shop 1", "itemPrice": "100.00", "actualAmount": "100.00", "qty": 1, "itemTotalCommission": "7.00", "refundAmount": "0.00", "attributionType": "wide", "completeTime": {{completePart}} }
              """;
        var clickPart = clickTime is null ? "null" : clickTime.Value.ToString();
        var scrollPart = scrollId is null ? "null" : $"\"{scrollId}\"";
        var hasNext = hasNextPage ? "true" : "false";

        return $$"""
        {
          "data": {
            "conversionReport": {
              "nodes": [
                {
                  "conversionId": 9000,
                  "conversionStatus": "PENDING",
                  "purchaseTime": {{purchaseTime}},
                  "clickTime": {{clickPart}},
                  "totalCommission": "7.00",
                  "netCommission": "7.00",
                  "utmContent": "tg_promo",
                  "orders": [
                    {
                      "orderId": "{{orderId}}",
                      "orderStatus": "PENDING",
                      "items": [ {{items}} ]
                    }
                  ]
                }
              ],
              "pageInfo": { "page": 1, "limit": 50, "hasNextPage": {{hasNext}}, "scrollId": {{scrollPart}} }
            }
          }
        }
        """;
    }

    private sealed record SyntheticConversion(
        string OrderId,
        string ProductId,
        string ProductTitle,
        string ShopId,
        string ShopName,
        decimal TotalCommission,
        string[] SubIds,
        string Status);

    private static string MultiNodePayload(IEnumerable<SyntheticConversion> conversions, bool hasNextPage, string? scrollId)
    {
        var nodes = new StringBuilder();
        var i = 0;
        var conversionSeed = 9000;
        foreach (var c in conversions)
        {
            if (i > 0) nodes.Append(',');
            var utm = string.Join("_", c.SubIds);
            nodes.Append($$"""
            {
              "conversionId": {{conversionSeed + i}},
              "conversionStatus": "{{c.Status}}",
              "purchaseTime": 1716000000,
              "clickTime": 1715999000,
              "totalCommission": "{{c.TotalCommission.ToString(CultureInfo.InvariantCulture)}}",
              "netCommission": "{{c.TotalCommission.ToString(CultureInfo.InvariantCulture)}}",
              "utmContent": "{{utm}}",
              "orders": [
                {
                  "orderId": "{{c.OrderId}}",
                  "orderStatus": "{{c.Status}}",
                  "items": [
                    { "itemId": {{c.ProductId}}, "itemName": "{{c.ProductTitle}}", "imageUrl": "https://cf.shopee.com.br/img", "shopId": {{c.ShopId}}, "shopName": "{{c.ShopName}}", "itemPrice": "100.00", "actualAmount": "100.00", "qty": 1, "itemTotalCommission": "{{c.TotalCommission.ToString(CultureInfo.InvariantCulture)}}", "refundAmount": "0.00", "attributionType": "wide", "completeTime": null }
                  ]
                }
              ]
            }
            """);
            i++;
        }

        var scrollPart = scrollId is null ? "null" : $"\"{scrollId}\"";
        var hasNext = hasNextPage ? "true" : "false";

        return $$"""
        {
          "data": {
            "conversionReport": {
              "nodes": [ {{nodes}} ],
              "pageInfo": { "page": 1, "limit": 500, "hasNextPage": {{hasNext}}, "scrollId": {{scrollPart}} }
            }
          }
        }
        """;
    }

    private sealed class RequestCapture
    {
        public List<CapturedRequest> All { get; } = new();
        public CapturedRequest? Last => All.LastOrDefault();
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, string? Authorization, string Body);

    private sealed class RequestCaptureHandler : DelegatingHandler
    {
        private readonly RequestCapture _capture;
        public RequestCaptureHandler(RequestCapture capture, HttpMessageHandler inner)
        {
            _capture = capture;
            InnerHandler = inner;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            _capture.All.Add(new CapturedRequest(
                Method: request.Method,
                Url: request.RequestUri?.ToString() ?? string.Empty,
                Authorization: request.Headers.TryGetValues("Authorization", out var auth) ? auth.SingleOrDefault() : null,
                Body: body));
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class CapturingLogger : ILogger<ShopeeAffiliateReportsClient>
    {
        private readonly List<string> _output;
        public CapturingLogger(List<string> output) => _output = output;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _output.Add(formatter(state, exception));
        private sealed class NoopScope : IDisposable { public void Dispose() { } }
    }
}
