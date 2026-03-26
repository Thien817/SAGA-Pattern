namespace OrderService.Models;

public sealed record OrderItemCreate(int ProductId, int Quantity, decimal UnitPrice);
