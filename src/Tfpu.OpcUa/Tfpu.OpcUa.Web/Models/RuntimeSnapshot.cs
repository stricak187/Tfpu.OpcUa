namespace Tfpu.OpcUa.Web.Models;

public class RuntimeSnapshot
{
    public long ReceivedTotal { get; init; }
    public long ProcessedTotal { get; init; }
    public long WrittenTotal { get; init; }
    public long DroppedTotal { get; init; }
    public long FailedTotal { get; init; }

    public double ReceivedPerSecond { get; init; }
    public double ProcessedPerSecond { get; init; }
    public double WrittenPerSecond { get; init; }

    public int NotificationQueueLength { get; init; }
    public int WriteQueueLength { get; init; }

    public double ProcessingLatencyMs { get; init; }
    public double EndToEndLatencyMs { get; init; }
    public double MaxLatencyMs { get; init; }

    public double Lambda { get; init; }
    public double Mu { get; init; }
    public double Rho { get; init; }
    public bool IsOverloaded { get; init; }

    public bool IsRunning { get; set; }
    public int NodeCount { get; set; }
    public int DataSetCount { get; set; }
    public string Transport { get; set; }
    public string Url { get; set; }
}
