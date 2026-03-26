namespace OrderService.Models;

public sealed record OrderItemCreate(Guid ProductId, int Quantity, decimal UnitPrice);
