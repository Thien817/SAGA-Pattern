using System.Data;
using Microsoft.Data.SqlClient;
using PaymentService.Infrastructure;
using PaymentService.Models;

namespace PaymentService.Repositories;

public sealed class PaymentRepository(SqlConnectionFactory connectionFactory) : IPaymentRepository
{
    public async Task<PaymentRecord> UpsertPaymentResultAsync(
        int orderId,
        decimal amount,
        string status,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            const string sql = @"
MERGE pay.Payments AS target
USING (SELECT @OrderId AS OrderId) AS source
ON target.OrderId = source.OrderId
WHEN MATCHED THEN
    UPDATE SET
        Amount = @Amount,
        Status = @Status,
        FailureReason = @FailureReason,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (OrderId, Amount, Status, FailureReason, CreatedAt, UpdatedAt)
    VALUES (@OrderId, @Amount, @Status, @FailureReason, SYSUTCDATETIME(), SYSUTCDATETIME())
OUTPUT inserted.PaymentId, inserted.OrderId, inserted.Amount, inserted.Status, inserted.FailureReason;";

            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@FailureReason", (object?)failureReason ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Cannot upsert payment.");

            var result = new PaymentRecord(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4));

            await reader.CloseAsync();
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<PaymentRecord?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 PaymentId, OrderId, Amount, Status, FailureReason
FROM pay.Payments
WHERE OrderId = @OrderId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var record = new PaymentRecord(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));

        await reader.CloseAsync();
        return record;
    }

    public async Task<PaymentRecord?> MarkRefundedAsync(int orderId, string? reason, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE pay.Payments
SET Status = 'REFUNDED',
    FailureReason = @Reason,
    UpdatedAt = SYSUTCDATETIME()
OUTPUT inserted.PaymentId, inserted.OrderId, inserted.Amount, inserted.Status, inserted.FailureReason
WHERE OrderId = @OrderId
  AND Status = 'SUCCESS';";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var record = new PaymentRecord(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));

        await reader.CloseAsync();
        return record;
    }

    public async Task AddRefundAsync(int paymentId, int orderId, decimal amount, string? reason, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO pay.PaymentRefunds (PaymentId, OrderId, Amount, Reason)
VALUES (@PaymentId, @OrderId, @Amount, @Reason);";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PaymentId", paymentId);
        cmd.Parameters.AddWithValue("@OrderId", orderId);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
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
