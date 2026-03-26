using CartService.DTOs;

namespace CartService.Services;

public interface ICartService
{
    Task<CartDto> GetMyCartAsync(int userId);
    Task<CartDto> AddItemAsync(int userId, AddCartItemRequest request);
    Task<CartDto> UpdateItemAsync(int userId, int cartItemId, UpdateCartItemRequest request);
    Task RemoveItemAsync(int userId, int cartItemId);
    Task<CheckoutResponse> CheckoutAsync(int userId);
}
