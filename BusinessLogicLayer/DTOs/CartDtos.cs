namespace BusinessLogicLayer.DTOs;

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);
public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CartItemDto(Guid CartItemId, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CartDto(Guid CartId, Guid UserId, string Status, IReadOnlyList<CartItemDto> Items);
public sealed record CheckoutResponse(Guid CartId, Guid UserId, decimal TotalAmount, IReadOnlyList<CartItemDto> Items);
