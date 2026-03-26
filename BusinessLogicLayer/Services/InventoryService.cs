using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessLogicLayer.DTOs;
using BusinessLogicLayer.IServices;
using DataAccessLayer.IRepositories;

namespace BusinessLogicLayer.Services
{
    public sealed class InventoryService(IInventoryRepository inventoryRepository) : IInventoryService
    {
        public async Task<IReadOnlyList<ProductDto>> GetProductsAsync()
        {
            var products = await inventoryRepository.GetProductsAsync();

            return products
                .Select(p => new ProductDto(p.ProductId, p.ProductName, p.Price, p.Stock))
                .ToList();
        }

        public async Task<ProductDto?> GetProductAsync(Guid productId)
        {
            var p = await inventoryRepository.GetProductByIdAsync(productId);
            if (p is null) return null;

            return new ProductDto(p.ProductId, p.ProductName, p.Price, p.Stock);
        }

        public async Task<bool> ReserveStockAsync(ReserveStockRequest request)
        {
            if (request.Quantity <= 0)
                throw new InvalidOperationException("Quantity must be > 0");

            return await inventoryRepository.ReserveStockAsync(request.ProductId, request.Quantity);
        }
    }
}
