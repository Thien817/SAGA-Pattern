using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CartService.DTOs;

public sealed class AddCartItemRequest
{
    [Range(1, int.MaxValue)]
    [DefaultValue(1)]
    public int ProductId { get; init; }

    [Range(1, 9999)]
    [DefaultValue(1)]
    public int Quantity { get; init; }
}

public sealed class UpdateCartItemRequest
{
    [Range(1, 9999)]
    [DefaultValue(1)]
    public int Quantity { get; init; }
}

public sealed record CartItemDto(int CartItemId, int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CartDto(int CartId, int UserId, string Status, IReadOnlyList<CartItemDto> Items);
public sealed record CheckoutResponse(int CartId, int UserId, decimal TotalAmount, IReadOnlyList<CartItemDto> Items);
