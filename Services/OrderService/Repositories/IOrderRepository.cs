using OrderService.Models;

namespace OrderService.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<OrderWithItemsRecord>> GetOrdersWithItemsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderWithItemsRecord?> GetOrderWithItemsAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);

    Task<Guid> CreateOrReplacePendingOrderAsync(
        Guid cartId,
        Guid userId,
        decimal totalAmount,
        IReadOnlyList<OrderItemCreate> items,
        CancellationToken cancellationToken = default);

    Task<bool> CancelOrderAsync(Guid orderId, string? reason, CancellationToken cancellationToken = default);
    Task<bool> CompleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
