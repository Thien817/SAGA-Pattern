namespace DataAccessLayer.Models;

public sealed record OrderItemCreate(Guid ProductId, int Quantity, decimal UnitPrice);

