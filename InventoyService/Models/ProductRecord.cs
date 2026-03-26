namespace InventoryService.Models;

public sealed record ProductRecord(
    int ProductId,
    string ProductName,
    decimal Price,
    int Stock
);