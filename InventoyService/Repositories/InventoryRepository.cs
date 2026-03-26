using System.Data;
using InventoryService.Infrastructure;
using InventoryService.Models;
using Microsoft.Data.SqlClient;

namespace InventoryService.Repositories;

public sealed class InventoryRepository(SqlConnectionFactory factory) : IInventoryRepository
{
    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ProductId, p.ProductName, p.Price, s.OnHandQty
FROM inv.Products p
LEFT JOIN inv.InventoryStocks s ON s.ProductId = p.ProductId
ORDER BY p.ProductId";

        var result = new List<ProductRecord>();

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProductRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            ));
        }

        return result;
    }

    public async Task<ProductRecord?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 p.ProductId, p.ProductName, p.Price, s.OnHandQty
FROM inv.Products p
LEFT JOIN inv.InventoryStocks s ON s.ProductId = p.ProductId
WHERE p.ProductId = @ProductId";

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new ProductRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
        );
    }

    public async Task<bool> ReserveForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            const string checkSql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM ord.OrderItems oi
    JOIN inv.InventoryStocks s ON s.ProductId = oi.ProductId
    WHERE oi.OrderId = @OrderId
      AND s.OnHandQty < oi.Quantity
) THEN 1 ELSE 0 END";

            await using (var checkCmd = new SqlCommand(checkSql, conn, tx))
            {
                checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                var hasInsufficient = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0) == 1;
                if (hasInsufficient)
                {
                    tx.Rollback();
                    return false;
                }
            }

            const string reserveSql = @"
UPDATE s
SET s.OnHandQty = s.OnHandQty - oi.Quantity,
    s.ReservedQty = s.ReservedQty + oi.Quantity,
    s.UpdatedAt = SYSUTCDATETIME()
FROM inv.InventoryStocks s
JOIN ord.OrderItems oi ON oi.ProductId = s.ProductId
WHERE oi.OrderId = @OrderId;";

            await using (var reserveCmd = new SqlCommand(reserveSql, conn, tx))
            {
                reserveCmd.Parameters.AddWithValue("@OrderId", orderId);
                await reserveCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string insertReservationSql = @"
INSERT INTO inv.InventoryReservations (OrderId, ProductId, Quantity, Status, Reason, CreatedAt, UpdatedAt)
SELECT oi.OrderId, oi.ProductId, oi.Quantity, 'RESERVED', NULL, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM ord.OrderItems oi
WHERE oi.OrderId = @OrderId;";

            await using (var reservationCmd = new SqlCommand(insertReservationSql, conn, tx))
            {
                reservationCmd.Parameters.AddWithValue("@OrderId", orderId);
                await reservationCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task AddOutboxEventAsync(string aggregateType, int aggregateId, string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson, PublishStatus, RetryCount)
VALUES (@AggregateType, @AggregateId, @EventType, @PayloadJson, 'PENDING', 0);";

        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AggregateType", aggregateType);
        cmd.Parameters.AddWithValue("@AggregateId", aggregateId);
        cmd.Parameters.AddWithValue("@EventType", eventType);
        cmd.Parameters.AddWithValue("@PayloadJson", payloadJson);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
