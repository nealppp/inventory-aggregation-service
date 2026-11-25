namespace InventoryAggregator.Api.Models;

public sealed record UpstreamPageResponse(
    IReadOnlyList<InventoryRecord>? Records,
    int Page,
    int PageSize,
    int TotalPages,
    int TotalRecords);

public sealed record InventoryRecord(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Quantity,
    DateTimeOffset LastUpdated);
