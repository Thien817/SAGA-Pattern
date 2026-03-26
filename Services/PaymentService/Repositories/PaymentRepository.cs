using System.Text.Json;
using Microsoft.Data.SqlClient;
using PaymentService.DTOs;
using PaymentService.Infrastructure;
using PaymentService.Models;

namespace PaymentService.Repositories;

public sealed class PaymentRepository(SqlConnectionFactory connectionFactory) : IPaymentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PaymentRecord?> GetPaymentByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PaymentId, OrderId, Amount, Status, Provider, TransactionRef, FailureReason, CreatedAt, UpdatedAt
FROM pay.Payments
WHERE OrderId = @OrderId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapPayment(reader);
    }

    public async Task<decimal?> GetOrderAmountAsync(int orderId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 TotalAmount
FROM ord.Orders
WHERE OrderId = @OrderId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is decimal amount ? amount : null;
    }

    public async Task<PaymentRecord> UpsertSuccessfulPaymentAsync(int orderId, decimal amount, CancellationToken cancellationToken = default)
    {
        var provider = "SIMULATED";
        var transactionRef = $"PAY-{orderId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var payment = await UpsertPaymentAsync(
            orderId,
            amount,
            "SUCCESS",
            provider,
            transactionRef,
            failureReason: null,
            eventType: "PaymentSucceeded",
            payloadFactory: record => new PaymentSucceededPayload(record.OrderId, record.Amount, record.Provider, record.TransactionRef),
            cancellationToken);

        return payment;
    }

    public async Task<PaymentRecord> UpsertFailedPaymentAsync(int orderId, decimal amount, string? failureReason, CancellationToken cancellationToken = default)
    {
        var payment = await UpsertPaymentAsync(
            orderId,
            amount,
            "FAILED",
            provider: "SIMULATED",
            transactionRef: null,
            failureReason,
            eventType: "PaymentFailed",
            payloadFactory: record => new PaymentFailedPayload(record.OrderId, record.FailureReason),
            cancellationToken);

        return payment;
    }

    public async Task<PaymentRecord> RefundPaymentAsync(int orderId, string? reason, CancellationToken cancellationToken = default)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            const string selectSql = @"
SELECT PaymentId, OrderId, Amount, Status, Provider, TransactionRef, FailureReason, CreatedAt, UpdatedAt
FROM pay.Payments
WHERE OrderId = @OrderId";

            PaymentRecord payment;
            await using (var selectCmd = new SqlCommand(selectSql, conn, (SqlTransaction)tx))
            {
                selectCmd.Parameters.AddWithValue("@OrderId", orderId);
                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Payment for orderId {orderId} was not found.");
                }

                payment = MapPayment(reader);
            }

            if (payment.Status == "REFUNDED")
            {
                await tx.CommitAsync(cancellationToken);
                return payment with { FailureReason = reason ?? payment.FailureReason };
            }

            const string updateSql = @"
UPDATE pay.Payments
SET Status = 'REFUNDED',
    FailureReason = @Reason,
    UpdatedAt = SYSUTCDATETIME()
WHERE PaymentId = @PaymentId";

            await using (var updateCmd = new SqlCommand(updateSql, conn, (SqlTransaction)tx))
            {
                updateCmd.Parameters.AddWithValue("@PaymentId", payment.PaymentId);
                updateCmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string refundSql = @"
INSERT INTO pay.PaymentRefunds (PaymentId, OrderId, Amount, Reason)
VALUES (@PaymentId, @OrderId, @Amount, @Reason)";

            await using (var refundCmd = new SqlCommand(refundSql, conn, (SqlTransaction)tx))
            {
                refundCmd.Parameters.AddWithValue("@PaymentId", payment.PaymentId);
                refundCmd.Parameters.AddWithValue("@OrderId", payment.OrderId);
                refundCmd.Parameters.AddWithValue("@Amount", payment.Amount);
                refundCmd.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
                await refundCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var refreshed = await GetPaymentForUpdateAsync(payment.PaymentId, conn, (SqlTransaction)tx, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return refreshed;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PaymentRecord> UpsertPaymentAsync<TPayload>(
        int orderId,
        decimal amount,
        string status,
        string? provider,
        string? transactionRef,
        string? failureReason,
        string eventType,
        Func<PaymentRecord, TPayload> payloadFactory,
        CancellationToken cancellationToken)
    {
        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            const string upsertSql = @"
IF EXISTS (SELECT 1 FROM pay.Payments WHERE OrderId = @OrderId)
BEGIN
    UPDATE pay.Payments
    SET Amount = @Amount,
        Status = @Status,
        Provider = @Provider,
        TransactionRef = @TransactionRef,
        FailureReason = @FailureReason,
        UpdatedAt = SYSUTCDATETIME()
    WHERE OrderId = @OrderId;

    SELECT PaymentId
    FROM pay.Payments
    WHERE OrderId = @OrderId;
END
ELSE
BEGIN
    INSERT INTO pay.Payments (OrderId, Amount, Status, Provider, TransactionRef, FailureReason)
    OUTPUT INSERTED.PaymentId
    VALUES (@OrderId, @Amount, @Status, @Provider, @TransactionRef, @FailureReason);
END";

            int paymentId;
            await using (var upsertCmd = new SqlCommand(upsertSql, conn, (SqlTransaction)tx))
            {
                upsertCmd.Parameters.AddWithValue("@OrderId", orderId);
                upsertCmd.Parameters.AddWithValue("@Amount", amount);
                upsertCmd.Parameters.AddWithValue("@Status", status);
                upsertCmd.Parameters.AddWithValue("@Provider", (object?)provider ?? DBNull.Value);
                upsertCmd.Parameters.AddWithValue("@TransactionRef", (object?)transactionRef ?? DBNull.Value);
                upsertCmd.Parameters.AddWithValue("@FailureReason", (object?)failureReason ?? DBNull.Value);
                paymentId = (int)(await upsertCmd.ExecuteScalarAsync(cancellationToken))!;
            }

            var payment = await GetPaymentForUpdateAsync(paymentId, conn, (SqlTransaction)tx, cancellationToken);

            var payloadJson = JsonSerializer.Serialize(payloadFactory(payment), JsonOptions);

            const string insertOutboxSql = @"
INSERT INTO msg.OutboxEvents (AggregateType, AggregateId, EventType, PayloadJson)
VALUES ('Payment', @AggregateId, @EventType, @PayloadJson)";

            await using (var outboxCmd = new SqlCommand(insertOutboxSql, conn, (SqlTransaction)tx))
            {
                outboxCmd.Parameters.AddWithValue("@AggregateId", orderId);
                outboxCmd.Parameters.AddWithValue("@EventType", eventType);
                outboxCmd.Parameters.AddWithValue("@PayloadJson", payloadJson);
                await outboxCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return payment;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PaymentRecord MapPayment(SqlDataReader reader)
    {
        return new PaymentRecord(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDateTime(7),
            reader.GetDateTime(8));
    }

    private static async Task<PaymentRecord> GetPaymentForUpdateAsync(int paymentId, SqlConnection conn, SqlTransaction tx, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT PaymentId, OrderId, Amount, Status, Provider, TransactionRef, FailureReason, CreatedAt, UpdatedAt
FROM pay.Payments
WHERE PaymentId = @PaymentId";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@PaymentId", paymentId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"PaymentId {paymentId} was not found after update.");
        }

        return MapPayment(reader);
    }
}