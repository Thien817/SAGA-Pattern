namespace ShippingService.Models;

public sealed record ShipmentRecord(
    int ShipmentId,
    int OrderId,
    string Status,
    string? Carrier,
    string? TrackingNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);
