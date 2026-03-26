namespace OrderService.Models;

public sealed record OrderItemRecord(
    int OrderItemId,
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
