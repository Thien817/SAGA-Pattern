using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAccessLayer.Models;

namespace DataAccessLayer.IRepositories
{
 

    public interface IInventoryRepository
    {
        Task<IReadOnlyList<ProductDetailRecord>> GetProductsAsync();
        Task<ProductDetailRecord?> GetProductByIdAsync(Guid productId);

        Task<bool> ReserveStockAsync(Guid productId, int quantity);
    }
}
