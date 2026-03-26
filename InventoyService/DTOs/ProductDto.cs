namespace InventoryService.DTOs;

public sealed record ProductDto(
    int ProductId,
    string ProductName,
    decimal Price,
    int Stock
);