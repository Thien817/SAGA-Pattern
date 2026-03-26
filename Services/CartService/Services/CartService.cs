using CartService.DTOs;
using CartService.Repositories;

namespace CartService.Services;

public sealed class CartService(ICartRepository cartRepository) : ICartService
{
    public async Task<CartDto> GetMyCartAsync(int userId)
    {
        var cartId = await cartRepository.EnsureActiveCartAsync(userId);
        var items = await cartRepository.GetCartItemsAsync(cartId);

        var dtoItems = items
            .Select(x => new CartItemDto(x.CartItemId, x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice))
            .ToList();

        return new CartDto(cartId, userId, "ACTIVE", dtoItems);
    }

    public async Task<CartDto> AddItemAsync(int userId, AddCartItemRequest request)
    {
        var cartId = await cartRepository.EnsureActiveCartAsync(userId);
        var product = await cartRepository.GetProductAsync(request.ProductId);

        if (product is null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        await cartRepository.AddOrIncreaseItemAsync(cartId, request.ProductId, request.Quantity, product.Price);

        return await GetMyCartAsync(userId);
    }

    public async Task<CartDto> UpdateItemAsync(int userId, int cartItemId, UpdateCartItemRequest request)
    {
        var cartId = await cartRepository.EnsureActiveCartAsync(userId);
        var ok = await cartRepository.UpdateItemQuantityAsync(cartId, cartItemId, request.Quantity);

        if (!ok)
        {
            throw new InvalidOperationException("Cart item not found.");
        }

        return await GetMyCartAsync(userId);
    }

    public async Task RemoveItemAsync(int userId, int cartItemId)
    {
        var cartId = await cartRepository.EnsureActiveCartAsync(userId);
        await cartRepository.RemoveItemAsync(cartId, cartItemId);
    }

    public async Task<CheckoutResponse> CheckoutAsync(int userId)
    {
        var cartId = await cartRepository.EnsureActiveCartAsync(userId);
        var items = await cartRepository.GetCartItemsAsync(cartId);

        if (items.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        await cartRepository.MarkCheckedOutAsync(cartId);

        var dtoItems = items
            .Select(x => new CartItemDto(x.CartItemId, x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice))
            .ToList();

        var totalAmount = dtoItems.Sum(x => x.LineTotal);
        return new CheckoutResponse(cartId, userId, totalAmount, dtoItems);
    }
}
