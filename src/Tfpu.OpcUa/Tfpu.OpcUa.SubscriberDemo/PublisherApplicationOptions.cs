using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Tfpu.OpcUa.SubscriberDemo;

public class PublisherApplicationOptions
{
    public const string SectionName = "PublisherApplication";

    [Required]
    public string ApplicationName { get; init; } = string.Empty;

    [Required]
    public string PublisherUrl { get; init; } = string.Empty;

    [Required]
    public string NetworkInterface { get; init; } = string.Empty;

    [Required]
    [ValidateEnumeratedItems]
    public List<PlcOptions> Plcs { get; init; } = [];
}

public class PlcOptions
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public int MaxSubscriptionsPerSession { get; init; }

    [Required]
    public int MaxMonitoredItemsPerSubscription { get; init; }

    [Required]
    [ValidateEnumeratedItems]
    public List<NodeOptions> Nodes { get; init; } = [];
}

public class NodeOptions
{
    [Required]
    public string MonitoringProfileName { get; init; } = string.Empty;

    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public ushort NamespaceIndex { get; set; }

    [Required]
    public string Address { get; init; } = string.Empty;

    [Required]
    public SqlDbType DataType { get; init; }
}