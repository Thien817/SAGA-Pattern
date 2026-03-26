namespace OrderService.DTOs;

public sealed record OrderItemDto(
    Guid OrderItemId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    Guid OrderId,
    Guid? CartId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items);

public sealed record CartCheckedOutPayload(
    Guid CartId,
    Guid UserId,
    decimal TotalAmount,
    IReadOnlyList<CartCheckedOutItemPayload> Items);

public sealed record CartCheckedOutItemPayload(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice);

public sealed record PaymentFailedPayload(Guid OrderId, string? FailureReason);
public sealed record InventoryFailedPayload(Guid OrderId, string? FailureReason);
public sealed record InventoryReservedPayload(Guid OrderId, string? ReservationReason);
