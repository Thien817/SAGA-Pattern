using ShippingService.Models;

namespace ShippingService.Repositories;

public interface IShippingRepository
{
    Task CreateShipmentAsync(int orderId, CancellationToken ct = default);
    Task<ShipmentRecord?> GetShipmentByOrderIdAsync(int orderId, CancellationToken ct = default);
}
