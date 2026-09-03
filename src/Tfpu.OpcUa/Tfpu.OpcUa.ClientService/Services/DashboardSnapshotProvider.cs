using Tfpu.OpcUa.Contracts.Grpc;

namespace Tfpu.OpcUa.ClientService.Services;

public sealed class DashboardSnapshotProvider
{
    private readonly CommunicationService _communication;
    private readonly MessageProcessingService _processing;
    private readonly WritingService _writing;
    private readonly PublisherService _publisher;

    private readonly object _lock = new();

    private DateTime _lastAt = DateTime.UtcNow;
    private long _lastReceived;
    private long _lastProcessed;
    private long _lastWritten;
    private double _maxObservedLatencyMs;

    public DashboardSnapshotProvider(
        CommunicationService communication,
        MessageProcessingService processing,
        WritingService writing,
        PublisherService publisher)
    {
        _communication = communication;
        _processing = processing;
        _writing = writing;
        _publisher = publisher;
    }

    public DashboardSnapshotReply GetSnapshot()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var elapsed = Math.Max((now - _lastAt).TotalSeconds, 0.001);

            var comm = _communication.GetSnapshot();
            var proc = _processing.GetSnapshot();
            var write = _writing.GetSnapshot();

            var receivedRate = (comm.ReceivedTotal - _lastReceived) / elapsed;
            var processedRate = (proc.ProcessedTotal - _lastProcessed) / elapsed;
            var writtenRate = (write.WrittenTotal - _lastWritten) / elapsed;

            var currentMaxLatency = Math.Max(proc.LastProcessingLatencyMs, write.LastWriteLatencyMs);
            _maxObservedLatencyMs = Math.Max(_maxObservedLatencyMs, currentMaxLatency);

            _lastAt = now;
            _lastReceived = comm.ReceivedTotal;
            _lastProcessed = proc.ProcessedTotal;
            _lastWritten = write.WrittenTotal;

            var rho = processedRate <= 0 ? 0 : receivedRate / processedRate;

            var publisher = _publisher.GetSnapshot();

            var reply = new DashboardSnapshotReply
            {
                EndpointUrl = comm.EndpointUrl,
                Status = comm.Status,
                SessionStatus = comm.SessionStatus,
                MonitoringStatus = comm.MonitoringStatus,
                StartedAt = comm.StartedAt?.ToString("HH:mm:ss") ?? "-",
                Uptime = comm.StartedAt is null
                    ? "-"
                    : (DateTime.Now - comm.StartedAt.Value).ToString(@"hh\:mm\:ss"),
                LastError = comm.LastError,

                SubscriptionCount = comm.SubscriptionCount,
                MonitoredItemCount = comm.MonitoredItemCount,

                ReceivedTotal = comm.ReceivedTotal,
                ProcessedTotal = proc.ProcessedTotal,
                WrittenTotal = write.WrittenTotal,
                DroppedTotal = comm.DroppedTotal + write.DroppedTotal,
                FailedTotal = write.FailedTotal,

                ReceivedPerSecond = receivedRate,
                ProcessedPerSecond = processedRate,
                WrittenPerSecond = writtenRate,

                NotificationQueueLength = proc.NotificationQueueLength,
                WriteQueueLength = write.WriteQueueLength,

                ProcessingLatencyMs = proc.LastProcessingLatencyMs,
                EndToEndLatencyMs = write.LastWriteLatencyMs,
                MaxLatencyMs = _maxObservedLatencyMs,

                Lambda = receivedRate,
                Mu = processedRate,
                Rho = rho,
                IsOverloaded = rho > 1.0,

                PubSubRunning = publisher.IsRunning,
                PubSubNodeCount = publisher.NodeCount,
                PubSubDataSetCount = publisher.DataSetCount,
                PubSubTransport = publisher.Transport,
                PubSubUrl = publisher.Url
            };

            var communicationSnapshot = comm;

            return reply;
        }
    }
}