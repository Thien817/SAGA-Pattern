using InventoryService.Models;

namespace InventoryService.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<ProductRecord>> GetProductsAsync();
    Task<ProductRecord?> GetByIdAsync(int productId);
    Task<bool> ReserveStockAsync(int productId, int quantity);
}