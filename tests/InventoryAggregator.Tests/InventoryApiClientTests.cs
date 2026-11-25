using System.Net;
using System.Text;
using System.Text.Json;
using InventoryAggregator.Api.Clients;
using InventoryAggregator.Api.Configuration;
using InventoryAggregator.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InventoryAggregator.Tests;

public class InventoryApiClientTests
{
    [Fact]
    public async Task GetAllRecordsAsync_StitchesMultiplePages_AndDeduplicatesIdenticalRecords()
    {
        var rec1 = new InventoryRecord(1, "Item 1", "CatA", 10m, 2, DateTimeOffset.UtcNow);
        var rec2 = new InventoryRecord(2, "Item 2", "CatA", 20m, 1, DateTimeOffset.UtcNow);

        var handler = new StubHttpMessageHandler(req =>
        {
            var query = req.RequestUri!.Query;
            if (query.Contains("page=1"))
            {
                return JsonResponse(new UpstreamPageResponse([rec1], Page: 1, PageSize: 1, TotalPages: 2, TotalRecords: 2));
            }
            // Page 2 includes duplicate rec1 plus rec2
            return JsonResponse(new UpstreamPageResponse([rec1, rec2], Page: 2, PageSize: 1, TotalPages: 2, TotalRecords: 2));
        });

        var client = CreateClient(handler, pageSize: 1);
        var results = await client.GetAllRecordsAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == 1);
        Assert.Contains(results, r => r.Id == 2);
    }

    [Fact]
    public async Task GetAllRecordsAsync_ThrowsWhenCollectedCountDoesNotMatchTotalRecords()
    {
        var rec1 = new InventoryRecord(1, "Item 1", "CatA", 10m, 2, DateTimeOffset.UtcNow);

        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(new UpstreamPageResponse([rec1], Page: 1, PageSize: 25, TotalPages: 1, TotalRecords: 5)));

        var client = CreateClient(handler, pageSize: 25);

        var ex = await Assert.ThrowsAsync<UpstreamApiException>(() => client.GetAllRecordsAsync());
        Assert.Contains("completeness check failed", ex.Message);
    }

    [Fact]
    public async Task GetAllRecordsAsync_ThrowsOnTruncatedMidPage()
    {
        var rec1 = new InventoryRecord(1, "Item 1", "CatA", 10m, 2, DateTimeOffset.UtcNow);

        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(new UpstreamPageResponse([rec1], Page: 1, PageSize: 5, TotalPages: 2, TotalRecords: 6)));

        var client = CreateClient(handler, pageSize: 5);

        var ex = await Assert.ThrowsAsync<UpstreamApiException>(() => client.GetAllRecordsAsync());
        Assert.Contains("truncated", ex.Message);
    }

    [Fact]
    public async Task GetAllRecordsAsync_RetriesTransient500AndSucceeds()
    {
        int attempts = 0;
        var rec1 = new InventoryRecord(1, "Item 1", "CatA", 10m, 2, DateTimeOffset.UtcNow);

        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            return JsonResponse(new UpstreamPageResponse([rec1], Page: 1, PageSize: 25, TotalPages: 1, TotalRecords: 1));
        });

        var services = new ServiceCollection();
        services.AddHttpClient<IInventoryApiClient, InventoryApiClient>(c =>
            {
                c.BaseAddress = new Uri("https://example.test/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(opts =>
            {
                opts.Retry.MaxRetryAttempts = 3;
                opts.Retry.Delay = TimeSpan.FromMilliseconds(10);
                opts.Retry.UseJitter = false;
            });

        services.AddSingleton<IOptions<InventoryApiOptions>>(
            Options.Create(new InventoryApiOptions { BaseUrl = "https://example.test", PageSize = 25 }));

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IInventoryApiClient>();

        var records = await client.GetAllRecordsAsync();

        Assert.Equal(3, attempts);
        Assert.Single(records);
    }

    private static InventoryApiClient CreateClient(HttpMessageHandler handler, int pageSize = 25)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var opts = Options.Create(new InventoryApiOptions
        {
            BaseUrl = "https://example.test",
            PageSize = pageSize
        });
        return new InventoryApiClient(httpClient, opts, NullLogger<InventoryApiClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
