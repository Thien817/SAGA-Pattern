using Microsoft.Data.SqlClient;
using OrderService.Infrastructure;

namespace OrderService.BackgroundServices;

public sealed class OutboxEventPublisher : BackgroundService
{
    private readonly SqlConnectionFactory _connectionFactory;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    public OutboxEventPublisher(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var eventsToPublish = await DequeueAsync(stoppingToken);
                foreach (var evt in eventsToPublish)
                {
                    await PublishOneAsync(evt, stoppingToken);
                }
            }
            catch
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
WHERE PublishStatus = 'PENDING'
ORDER BY OccurredAt";

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BatchSize", BatchSize);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var result = new List<(int, string, string)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return result;
    }

    private async Task PublishOneAsync((int EventId, string EventType, string PayloadJson) evt, CancellationToken cancellationToken)
    {
        var consumers = GetConsumers(evt.EventType);
        if (consumers.Count == 0)
        {
            await MarkFailedAsync(evt.EventId, $"No consumer for eventType {evt.EventType}", cancellationToken);
            return;
        }

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var consumer in consumers)
            {
                const string insertSql = @"
IF NOT EXISTS (SELECT 1 FROM msg.InboxEvents WHERE EventId = @EventId AND ConsumerName = @ConsumerName)
BEGIN
    INSERT INTO msg.InboxEvents (EventId, ConsumerName, EventType, PayloadJson, ProcessStatus)
    VALUES (@EventId, @ConsumerName, @EventType, @PayloadJson, 'RECEIVED')
END";

                await using var insertCmd = new SqlCommand(insertSql, conn, tx);
                insertCmd.Parameters.AddWithValue("@EventId", evt.EventId);
                insertCmd.Parameters.AddWithValue("@ConsumerName", consumer);
                insertCmd.Parameters.AddWithValue("@EventType", evt.EventType);
                insertCmd.Parameters.AddWithValue("@PayloadJson", evt.PayloadJson);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string updateSql = @"
UPDATE msg.OutboxEvents
SET PublishStatus = 'PUBLISHED',
    PublishedAt = SYSUTCDATETIME()
WHERE EventId = @EventId";

            await using var updateCmd = new SqlCommand(updateSql, conn, tx);
            updateCmd.Parameters.AddWithValue("@EventId", evt.EventId);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);

            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            await MarkFailedAsync(evt.EventId, ex.Message, cancellationToken);
        }
    }

    private static IReadOnlyList<string> GetConsumers(string eventType)
    {
        return eventType switch
        {
            "CartCheckedOut" => new[] { "OrderService" },
            "OrderCreated" => new[] { "PaymentService" },
            "PaymentSucceeded" => new[] { "InventoryService" },
            "PaymentFailed" => new[] { "OrderService" },
            "InventoryReserved" => new[] { "OrderService" },
            "InventoryFailed" => new[] { "OrderService", "PaymentService" },
            _ => Array.Empty<string>()
        };
    }

    private async Task MarkFailedAsync(int eventId, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE msg.OutboxEvents
SET PublishStatus = 'FAILED',
    PublishedAt = SYSUTCDATETIME(),
    RetryCount = RetryCount + 1
WHERE EventId = @EventId";

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EventId", eventId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
