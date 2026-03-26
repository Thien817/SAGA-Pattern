using CartService.Models;
using CartService.DTOs;

namespace CartService.Repositories;

public interface ICartRepository
{
    Task<int> EnsureActiveCartAsync(int userId);
    Task<IReadOnlyList<CartItemRecord>> GetCartItemsAsync(int cartId);
    Task<ProductRecord?> GetProductAsync(int productId);
    Task AddOrIncreaseItemAsync(int cartId, int productId, int quantity, decimal unitPrice);
    Task<bool> UpdateItemQuantityAsync(int cartId, int cartItemId, int quantity);
    Task RemoveItemAsync(int cartId, int cartItemId);
    Task MarkCheckedOutAsync(int cartId);
    Task<CheckoutResponse> CheckoutAndStageCartCheckedOutAsync(int userId, CancellationToken cancellationToken = default);
}
