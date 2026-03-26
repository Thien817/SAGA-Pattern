namespace OrderService.DTOs;

public sealed record OrderItemDto(
    int OrderItemId,
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    int OrderId,
    int? CartId,
    int UserId,
    decimal TotalAmount,
    string Status,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items);

public sealed record CartCheckedOutPayload(
    int CartId,
    int UserId,
    decimal TotalAmount,
    IReadOnlyList<CartCheckedOutItemPayload> Items);

public sealed record CartCheckedOutItemPayload(
    int ProductId,
    int Quantity,
    decimal UnitPrice);

public sealed record PaymentFailedPayload(int OrderId, string? FailureReason);
public sealed record InventoryFailedPayload(int OrderId, string? FailureReason);
public sealed record InventoryReservedPayload(int OrderId, string? ReservationReason);
