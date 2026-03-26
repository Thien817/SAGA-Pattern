using System.Text.Json;
using Microsoft.Data.SqlClient;
using ShippingService.DTOs;
using ShippingService.Infrastructure;
using ShippingService.Models;

namespace ShippingService.Repositories;

public sealed class ShippingRepository(SqlConnectionFactory connectionFactory) : IShippingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CreateShipmentAsync(int orderId, CancellationToken ct = default)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            const string upsertShipmentSql = @"
IF EXISTS (SELECT 1 FROM ship.Shipments WHERE OrderId = @OrderId)
BEGIN
    UPDATE ship.Shipments
    SET Status = 'CREATED',
        UpdatedAt = SYSUTCDATETIME()
    WHERE OrderId = @OrderId
END
ELSE
BEGIN
    INSERT INTO ship.Shipments (OrderId, Status, CreatedAt, UpdatedAt)
    VALUES (@OrderId, 'CREATED', SYSUTCDATETIME(), SYSUTCDATETIME())
END";

            await using (var shipmentCmd = new SqlCommand(upsertShipmentSql, conn, (SqlTransaction)tx))
            {
                shipmentCmd.Parameters.AddWithValue("@OrderId", orderId);
                await shipmentCmd.ExecuteNonQueryAsync(ct);
            }

            var payloadJson = JsonSerializer.Serialize(new ShipmentCreatedPayload(orderId, "CREATED"), JsonOptions);

            const string insertOutboxSql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson)
VALUES ('Shipping', @AggregateId, 'ShipmentCreated', @PayloadJson)";

            await using (var outboxCmd = new SqlCommand(insertOutboxSql, conn, (SqlTransaction)tx))
            {
                outboxCmd.Parameters.AddWithValue("@AggregateId", orderId);
                outboxCmd.Parameters.AddWithValue("@PayloadJson", payloadJson);
                await outboxCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ShipmentRecord?> GetShipmentByOrderIdAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT ShipmentId, OrderId, Status, Carrier, TrackingNumber, CreatedAt, UpdatedAt
FROM ship.Shipments
WHERE OrderId = @OrderId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ShipmentRecord(
            ShipmentId:     reader.GetInt32(0),
            OrderId:        reader.GetInt32(1),
            Status:         reader.GetString(2),
            Carrier:        reader.IsDBNull(3) ? null : reader.GetString(3),
            TrackingNumber: reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt:      reader.GetDateTime(5),
            UpdatedAt:      reader.GetDateTime(6));
    }
}
