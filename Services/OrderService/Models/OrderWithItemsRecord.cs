namespace OrderService.Models;

public sealed record OrderWithItemsRecord(OrderRecord Order, IReadOnlyList<OrderItemRecord> Items);
