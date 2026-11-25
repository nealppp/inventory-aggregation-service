using InventoryAggregator.Api.Models;
using AggregatorService = InventoryAggregator.Api.Services.InventoryAggregator;

namespace InventoryAggregator.Tests;

public class InventoryAggregatorTests
{
    private readonly AggregatorService _sut = new();

    [Fact]
    public void Aggregate_ExcludesZeroQuantityRecords_AndOmitsEmptyCategories()
    {
        var records = new[]
        {
            new InventoryRecord(1, "Printer", "Electronics", 100.00m, 2, DateTimeOffset.Parse("2026-10-15T12:00:00Z")),
            new InventoryRecord(2, "Out of Stock Cable", "Electronics", 15.00m, 0, DateTimeOffset.Parse("2026-10-16T12:00:00Z")),
            new InventoryRecord(3, "Empty Category Item", "Furniture", 250.00m, 0, DateTimeOffset.Parse("2026-10-16T12:00:00Z"))
        };

        var result = _sut.Aggregate(records);

        var category = Assert.Single(result.Categories);
        Assert.Equal("Electronics", category.Category);
        Assert.Equal(200.00m, category.TotalInventoryValue);
        Assert.Equal(1, category.AvailableRecordCount);
        Assert.Single(category.Items);
        Assert.Equal(1, category.Items[0].Id);
    }

    [Fact]
    public void Aggregate_ComputesTotalValueAndMostRecentRecord_WithDeterministicTieBreaker()
    {
        var sharedTimestamp = DateTimeOffset.Parse("2026-10-15T12:30:00Z");
        var records = new[]
        {
            new InventoryRecord(101, "Scanner A", "Electronics", 50.50m, 3, sharedTimestamp),
            new InventoryRecord(105, "Scanner B", "Electronics", 10.00m, 5, sharedTimestamp),
            new InventoryRecord(99, "Older Monitor", "Electronics", 200.00m, 1, sharedTimestamp.AddHours(-2))
        };

        var result = _sut.Aggregate(records);

        var category = Assert.Single(result.Categories);
        Assert.Equal(401.50m, category.TotalInventoryValue);
        Assert.Equal(3, category.AvailableRecordCount);
        Assert.Equal(105, category.MostRecentlyUpdated.Id);
        Assert.Equal("Scanner B", category.MostRecentlyUpdated.Name);
    }

    [Fact]
    public void Aggregate_GroupsCategoriesCaseInsensitively()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            new InventoryRecord(1, "Pen", "Office Supplies", 2.00m, 10, now),
            new InventoryRecord(2, "Notebook", "office supplies ", 5.00m, 4, now)
        };

        var result = _sut.Aggregate(records);

        var category = Assert.Single(result.Categories);
        Assert.Equal(40.00m, category.TotalInventoryValue);
        Assert.Equal(2, category.AvailableRecordCount);
    }
}
