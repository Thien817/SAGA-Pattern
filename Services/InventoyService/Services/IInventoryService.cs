using InventoryService.DTOs;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync();
    Task<ProductDto?> GetByIdAsync(int productId);
    Task<bool> ReserveAsync(int productId, int quantity);
}