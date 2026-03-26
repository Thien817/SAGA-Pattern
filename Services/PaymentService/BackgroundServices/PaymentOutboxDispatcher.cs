using Microsoft.Data.SqlClient;
using PaymentService.Infrastructure;

namespace PaymentService.BackgroundServices;

public sealed class PaymentOutboxDispatcher(SqlConnectionFactory connectionFactory) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var events = await DequeueAsync(stoppingToken);
                foreach (var outboxEvent in events)
                {
                    await DispatchOneAsync(outboxEvent, stoppingToken);
                }
            }
            catch (Exception)
            {
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<List<(int EventId, string EventType, string PayloadJson)>> DequeueAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP (@BatchSize)
    EventId,
    EventType,
    PayloadJson
FROM msg.OutboxEvents
WHERE AggregateType = 'Payment'
  AND PublishStatus = 'PENDING'
ORDER BY OccurredAt";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BatchSize", BatchSize);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<(int, string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return result;
    }

    private async Task DispatchOneAsync((int EventId, string EventType, string PayloadJson) outboxEvent, CancellationToken cancellationToken)
    {
        try
        {
            var consumers = ResolveConsumers(outboxEvent.EventType);
            if (consumers.Count == 0)
            {
                throw new InvalidOperationException($"No consumer configured for eventType {outboxEvent.EventType}.");
            }

            await using var conn = connectionFactory.Create();
            await conn.OpenAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);

            foreach (var consumer in consumers)
            {
                const string insertInboxSql = @"
IF NOT EXISTS (
    SELECT 1
    FROM msg.InboxEvents
    WHERE EventId = @EventId
)
BEGIN
    INSERT INTO msg.InboxEvents (EventId, ConsumerName, EventType, PayloadJson)
    VALUES (@EventId, @ConsumerName, @EventType, @PayloadJson)
END";

                await using var insertCmd = new SqlCommand(insertInboxSql, conn, (SqlTransaction)tx);
                insertCmd.Parameters.AddWithValue("@EventId", BuildInboxEventId(outboxEvent.EventId, consumer));
                insertCmd.Parameters.AddWithValue("@ConsumerName", consumer);
                insertCmd.Parameters.AddWithValue("@EventType", outboxEvent.EventType);
                insertCmd.Parameters.AddWithValue("@PayloadJson", outboxEvent.PayloadJson);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string markPublishedSql = @"
UPDATE msg.OutboxEvents
SET PublishStatus = 'PUBLISHED',
    PublishedAt = SYSUTCDATETIME()
WHERE EventId = @EventId";

            await using (var markCmd = new SqlCommand(markPublishedSql, conn, (SqlTransaction)tx))
            {
                markCmd.Parameters.AddWithValue("@EventId", outboxEvent.EventId);
                await markCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await MarkFailedAsync(outboxEvent.EventId, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(int eventId, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE msg.OutboxEvents
SET PublishStatus = 'FAILED',
    RetryCount = RetryCount + 1
WHERE EventId = @EventId";

        await using var conn = connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EventId", eventId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<string> ResolveConsumers(string eventType)
    {
        return eventType switch
        {
            "PaymentSucceeded" => ["OrderService", "InventoryService"],
            "PaymentFailed" => ["OrderService"],
            _ => []
        };
    }

    private static int BuildInboxEventId(int outboxEventId, string consumerName)
    {
        const int multiplier = 10;
        var suffix = consumerName switch
        {
            "OrderService" => 1,
            "PaymentService" => 2,
            "InventoryService" => 3,
            "ShippingService" => 4,
            _ => 9
        };

        return checked(outboxEventId * multiplier + suffix);
    }
}