using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Tfpu.OpcUa.ClientService;

public class ClientApplicationOptions
{
    public const string SectionName = "ClientApplication";

    [Required]
    public string ApplicationName { get; init; } = string.Empty;

    [Required]
    public int SessionTimeout { get; init; }

    [Required]
    [ValidateObjectMembers]
    public required ServerOptions Server { get; set; }

    [Required]
    public List<MonitoringProfileOptions> MonitoringProfiles { get; init; } = [];

    [Required]
    public List<PlcOptions> Plcs { get; init; } = [];

    [Required]
    public string DatabaseConnection { get; init; } = string.Empty;
}

// Server
public class ServerOptions
{
    [Required]
    public string EndpointUrl { get; init; } = string.Empty;


    [Required]
    public bool UseAnonymous { get; init; }

    [Required]
    [ValidateObjectMembers]
    public required CredentialsOptions Credentials { get; set; }
}

public class CredentialsOptions
{
    public string Username { get; init; } = string.Empty;

    public byte[] Password { get; init; } = [];
}

// Monitoring setup
public class MonitoringProfileOptions
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public int PublishingInterval { get; set; }

    [Required]
    public int SamplingInterval { get; set; }

    public double? Deadband { get; set; }
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