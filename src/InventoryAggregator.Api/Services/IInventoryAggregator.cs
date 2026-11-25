using InventoryAggregator.Api.Models;

namespace InventoryAggregator.Api.Services;

public interface IInventoryAggregator
{
    AggregatedInventoryResponse Aggregate(IEnumerable<InventoryRecord> records);
}
