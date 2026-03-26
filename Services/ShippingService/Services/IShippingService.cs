using ShippingService.DTOs;

namespace ShippingService.Services;

public interface IShippingService
{
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken ct = default);
    Task<ShipmentDto?> GetShipmentByOrderIdAsync(int orderId, CancellationToken ct = default);
}
