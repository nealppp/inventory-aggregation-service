using InventoryAggregator.Api.Models;

namespace InventoryAggregator.Api.Services;

public sealed class InventoryAggregator : IInventoryAggregator
{
    public AggregatedInventoryResponse Aggregate(IEnumerable<InventoryRecord> records)
    {
        var categories = records
            .Where(r => r.Quantity > 0)
            .GroupBy(r => r.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedItems = group
                    .OrderBy(r => r.Id)
                    .Select(r => new CategoryItem(r.Id, r.Name, r.Price, r.Quantity, r.LastUpdated))
                    .ToList();

                var mostRecent = group
                    .OrderByDescending(r => r.LastUpdated)
                    .ThenByDescending(r => r.Id)
                    .First();

                return new CategorySummary(
                    Category: group.First().Category.Trim(),
                    TotalInventoryValue: group.Sum(r => r.Price * r.Quantity),
                    AvailableRecordCount: orderedItems.Count,
                    MostRecentlyUpdated: new RecentRecordSummary(
                        mostRecent.Id,
                        mostRecent.Name,
                        mostRecent.LastUpdated),
                    Items: orderedItems);
            })
            .ToList();

        return new AggregatedInventoryResponse(categories);
    }
}
