namespace CartService.Models;

public sealed record CartItemRecord(
    int CartItemId,
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
