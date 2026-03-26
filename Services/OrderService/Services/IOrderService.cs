using OrderService.DTOs;

namespace OrderService.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}
