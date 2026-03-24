using DataAccessLayer.Models;

namespace DataAccessLayer.IRepositories;

public interface ICartRepository
{
    Task<Guid> EnsureActiveCartAsync(Guid userId);
    Task<IReadOnlyList<CartItemRecord>> GetCartItemsAsync(Guid cartId);
    Task<ProductRecord?> GetProductAsync(Guid productId);
    Task AddOrIncreaseItemAsync(Guid cartId, Guid productId, int quantity, decimal unitPrice);
    Task<bool> UpdateItemQuantityAsync(Guid cartId, Guid cartItemId, int quantity);
    Task RemoveItemAsync(Guid cartId, Guid cartItemId);
    Task MarkCheckedOutAsync(Guid cartId);
}
