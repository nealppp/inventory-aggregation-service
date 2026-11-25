namespace InventoryAggregator.Api.Models;

public sealed record AggregatedInventoryResponse(
    IReadOnlyList<CategorySummary> Categories);

public sealed record CategorySummary(
    string Category,
    decimal TotalInventoryValue,
    int AvailableRecordCount,
    RecentRecordSummary MostRecentlyUpdated,
    IReadOnlyList<CategoryItem> Items);

public sealed record RecentRecordSummary(
    int Id,
    string Name,
    DateTimeOffset LastUpdated);

public sealed record CategoryItem(
    int Id,
    string Name,
    decimal Price,
    int Quantity,
    DateTimeOffset LastUpdated);
