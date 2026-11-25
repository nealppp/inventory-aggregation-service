using InventoryAggregator.Api.Models;

namespace InventoryAggregator.Api.Clients;

public interface IInventoryApiClient
{
    Task<IReadOnlyList<InventoryRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default);
}
