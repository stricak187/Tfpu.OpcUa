using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using System.Threading.Channels;
using Tfpu.OpcUa.ClientService.Models;
using static Tfpu.OpcUa.ClientService.Services.CommunicationService;

namespace Tfpu.OpcUa.ClientService.Services;

public class MessageProcessingService
{
    private readonly ClientApplicationOptions _clientApplicationOptions;
    private readonly SqlCommand _baseSqlCommand;
    private readonly ChannelReader<DataChangeEvent> _notificationChannelReader;
    private readonly ChannelWriter<SqlCommandBulk> _commandChannelWriter;
    private readonly WritingService _writingService;

    private const int workersCount = 16;
    private const int batchSize = 1000;

    // Metrics
    private long _processedTotal;
    private int _notificationQueueLength;
    private long _lastProcessingLatencyMsBits;

    public MessageProcessingService(
        IOptions<ClientApplicationOptions> clientApplicationOptions,
        ChannelReader<DataChangeEvent> notificationChannelReader,
        ChannelWriter<SqlCommandBulk> commandChannelWriter,
        WritingService writingService)
    {
        _clientApplicationOptions = clientApplicationOptions.Value;
        _notificationChannelReader = notificationChannelReader;
        _commandChannelWriter = commandChannelWriter;
        _writingService = writingService;

        _baseSqlCommand = GetBaseSqlCommandsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public record SqlCommandBulk(List<SqlCommand> SqlCommands);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(workersCount);
        for (int i = 0; i < workersCount; i++)
        {
            int workerId = i;
            tasks.Add(Task.Run(async () => await PayloaderJob(workerId, cancellationToken), cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    protected async Task PayloaderJob(int workerId, CancellationToken cancellationToken)
    {
        var batch = new List<DataChangeEvent>(batchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            batch.Clear();

            var processingStartedAt = DateTime.UtcNow;

            var firstNotification = await _notificationChannelReader.ReadAsync(cancellationToken);
            Interlocked.Decrement(ref _notificationQueueLength);
            batch.Add(firstNotification);

            while (batch.Count < batchSize && _notificationChannelReader.TryRead(out var nextNotification))
            {
                Interlocked.Decrement(ref _notificationQueueLength);
                batch.Add(nextNotification);
            }

            var sqlCommands = new List<SqlCommand>(batch.Count);

            foreach (var notification in batch)
            {
                var cmd = _baseSqlCommand.Clone();

                cmd.Parameters["@p_DisplayName"].Value = notification.DisplayName;
                cmd.Parameters["@p_NodeId"].Value = notification.NodeId;
                cmd.Parameters["@p_Value"].Value = notification.Value;
                cmd.Parameters["@p_StatusCode"].Value = notification.StatusCode.ToString();
                cmd.Parameters["@p_SourceTimestamp"].Value = notification.SourceTimestamp;
                cmd.Parameters["@p_ReceivedAt"].Value = notification.ReceivedAt;

                sqlCommands.Add(cmd);
            }

            Interlocked.Add(ref _processedTotal, batch.Count);

            var processingLatencyMs = (DateTime.UtcNow - processingStartedAt).TotalMilliseconds;
            Interlocked.Exchange(
                ref _lastProcessingLatencyMsBits,
                BitConverter.DoubleToInt64Bits(processingLatencyMs));

            _writingService.CommandBulkEnqueued(sqlCommands.Count);
            await _commandChannelWriter.WriteAsync(new SqlCommandBulk(sqlCommands), cancellationToken);
        }
    }

    public ProcessingSnapshot GetSnapshot()
    {
        return new ProcessingSnapshot(
            ProcessedTotal: Interlocked.Read(ref _processedTotal),
            NotificationQueueLength: Volatile.Read(ref _notificationQueueLength),
            LastProcessingLatencyMs: BitConverter.Int64BitsToDouble(
                Interlocked.Read(ref _lastProcessingLatencyMsBits)));
    }

    // Helpers
    public void NotificationEnqueued() =>  Interlocked.Increment(ref _notificationQueueLength);
    
    private async Task<SqlCommand> GetBaseSqlCommandsAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_clientApplicationOptions.DatabaseConnection);
        await connection.OpenAsync(cancellationToken);

        string sql = @"
        INSERT INTO [TfpuOpcUa].[dbo].[DataChangeEvent] ([DisplayName], [NodeId], [Value], [StatusCode], [SourceTimestamp], [ReceivedAt])
        VALUES (@p_DisplayName, @p_NodeId, @p_Value, @p_StatusCode, @p_SourceTimestamp, @p_ReceivedAt);";

        var cmd = new SqlCommand(sql, connection);

        cmd.Parameters.Add("@p_DisplayName", SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@p_NodeId", SqlDbType.NVarChar, 500);
        cmd.Parameters.Add("@p_Value", SqlDbType.Float);
        cmd.Parameters.Add("@p_StatusCode", SqlDbType.NVarChar, 100);
        cmd.Parameters.Add("@p_SourceTimestamp", SqlDbType.DateTime2, 7);
        cmd.Parameters.Add("@p_ReceivedAt", SqlDbType.DateTime2, 7);

        await cmd.PrepareAsync(cancellationToken);

        return cmd;
    }
}
