namespace CartService.DTOs;

public sealed record AddCartItemRequest(int ProductId, int Quantity);
public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartItemDto(int CartItemId, int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CartDto(int CartId, int UserId, string Status, IReadOnlyList<CartItemDto> Items);
public sealed record CheckoutResponse(int CartId, int UserId, decimal TotalAmount, IReadOnlyList<CartItemDto> Items);
