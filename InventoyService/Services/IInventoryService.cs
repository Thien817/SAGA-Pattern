using InventoryService.DTOs;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
