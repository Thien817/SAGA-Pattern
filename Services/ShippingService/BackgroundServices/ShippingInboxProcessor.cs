using Microsoft.Data.SqlClient;
using ShippingService.Infrastructure;
using ShippingService.Services;

namespace ShippingService.BackgroundServices;

public sealed class ShippingInboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqlConnectionFactory _connectionFactory;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const string ConsumerName = "ShippingService";
    private const int BatchSize = 10;

    public ShippingInboxProcessor(IServiceScopeFactory scopeFactory, SqlConnectionFactory connectionFactory)
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

    private async Task HandleOneAsync(
        (int EventId, string EventType, string PayloadJson) inboxEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var shippingService = scope.ServiceProvider.GetRequiredService<IShippingService>();

            await shippingService.HandleInboxEventAsync(
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

    private async Task MarkProcessedAsync(int eventId, CancellationToken cancellationToken)
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

    private async Task MarkFailedAsync(int eventId, string errorMessage, CancellationToken cancellationToken)
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
