using OrderService.DTOs;

namespace OrderService.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(int userId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetOrderAsync(int orderId, int userId, CancellationToken cancellationToken = default);
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
