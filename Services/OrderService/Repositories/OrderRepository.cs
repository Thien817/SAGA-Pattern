using Microsoft.Data.SqlClient;
using OrderService.Infrastructure;
using OrderService.Models;

namespace OrderService.Repositories;

public sealed class OrderRepository(SqlConnectionFactory connectionFactory) : IOrderRepository
{
    public async Task<IReadOnlyList<OrderWithItemsRecord>> GetOrdersWithItemsAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    o.OrderId,
    o.CartId,
    o.UserId,
    o.TotalAmount,
    o.Status,
    o.CancelReason,
    o.CreatedAt,
    o.UpdatedAt,
    oi.OrderItemId,
    oi.ProductId,
    oi.Quantity,
    oi.UnitPrice,
    oi.LineTotal
FROM ord.Orders o
LEFT JOIN ord.OrderItems oi ON oi.OrderId = o.OrderId
WHERE o.UserId = @UserId
ORDER BY o.CreatedAt DESC, oi.ProductId";

        var byId = new Dictionary<int, (OrderRecord order, List<OrderItemRecord> items)>();

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetInt32(0);
            if (!byId.TryGetValue(orderId, out var entry))
            {
                var order = new OrderRecord(
                    orderId,
                    reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetDecimal(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetDateTime(6),
                    reader.GetDateTime(7));

                entry = (order, new List<OrderItemRecord>());
                byId[orderId] = entry;
            }

            if (!reader.IsDBNull(8))
            {
                entry.items.Add(new OrderItemRecord(
                    reader.GetInt32(8),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12)));
            }
        }

        return byId.Values.Select(x => new OrderWithItemsRecord(x.order, x.items)).ToList();
    }

    public async Task<OrderWithItemsRecord?> GetOrderWithItemsAsync(int orderId, int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    o.OrderId,
    o.CartId,
    o.UserId,
    o.TotalAmount,
    o.Status,
    o.CancelReason,
    o.CreatedAt,
    o.UpdatedAt,
    oi.OrderItemId,
    oi.ProductId,
    oi.Quantity,
    oi.UnitPrice,
    oi.LineTotal
FROM ord.Orders o
LEFT JOIN ord.OrderItems oi ON oi.OrderId = o.OrderId
WHERE o.OrderId = @OrderId
  AND o.UserId = @UserId
ORDER BY oi.ProductId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        OrderRecord? order = null;
        var items = new List<OrderItemRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            if (order is null)
            {
                order = new OrderRecord(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetDecimal(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetDateTime(6),
                    reader.GetDateTime(7));
            }

            if (!reader.IsDBNull(8))
            {
                items.Add(new OrderItemRecord(
                    reader.GetInt32(8),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12)));
            }
        }

        return order is null ? null : new OrderWithItemsRecord(order, items);
    }

    public async Task<int> CreateOrReplacePendingOrderAsync(int cartId, int userId, decimal totalAmount, IReadOnlyList<OrderItemCreate> items, CancellationToken cancellationToken = default)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction();

        try
        {
            const string findOrderSql = @"
SELECT TOP 1 OrderId
FROM ord.Orders
WHERE CartId = @CartId
  AND UserId = @UserId
ORDER BY CreatedAt DESC";

            int? existingOrderId = null;
            await using (var findCmd = new SqlCommand(findOrderSql, conn, tx))
            {
                findCmd.Parameters.AddWithValue("@CartId", cartId);
                findCmd.Parameters.AddWithValue("@UserId", userId);
                var existing = await findCmd.ExecuteScalarAsync(cancellationToken);
                if (existing is int existingId) existingOrderId = existingId;
            }

            int orderId;
            if (existingOrderId is not null)
            {
                orderId = existingOrderId.Value;

                const string updateOrderSql = @"
UPDATE ord.Orders
SET TotalAmount = @TotalAmount,
    Status = 'PENDING',
    CancelReason = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE OrderId = @OrderId";

                await using (var updateCmd = new SqlCommand(updateOrderSql, conn, tx))
                {
                    updateCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                    updateCmd.Parameters.AddWithValue("@OrderId", orderId);
                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                const string deleteItemsSql = @"
DELETE FROM ord.OrderItems
WHERE OrderId = @OrderId";

                await using (var deleteCmd = new SqlCommand(deleteItemsSql, conn, tx))
                {
                    deleteCmd.Parameters.AddWithValue("@OrderId", orderId);
                    await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            else
            {
                const string insertOrderSql = @"
INSERT INTO ord.Orders (CartId, UserId, TotalAmount, Status)
OUTPUT INSERTED.OrderId
VALUES (@CartId, @UserId, @TotalAmount, 'PENDING')";

                await using var insertCmd = new SqlCommand(insertOrderSql, conn, tx);
                insertCmd.Parameters.AddWithValue("@CartId", cartId);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

                var created = await insertCmd.ExecuteScalarAsync(cancellationToken);
                orderId = (int)created!;
            }

            const string insertItemSql = @"
INSERT INTO ord.OrderItems (OrderId, ProductId, Quantity, UnitPrice)
VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)";

            foreach (var item in items)
            {
                await using var insertItemCmd = new SqlCommand(insertItemSql, conn, tx);
                insertItemCmd.Parameters.AddWithValue("@OrderId", orderId);
                insertItemCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                insertItemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                insertItemCmd.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
                await insertItemCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            tx.Commit();
            return orderId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(int orderId, string? reason, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE ord.Orders
SET Status = 'CANCELLED',
    CancelReason = @Reason,
    UpdatedAt = SYSUTCDATETIME()
WHERE OrderId = @OrderId
  AND Status IN ('PENDING', 'PAID')";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> CompleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE ord.Orders
SET Status = 'COMPLETED',
    UpdatedAt = SYSUTCDATETIME()
WHERE OrderId = @OrderId
  AND Status IN ('PENDING', 'PAID')";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson, PublishStatus, RetryCount)
VALUES (@AggregateType, @AggregateId, @EventType, @PayloadJson, 'PENDING', 0);";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AggregateType", aggregateType);
        cmd.Parameters.AddWithValue("@AggregateId", aggregateId);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@PayloadJson", payloadJson);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
