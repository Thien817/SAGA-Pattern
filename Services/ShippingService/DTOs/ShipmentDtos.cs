namespace ShippingService.DTOs;

public sealed record ShipmentDto(
    int ShipmentId,
    int OrderId,
    string Status,
    string? Carrier,
    string? TrackingNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record InventoryReservedPayload(int OrderId, string? ReservationReason);
