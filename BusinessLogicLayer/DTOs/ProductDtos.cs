namespace BusinessLogicLayer.DTOs;

public sealed record ProductDto(Guid ProductId, string ProductName, decimal Price, int Stock);

public sealed record ReserveStockRequest(Guid ProductId, int Quantity);