using System.Text.Json;
using InventoryService.DTOs;
using InventoryService.Infrastructure;
using InventoryService.Models;
using Microsoft.Data.SqlClient;

namespace InventoryService.Repositories;

public sealed class InventoryRepository(SqlConnectionFactory factory)
    : IInventoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync()
    {
        const string sql = @"
SELECT p.ProductId, p.ProductName, p.Price, ISNULL(s.OnHandQty, 0) AS Stock
FROM inv.Products p
LEFT JOIN inv.InventoryStocks s ON s.ProductId = p.ProductId";

        var result = new List<ProductRecord>();

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ProductRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3)
            ));
        }

        return result;
    }

    public async Task<ProductRecord?> GetByIdAsync(int productId)
    {
        const string sql = @"
SELECT TOP 1 p.ProductId, p.ProductName, p.Price, ISNULL(s.OnHandQty, 0) AS Stock
FROM inv.Products p
LEFT JOIN inv.InventoryStocks s ON s.ProductId = p.ProductId
WHERE p.ProductId = @ProductId";

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new ProductRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3)
        );
    }

    public async Task<bool> ReserveStockAsync(int productId, int quantity)
    {
        const string sql = @"
UPDATE inv.InventoryStocks
SET OnHandQty = OnHandQty - @Quantity,
        ReservedQty = ReservedQty + @Quantity,
        UpdatedAt = SYSUTCDATETIME()
WHERE ProductId = @ProductId
    AND OnHandQty >= @Quantity";

        await using var conn = factory.Create();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@Quantity", quantity);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<InventoryReservationResult> ReserveOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        await using var conn = factory.Create();
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            const string loadItemsSql = @"
SELECT
    oi.ProductId,
    oi.Quantity,
    p.ProductName,
    ISNULL(s.OnHandQty, 0) AS OnHandQty
FROM ord.OrderItems oi
JOIN inv.Products p ON p.ProductId = oi.ProductId
LEFT JOIN inv.InventoryStocks s WITH (UPDLOCK, ROWLOCK) ON s.ProductId = oi.ProductId
WHERE oi.OrderId = @OrderId
ORDER BY oi.OrderItemId";

            var items = new List<(int ProductId, int Quantity, string ProductName, int OnHandQty)>();
            await using (var loadCmd = new SqlCommand(loadItemsSql, conn, (SqlTransaction)tx))
            {
                loadCmd.Parameters.AddWithValue("@OrderId", orderId);
                await using var reader = await loadCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add((
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetInt32(3)));
                }
            }

            if (items.Count == 0)
            {
                const string reason = "Order has no items to reserve.";
                await StageFailureAsync(orderId, reason, conn, (SqlTransaction)tx, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return new InventoryReservationResult(orderId, false, reason);
            }

            var insufficient = items.FirstOrDefault(item => item.OnHandQty < item.Quantity);
            if (insufficient != default)
            {
                var reason = $"Insufficient stock for product {insufficient.ProductName}.";
                await StageFailureAsync(orderId, reason, conn, (SqlTransaction)tx, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return new InventoryReservationResult(orderId, false, reason);
            }

            const string deleteExistingSql = @"
DELETE FROM inv.InventoryReservations
WHERE OrderId = @OrderId";

            await using (var deleteCmd = new SqlCommand(deleteExistingSql, conn, (SqlTransaction)tx))
            {
                deleteCmd.Parameters.AddWithValue("@OrderId", orderId);
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string reserveStockSql = @"
UPDATE inv.InventoryStocks
SET OnHandQty = OnHandQty - @Quantity,
    ReservedQty = ReservedQty + @Quantity,
    UpdatedAt = SYSUTCDATETIME()
WHERE ProductId = @ProductId
  AND OnHandQty >= @Quantity";

            const string insertReservationSql = @"
INSERT INTO inv.InventoryReservations (OrderId, ProductId, Quantity, Status, Reason)
VALUES (@OrderId, @ProductId, @Quantity, 'RESERVED', NULL)";

            foreach (var item in items)
            {
                await using (var reserveCmd = new SqlCommand(reserveStockSql, conn, (SqlTransaction)tx))
                {
                    reserveCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    reserveCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    var rows = await reserveCmd.ExecuteNonQueryAsync(cancellationToken);
                    if (rows == 0)
                    {
                        throw new InvalidOperationException($"Concurrent stock update prevented reservation for productId {item.ProductId}.");
                    }
                }

                await using var reservationCmd = new SqlCommand(insertReservationSql, conn, (SqlTransaction)tx);
                reservationCmd.Parameters.AddWithValue("@OrderId", orderId);
                reservationCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                reservationCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                await reservationCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var payloadJson = JsonSerializer.Serialize(new InventoryReservedPayload(orderId, "Inventory reserved successfully."), JsonOptions);
            await StageOutboxEventAsync(orderId, "InventoryReserved", payloadJson, conn, (SqlTransaction)tx, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new InventoryReservationResult(orderId, true, null);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task StageFailureAsync(int orderId, string reason, SqlConnection conn, SqlTransaction tx, CancellationToken cancellationToken)
    {
        const string deleteExistingSql = @"
DELETE FROM inv.InventoryReservations
WHERE OrderId = @OrderId";

        await using (var deleteCmd = new SqlCommand(deleteExistingSql, conn, tx))
        {
            deleteCmd.Parameters.AddWithValue("@OrderId", orderId);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertFailedSql = @"
INSERT INTO inv.InventoryReservations (OrderId, ProductId, Quantity, Status, Reason)
SELECT @OrderId, oi.ProductId, oi.Quantity, 'FAILED', @Reason
FROM ord.OrderItems oi
WHERE oi.OrderId = @OrderId";

        await using (var failedCmd = new SqlCommand(insertFailedSql, conn, tx))
        {
            failedCmd.Parameters.AddWithValue("@OrderId", orderId);
            failedCmd.Parameters.AddWithValue("@Reason", reason);
            await failedCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var payloadJson = JsonSerializer.Serialize(new InventoryFailedPayload(orderId, reason), JsonOptions);
        await StageOutboxEventAsync(orderId, "InventoryFailed", payloadJson, conn, tx, cancellationToken);
    }

    private static async Task StageOutboxEventAsync(int orderId, string eventType, string payloadJson, SqlConnection conn, SqlTransaction tx, CancellationToken cancellationToken)
    {
        const string insertOutboxSql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson)
VALUES ('Inventory', @AggregateId, @EventType, @PayloadJson)";

        await using var outboxCmd = new SqlCommand(insertOutboxSql, conn, tx);
        outboxCmd.Parameters.AddWithValue("@AggregateId", orderId);
        outboxCmd.Parameters.AddWithValue("@EventType", eventType);
        outboxCmd.Parameters.AddWithValue("@PayloadJson", payloadJson);
        await outboxCmd.ExecuteNonQueryAsync(cancellationToken);
    }
}