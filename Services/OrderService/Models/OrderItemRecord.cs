namespace OrderService.Models;

public sealed record OrderItemRecord(
    Guid OrderItemId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
