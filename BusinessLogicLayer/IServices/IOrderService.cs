using BusinessLogicLayer.DTOs;

namespace BusinessLogicLayer.IServices;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);

    // Internal inbox handler used by the Order service background worker.
    Task HandleInboxEventAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default);
}

