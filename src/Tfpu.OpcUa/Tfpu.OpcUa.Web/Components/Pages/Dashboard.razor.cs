using Microsoft.AspNetCore.Components;
using Tfpu.OpcUa.Contracts.Grpc;
using Tfpu.OpcUa.Web.Models;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class Dashboard : ComponentBase
{
    private DashboardSnapshotReply? _dashboard;

    private bool _commandInProgress;
    private bool IsConnected => _dashboard?.SessionStatus == "Connected";
    private bool IsMonitoringActive => _dashboard?.MonitoringStatus == "Active";
    private bool IsPubSubRunning => _dashboard?.PubSubRunning == true;
    private bool CanEnableMonitoring => !_commandInProgress && IsConnected && !IsMonitoringActive;
    private bool CanDisableMonitoring => !_commandInProgress && IsMonitoringActive;
    private bool CanStartPubSub => !_commandInProgress && !IsPubSubRunning;
    private bool CanStopPubSub => !_commandInProgress && IsPubSubRunning;

    private string PubSubUrl => _dashboard?.PubSubUrl ?? "-";
    private string PubSubTransport => _dashboard?.PubSubTransport ?? "-";
    private int PubSubDataSetCount => _dashboard?.PubSubDataSetCount ?? 0;
    private int PubSubNodeCount => _dashboard?.PubSubNodeCount ?? 0;

    private RuntimeSnapshot _snapshot = new();

    private readonly List<DateTime> _sampleTimes = [];
    private readonly List<double> _received = [];
    private readonly List<double> _processed = [];
    private readonly List<double> _written = [];
    private readonly List<double> _notificationQueue = [];
    private readonly List<double> _writeQueue = [];
    private readonly List<double> _processingLatency = [];
    private readonly List<double> _endToEndLatency = [];
    private readonly List<double> _processingUtilization = [];
    private readonly List<double> _writingUtilization = [];

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private string? _loadError;

    private string EndpointUrl => _dashboard?.EndpointUrl ?? "-";

    private double DropPercent =>
        _snapshot.ReceivedTotal <= 0
            ? 0
            : _snapshot.DroppedTotal * 100.0 / _snapshot.ReceivedTotal;

    private string RuntimeDotClass =>
        _dashboard?.Status == "Monitoring" ? "ok" :
        _dashboard?.Status == "Faulted" ? "error" :
        "warn";

    private string StatusAccent =>
        _dashboard?.Status == "Monitoring" ? "green" :
        _dashboard?.Status == "Faulted" ? "red" :
        "amber";

    private string SessionAccent =>
        _dashboard?.SessionStatus == "Connected" ? "green" : "red";

    private string MonitoringAccent =>
        _dashboard?.MonitoringStatus == "Active" ? "green" : "amber";
    
    private double EventRateMax =>
        Math.Max(10, new[] { _received.DefaultIfEmpty(0).Max(), _processed.DefaultIfEmpty(0).Max(), _written.DefaultIfEmpty(0).Max() }.Max() * 1.2);

    private double QueueMax =>
        Math.Max(10, new[] { _notificationQueue.DefaultIfEmpty(0).Max(), _writeQueue.DefaultIfEmpty(0).Max() }.Max() * 1.2);

    private double LatencyMax =>
        Math.Max(10, new[] { _processingLatency.DefaultIfEmpty(0).Max(), _endToEndLatency.DefaultIfEmpty(0).Max() }.Max() * 1.2);

    private double ProcessingUtilization =>
        _snapshot.ReceivedPerSecond <= 0
            ? 0
            : Math.Min(100,
                _snapshot.ProcessedPerSecond /
                _snapshot.ReceivedPerSecond * 100);

    private double WritingUtilization =>
        _snapshot.ReceivedPerSecond <= 0
            ? 0
            : Math.Min(100,
                _snapshot.WrittenPerSecond /
                _snapshot.ReceivedPerSecond * 100);

    private IReadOnlyList<ChartSeriesModel> EventRateSeries =>
    [
        new("Received/s", "#4d9cff", _received),
        new("Processed/s", "#b077ff", _processed),
        new("Written/s", "#38d07b", _written)
    ];

    private IReadOnlyList<ChartSeriesModel> QueueSeries =>
    [
        new("Notification queue", "#4d9cff", _notificationQueue),
        new("Write queue", "#f4c430", _writeQueue)
    ];

    private IReadOnlyList<ChartSeriesModel> LatencySeries =>
    [
        new("Processing latency", "#b077ff", _processingLatency),
        new("Database write latency", "#f4c430", _endToEndLatency)
    ];

    private IReadOnlyList<ChartSeriesModel> UtilizationSeries =>
    [
        new("Processing", "#b077ff", _processingUtilization),
        new("Writing", "#38d07b", _writingUtilization)
    ];

    protected override async Task OnInitializedAsync()
    {
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        await LoadDashboardSnapshotAsync();

        _ = UpdateLoopAsync(_cts.Token);
    }

    private async Task UpdateLoopAsync(CancellationToken token)
    {
        if (_timer is null)
        {
            return;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(token))
            {
                await LoadDashboardSnapshotAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Exit the loop
        }
        catch (Exception ex)
        {
            // log
        }
    }

    private async Task LoadDashboardSnapshotAsync()
    {
        try
        {
            var reply = await DashboardClient.GetDashboardSnapshotAsync(new DashboardSnapshotRequest());

            _loadError = null;
            _dashboard = reply;

            _snapshot = new RuntimeSnapshot
            {
                ReceivedTotal = reply.ReceivedTotal,
                ProcessedTotal = reply.ProcessedTotal,
                WrittenTotal = reply.WrittenTotal,
                DroppedTotal = reply.DroppedTotal,
                FailedTotal = reply.FailedTotal,

                ReceivedPerSecond = reply.ReceivedPerSecond,
                ProcessedPerSecond = reply.ProcessedPerSecond,
                WrittenPerSecond = reply.WrittenPerSecond,

                NotificationQueueLength = reply.NotificationQueueLength,
                WriteQueueLength = reply.WriteQueueLength,

                ProcessingLatencyMs = reply.ProcessingLatencyMs,
                EndToEndLatencyMs = reply.EndToEndLatencyMs,
                MaxLatencyMs = reply.MaxLatencyMs,

                Lambda = reply.Lambda,
                Mu = reply.Mu,
                Rho = reply.Rho,
                IsOverloaded = reply.IsOverloaded
            };

            Push(_sampleTimes, DateTime.Now, 48);

            Push(_received, reply.ReceivedPerSecond, 48);
            Push(_processed, reply.ProcessedPerSecond, 48);
            Push(_written, reply.WrittenPerSecond, 48);

            Push(_notificationQueue, reply.NotificationQueueLength, 48);
            Push(_writeQueue, reply.WriteQueueLength, 48);

            Push(_processingLatency, reply.ProcessingLatencyMs, 48);
            Push(_endToEndLatency, reply.EndToEndLatencyMs, 48);

            Push(_processingUtilization, ProcessingUtilization, 48);
            Push(_writingUtilization, WritingUtilization, 48);
        }
        catch (Exception ex)
        {
            _loadError = $"Dashboard gRPC read failed: {ex.Message}";
        }
    }

    private static void Push(List<DateTime> list, DateTime value, int max)
    {
        list.Add(value);

        if (list.Count > max)
        {
            list.RemoveAt(0);
        }
    }

    private static void Push(List<double> list, double value, int max)
    {
        list.Add(value);

        if (list.Count > max)
        {
            list.RemoveAt(0);
        }
    }

    private async Task ExecuteMonitoringCommandAsync(Func<Grpc.Core.AsyncUnaryCall<RuntimeCommandReply>> command)
    {
        _commandInProgress = true;

        try
        {
            var reply = await command().ResponseAsync;

            _loadError = reply.IsSuccess
                ? null
                : reply.Message;

            await LoadDashboardSnapshotAsync();
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
        finally
        {
            _commandInProgress = false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _timer?.Dispose();
    }
}