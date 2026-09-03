namespace Tfpu.OpcUa.ClientService.Models;

public sealed record CommunicationSnapshot(
    string EndpointUrl,
    string Status,
    string SessionStatus,
    string MonitoringStatus,
    DateTime? StartedAt,
    string LastError,
    int SessionCount,
    int SubscriptionCount,
    int MonitoredItemCount,
    long ReceivedTotal,
    long DroppedTotal);

public sealed record ProcessingSnapshot(
    long ProcessedTotal,
    int NotificationQueueLength,
    double LastProcessingLatencyMs);

public sealed record WritingSnapshot(
    long WrittenTotal,
    long FailedTotal,
    long DroppedTotal,
    int WriteQueueLength,
    double LastWriteLatencyMs);
public sealed record PublisherSnapshot(
    bool IsRunning,
    int NodeCount,
    int DataSetCount,
    string Transport,
    string Url);