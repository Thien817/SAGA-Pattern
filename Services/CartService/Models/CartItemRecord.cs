namespace CartService.Models;

public sealed record CartItemRecord(
    Guid CartItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
