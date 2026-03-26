using System.Text.Json;
using ShippingService.DTOs;
using ShippingService.Repositories;

namespace ShippingService.Services;

public sealed class ShippingService(IShippingRepository shippingRepository) : IShippingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("eventType is required.");

        switch (eventType)
        {
            case "InventoryReserved":
            {
                var payload = JsonSerializer.Deserialize<InventoryReservedPayload>(payloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("Invalid InventoryReserved payload.");
                await shippingRepository.CreateShipmentAsync(payload.OrderId, ct);
                return;
            }
            default:
                throw new InvalidOperationException($"Unsupported eventType: {eventType}");
        }
    }

    public async Task<ShipmentDto?> GetShipmentByOrderIdAsync(int orderId, CancellationToken ct = default)
    {
        var record = await shippingRepository.GetShipmentByOrderIdAsync(orderId, ct);
        if (record is null) return null;

        return new ShipmentDto(
            record.ShipmentId,
            record.OrderId,
            record.Status,
            record.Carrier,
            record.TrackingNumber,
            record.CreatedAt,
            record.UpdatedAt);
    }
}
