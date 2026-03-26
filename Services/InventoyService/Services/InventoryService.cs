using InventoryService.DTOs;
using InventoryService.Repositories;

namespace InventoryService.Services;

public sealed class InventoryService(IInventoryRepository repo)
    : IInventoryService
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync()
    {
        var data = await repo.GetProductsAsync();

        return data.Select(x =>
            new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock)
        ).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int productId)
    {
        var x = await repo.GetByIdAsync(productId);
        if (x is null) return null;

        return new ProductDto(x.ProductId, x.ProductName, x.Price, x.Stock);
    }

    public Task<bool> ReserveAsync(int productId, int quantity)
    {
        return repo.ReserveStockAsync(productId, quantity);
    }
}