using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessLogicLayer.DTOs;

namespace BusinessLogicLayer.IServices
{
    public interface IInventoryService
    {
        Task<IReadOnlyList<ProductDto>> GetProductsAsync();
        Task<ProductDto?> GetProductAsync(Guid productId);

        Task<bool> ReserveStockAsync(ReserveStockRequest request);
    }
}
