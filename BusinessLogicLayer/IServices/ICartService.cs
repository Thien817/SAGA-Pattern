using BusinessLogicLayer.DTOs;

namespace BusinessLogicLayer.IServices;

public interface ICartService
{
    Task<CartDto> GetMyCartAsync(Guid userId);
    Task<CartDto> AddItemAsync(Guid userId, AddCartItemRequest request);
    Task<CartDto> UpdateItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequest request);
    Task RemoveItemAsync(Guid userId, Guid cartItemId);
    Task<CheckoutResponse> CheckoutAsync(Guid userId);
}
