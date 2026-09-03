using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using System.Threading.Channels;
using Tfpu.OpcUa.ClientService.Models;
using static Tfpu.OpcUa.ClientService.Services.MessageProcessingService;

namespace Tfpu.OpcUa.ClientService.Services;

public class WritingService
{
    private readonly ILogger<WritingService> _logger;
    private readonly ClientApplicationOptions _clientApplicationOptions;
    private readonly ChannelReader<SqlCommandBulk> _commandChannelReader;

    private const int databaseReconnectAttemptCount = 5;
    private const int workersCount = 16;
    private readonly SqlConnection[] _connections = new SqlConnection[workersCount];

    // Metrics
    private long _writtenTotal;
    private long _failedTotal;
    private long _droppedTotal;
    private int _writeQueueLength;
    private double _lastWriteLatencyMs;
    private readonly object _metricsLock = new();

    public WritingService(
        ILogger<WritingService> logger,
        IOptions<ClientApplicationOptions> clientApplicationOptions,
        ChannelReader<SqlCommandBulk> commandChannelReader)
    {
        _logger = logger;
        _clientApplicationOptions = clientApplicationOptions.Value;
        _commandChannelReader = commandChannelReader;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(workersCount);

        for (int i = 0; i < workersCount; i++)
        {
            _connections[i] = new SqlConnection(_clientApplicationOptions.DatabaseConnection + $";Connection Timeout=15;ConnectRetryCount={databaseReconnectAttemptCount}");
            await _connections[i].OpenAsync(cancellationToken);
        }

        try
        {
            for (int i = 0; i < workersCount; i++)
            {
                int workerId = i;
                tasks.Add(Task.Run(async () =>
                {
                    await DbWriterJob(workerId, cancellationToken);
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        finally
        {
            var disposeTasks = _connections
                .Where(x => x != null)
                .Select(async conn =>
                {
                    try
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            await conn.CloseAsync();
                        }

                        await conn.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while disposing the database connection.");
                    }
                });

            await Task.WhenAll(disposeTasks);
        }
    }

    private async Task DbWriterJob(int workerId, CancellationToken cancellationToken)
    {
        var connection = _connections[workerId];
        while (!cancellationToken.IsCancellationRequested)
        {
            SqlCommandBulk? sqlCommandBulk;
            try
            {
                if (_commandChannelReader.Count == 0)
                {
                    // No commands to process, wait a bit before checking again
                }

                sqlCommandBulk = await _commandChannelReader.ReadAsync(cancellationToken);
                Interlocked.Add(ref _writeQueueLength, -sqlCommandBulk.SqlCommands.Count);

                var writeStartedAt = DateTime.UtcNow;

                if (connection is null || connection.State != ConnectionState.Open)
                {
                    _connections[workerId] = TryConnect(_clientApplicationOptions.DatabaseConnection, 15)!;
                    connection = _connections[workerId];

                    if (connection is null)
                    {
                        _logger.LogError("Failed to reconnect to the database after multiple attempts.");
                        Interlocked.Add(ref _droppedTotal, sqlCommandBulk.SqlCommands.Count);
                        continue;
                    }
                }

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var sqlCommand in sqlCommandBulk.SqlCommands)
                    {
                        sqlCommand.Connection = connection;
                        sqlCommand.Transaction = transaction;
                        await sqlCommand.ExecuteNonQueryAsync(cancellationToken);
                        sqlCommand.Dispose();
                    }

                    transaction.Commit();

                    Interlocked.Add(ref _writtenTotal, sqlCommandBulk.SqlCommands.Count);
                    lock (_metricsLock)
                    {
                        _lastWriteLatencyMs = (DateTime.UtcNow - writeStartedAt).TotalMilliseconds;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                        _logger.LogError("Error occurred while rolling back the database transaction.");
                    }

                    Interlocked.Add(ref _failedTotal, sqlCommandBulk.SqlCommands.Count);
                    _logger.LogError(ex, "Error occurred while writing to the database.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred.");
            }
        }
    }

    public WritingSnapshot GetSnapshot()
    {
        double latency;

        lock (_metricsLock)
        {
            latency = _lastWriteLatencyMs;
        }

        return new WritingSnapshot(
            WrittenTotal: Interlocked.Read(ref _writtenTotal),
            FailedTotal: Interlocked.Read(ref _failedTotal),
            DroppedTotal: Interlocked.Read(ref _droppedTotal),
            WriteQueueLength: Volatile.Read(ref _writeQueueLength),
            LastWriteLatencyMs: latency);
    }

    public void CommandBulkEnqueued(int sqlCommandsCount) => Interlocked.Add(ref _writeQueueLength, sqlCommandsCount);

    private SqlConnection? TryConnect(string connectionString, int connectionTimeout = 5)
    {
        for (int i = 0; i < databaseReconnectAttemptCount; ++i)
        {
            try
            {
                var newConnection = new SqlConnection($"{connectionString};Connection Timeout={connectionTimeout}");
                newConnection.Open();

                return newConnection;
            }
            catch
            {
                _logger.LogDebug("Failed to connect to the database.");
            }
        }

        return null;
    }
}