using InventoryService.Models;

namespace InventoryService.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductRecord?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<bool> ReserveForOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
