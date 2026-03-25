using BusinessLogicLayer.IServices;
using DataAccessLayer.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SAGA_Pattern.BackgroundServices;

public sealed class OrderInboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqlConnectionFactory _connectionFactory;

    // In production you'd typically avoid polling; this is a simple demo worker.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const string ConsumerName = "OrderService";
    private const int BatchSize = 10;

    public OrderInboxProcessor(IServiceScopeFactory scopeFactory, SqlConnectionFactory connectionFactory)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var inboxEvents = await DequeueAsync(stoppingToken);
                foreach (var inboxEvent in inboxEvents)
                {
                    await HandleOneAsync(inboxEvent, stoppingToken);
                }
            }
            catch (Exception)
            {
                // Avoid crashing the host: the worker will retry next tick.
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<List<(Guid EventId, string EventType, string PayloadJson)>> DequeueAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP (@BatchSize)
    EventId,
    EventType,
    PayloadJson
FROM msg.InboxEvents
WHERE ConsumerName = @ConsumerName
  AND ProcessStatus = 'RECEIVED'
ORDER BY ReceivedAt";

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BatchSize", BatchSize);
        cmd.Parameters.AddWithValue("@ConsumerName", ConsumerName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<(Guid, string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return result;
    }

    private async Task HandleOneAsync(
        (Guid EventId, string EventType, string PayloadJson) inboxEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // BackgroundService is singleton; create a scope per event to safely resolve scoped services.
            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            await orderService.HandleInboxEventAsync(
                inboxEvent.EventType,
                inboxEvent.PayloadJson,
                cancellationToken);

            await MarkProcessedAsync(inboxEvent.EventId, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(inboxEvent.EventId, ex.Message, cancellationToken);
        }
    }

    private async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE msg.InboxEvents
SET ProcessStatus = 'PROCESSED',
    ProcessedAt = SYSUTCDATETIME(),
    ErrorMessage = NULL
WHERE EventId = @EventId";

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EventId", eventId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(Guid eventId, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE msg.InboxEvents
SET ProcessStatus = 'FAILED',
    ProcessedAt = SYSUTCDATETIME(),
    ErrorMessage = @ErrorMessage
WHERE EventId = @EventId";

        await using var conn = _connectionFactory.Create();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EventId", eventId);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

