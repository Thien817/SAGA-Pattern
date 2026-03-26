using Microsoft.Data.SqlClient;
using ShippingService.Infrastructure;
using ShippingService.Models;

namespace ShippingService.Repositories;

public sealed class ShippingRepository(SqlConnectionFactory connectionFactory) : IShippingRepository
{
    public async Task CreateShipmentAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM ship.Shipments WHERE OrderId = @OrderId)
BEGIN
    INSERT INTO ship.Shipments (OrderId, Status, CreatedAt, UpdatedAt)
    VALUES (@OrderId, 'CREATED', SYSUTCDATETIME(), SYSUTCDATETIME())
END";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        await cmd.ExecuteNonQueryAsync(ct);
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
