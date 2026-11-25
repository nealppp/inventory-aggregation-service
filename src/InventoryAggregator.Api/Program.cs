using InventoryAggregator.Api.Clients;
using InventoryAggregator.Api.Configuration;
using InventoryAggregator.Api.Services;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<InventoryApiOptions>(
    builder.Configuration.GetSection(InventoryApiOptions.SectionName));

builder.Services.AddSingleton<IInventoryAggregator, InventoryAggregator.Api.Services.InventoryAggregator>();

builder.Services.AddHttpClient<IInventoryApiClient, InventoryApiClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<InventoryApiOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        }

        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            client.DefaultRequestHeaders.Add(opts.ApiKeyHeaderName, opts.ApiKey);
        }
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.UseJitter = true;
    });

var app = builder.Build();

app.MapGet("/api/inventory/aggregated", async (
    IInventoryApiClient apiClient,
    IInventoryAggregator aggregator,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        var records = await apiClient.GetAllRecordsAsync(ct);
        var result = aggregator.Aggregate(records);
        return Results.Ok(result);
    }
    catch (UpstreamApiException ex)
    {
        logger.LogError(ex, "Failed to aggregate inventory due to upstream API issue.");
        return Results.Problem(
            title: "Upstream Inventory API Error",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();

public partial class Program { }
