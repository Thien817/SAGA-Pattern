using CartService.Models;

namespace CartService.Repositories;

public interface ICartRepository
{
    Task<int> EnsureActiveCartAsync(int userId);
    Task<IReadOnlyList<CartItemRecord>> GetCartItemsAsync(int cartId);
    Task<ProductRecord?> GetProductAsync(int productId);
    Task<int> GetAvailableStockAsync(int productId);
    Task<int?> GetCartItemQuantityAsync(int cartId, int cartItemId);
    Task<int?> GetProductIdByCartItemAsync(int cartId, int cartItemId);
    Task<int> GetCartItemQuantityByProductAsync(int cartId, int productId);
    Task AddOrIncreaseItemAsync(int cartId, int productId, int quantity, decimal unitPrice);
    Task<bool> UpdateItemQuantityAsync(int cartId, int cartItemId, int quantity);
    Task RemoveItemAsync(int cartId, int cartItemId);
    Task MarkCheckedOutAsync(int cartId);
    Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson);
}
