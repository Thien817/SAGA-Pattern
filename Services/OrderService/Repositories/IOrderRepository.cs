using OrderService.Models;

namespace OrderService.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<OrderWithItemsRecord>> GetOrdersWithItemsAsync(int userId, CancellationToken cancellationToken = default);
    Task<OrderWithItemsRecord?> GetOrderWithItemsAsync(int orderId, int userId, CancellationToken cancellationToken = default);

    Task<int> CreateOrReplacePendingOrderAsync(
        int cartId,
        int userId,
        decimal totalAmount,
        IReadOnlyList<OrderItemCreate> items,
        CancellationToken cancellationToken = default);

    Task<bool> MarkPaidAsync(int orderId, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(int orderId, string? reason, CancellationToken cancellationToken = default);
    Task<bool> CompleteOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
