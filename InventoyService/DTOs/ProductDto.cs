namespace InventoryService.DTOs;

public sealed record ProductDto(
    int ProductId,
    string ProductName,
    decimal Price,
    int Stock
);

public sealed record PaymentSucceededPayload(int OrderId, decimal Amount, string? Provider, string? TransactionRef);
public sealed record InventoryReservedPayload(int OrderId, string? ReservationReason);
public sealed record InventoryFailedPayload(int OrderId, string? FailureReason);