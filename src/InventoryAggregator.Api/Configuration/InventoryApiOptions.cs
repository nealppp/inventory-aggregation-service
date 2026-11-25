namespace InventoryAggregator.Api.Configuration;

public sealed class InventoryApiOptions
{
    public const string SectionName = "InventoryApi";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    public int PageSize { get; set; } = 25;
}
