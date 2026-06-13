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

    public double Lambda => ReceivedPerSecond;
    public double Mu => WrittenPerSecond;
    public double Rho => Mu <= 0 ? 0 : Lambda / Mu;
    public bool IsOverloaded => Rho > 1.0;
}
