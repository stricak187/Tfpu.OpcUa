using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using Opc.Ua.Client;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Threading.Channels;
using Tfpu.OpcUa.ClientService.Models;
using static System.Collections.Specialized.BitVector32;
using static Tfpu.OpcUa.ClientService.Services.MessageProcessingService;

namespace Tfpu.OpcUa.ClientService.Services;

public class CommunicationService
{
    private readonly ILogger<CommunicationService> _logger;
    private readonly ClientApplicationOptions _clientApplicationOptions;
    private readonly ApplicationConfiguration _applicationConfiguration;
    private readonly ITelemetryContext _telemetry;
    private readonly ChannelWriter<DataChangeEvent> _notificationChannelWriter;
    private readonly MessageProcessingService _messageProcessingService;

    private readonly Dictionary<ISession, object> _sessionLocks = new();
    private readonly Dictionary<ISession, SessionReconnectHandler?> _reconnectHandlers = new();

    private const bool validateNodes = true;

    // Metrics
    private long _receivedTotal;
    private long _droppedTotal;

    private DateTime? _startedAt;
    private string _lastError = "-";
    private bool _monitoringConfigured;
    private bool _publishingEnabled;

    public CommunicationService(
        ILogger<CommunicationService> logger,
        IOptions<ClientApplicationOptions> clientApplicationOptions,
        ChannelWriter<DataChangeEvent> notificationChannelWriter,
        MessageProcessingService messageProcessingService)
    {
        _logger = logger;
        _clientApplicationOptions = clientApplicationOptions.Value;
        _applicationConfiguration = GetApplicationConfiguration().GetAwaiter().GetResult();
        _telemetry = DefaultTelemetry.Create(logging =>
        {
            logging.AddConsole();
            // TODO: Serilog sink to sqlite
        });
        _notificationChannelWriter = notificationChannelWriter;
        _messageProcessingService = messageProcessingService;
    }

    public async Task ConfigureMonitoringAsync(CancellationToken cancellationToken)
    {
        foreach (var plc in _clientApplicationOptions.Plcs)
        {
            var session = await CreateSessionAsync(plc.Name, cancellationToken);
            var nodeProfileGroups = plc.Nodes.GroupBy(node => node.MonitoringProfileName);

            foreach (var nodeProfileGroup in nodeProfileGroups)
            {
                var profile = _clientApplicationOptions.MonitoringProfiles.Single(p => p.Name == nodeProfileGroup.Key);

                if (session.Subscriptions.Count() >= plc.MaxSubscriptionsPerSession)
                {
                    session = await CreateSessionAsync(plc.Name, cancellationToken);
                }
                var subscription = await CreateSubscriptionAsync(session, profile, cancellationToken);

                foreach (var node in nodeProfileGroup.ToList())
                {
                    if (subscription.MonitoredItemCount >= plc.MaxMonitoredItemsPerSubscription)
                    {
                        await subscription.ApplyChangesAsync(cancellationToken);
                        if (session.Subscriptions.Count() >= plc.MaxSubscriptionsPerSession)
                        {
                            session = await CreateSessionAsync(plc.Name, cancellationToken);
                        }
                        subscription = await CreateSubscriptionAsync(session, profile, cancellationToken);
                    }

                    var monitoredItem = await CreateMonitoredItemAsync(session, profile, node, cancellationToken);

                    monitoredItem.Notification += OnMonitoredItemNotificationReceived;
                    subscription.AddItem(monitoredItem);
                }

                await subscription.ApplyChangesAsync(cancellationToken);
            }
        }
        _startedAt ??=  DateTime.Now;
        _monitoringConfigured = true;
    }

    public async Task EnablePublishingAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessionLocks.Keys)
        {
            foreach (var subscription in session.Subscriptions)
            {
                subscription.PublishingEnabled = true;
                await subscription.SetPublishingModeAsync(true, cancellationToken);
                await subscription.ApplyChangesAsync(cancellationToken);
            }
        }
        _publishingEnabled = true;
    }

    protected virtual void OnMonitoredItemNotificationReceived(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification changeNotification)
        {
            // TODO: _logger.LogWarning("Unsupported notification type");
            return;
        }

        var dataChangeEvent = new DataChangeEvent
        {
            DisplayName = item.DisplayName,
            NodeName = item.DisplayName,
            NodeId = item.StartNodeId.ToString(),
            StatusCode = changeNotification.Value.StatusCode,
            Value = changeNotification.Value.WrappedValue.Value,
            SourceTimestamp = changeNotification.Value.SourceTimestamp,
            ReceivedAt = DateTime.UtcNow
        };

        Interlocked.Increment(ref _receivedTotal);

        var written = _notificationChannelWriter.TryWrite(dataChangeEvent);
        if (!written)
        {
            Interlocked.Increment(ref _droppedTotal);
        }
        else
        {
            _messageProcessingService.NotificationEnqueued();
        }
    }

    public CommunicationSnapshot GetSnapshot()
    {
        var sessions = _sessionLocks.Keys.ToList();

        return new CommunicationSnapshot(
            EndpointUrl: _clientApplicationOptions.Server.EndpointUrl,
            Status: _publishingEnabled ? "Monitoring" :
                    _monitoringConfigured ? "Configured" : "Idle",
            SessionStatus: sessions.Any(s => s.Connected) ? "Connected" : "Disconnected",
            MonitoringStatus: _publishingEnabled ? "Active" : "Inactive",
            StartedAt: _startedAt,
            LastError: _lastError,
            SessionCount: sessions.Count,
            SubscriptionCount: sessions.Sum(s => s.Subscriptions.Count()),
            MonitoredItemCount: (int)sessions.Sum(s => s.Subscriptions.Sum(sub => sub.MonitoredItemCount)),
            ReceivedTotal: Interlocked.Read(ref _receivedTotal),
            DroppedTotal: Interlocked.Read(ref _droppedTotal));
    }

    // Helpers
    private async Task<ApplicationConfiguration> GetApplicationConfiguration()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "pki");
        var config = new ApplicationConfiguration
        {
            ApplicationName = _clientApplicationOptions.ApplicationName,
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new() { StoreType = "Directory", StorePath = Path.Combine(basePath, "own"), SubjectName = $"CN={_clientApplicationOptions.ApplicationName}" },
                TrustedIssuerCertificates = new() { StoreType = "Directory", StorePath = Path.Combine(basePath, "issuer") },
                TrustedPeerCertificates = new() { StoreType = "Directory", StorePath = Path.Combine(basePath, "peer") },
                RejectedCertificateStore = new() { StoreType = "Directory", StorePath = Path.Combine(basePath, "rejected") },
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true,
                MinimumCertificateKeySize = 2048
            },
            TransportConfigurations = [],
            TransportQuotas = new()
            {
                OperationTimeout = 15000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 600000,
                SecurityTokenLifetime = 3600000
            },
            ClientConfiguration = new()
            {
                DefaultSessionTimeout = _clientApplicationOptions.SessionTimeout
            }
        };
        await config.ValidateAsync(ApplicationType.Client);

        return config;
    }

    private async Task<ConfiguredEndpoint> GetConfiguredEndpoint(CancellationToken cancellationToken)
    {
        var endpointDescription = await CoreClientUtils.SelectEndpointAsync(
            application: _applicationConfiguration,
            discoveryUrl: _clientApplicationOptions.Server.EndpointUrl,
            useSecurity: false,
            telemetry: _telemetry,
            cancellationToken);

        return new ConfiguredEndpoint(null, endpointDescription, EndpointConfiguration.Create(_applicationConfiguration));
    }

    // Session
    private async Task<ISession> CreateSessionAsync(string plcName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var configuredEndpoint = await GetConfiguredEndpoint(cancellationToken);

                var userIdentity = _clientApplicationOptions.Server.UseAnonymous
                    ? new UserIdentity()
                    : new UserIdentity(
                        username: _clientApplicationOptions.Server.Credentials.Username,
                        password: _clientApplicationOptions.Server.Credentials.Password);

                var sessionName = $"{_clientApplicationOptions.ApplicationName}_{plcName}_Session_{_sessionLocks.Count + 1}";

                var sessionFactory = new DefaultSessionFactory(_telemetry);
                var session = await sessionFactory.CreateAsync(
                    configuration: _applicationConfiguration,
                    endpoint: configuredEndpoint,
                    updateBeforeConnect: false,
                    sessionName: sessionName,
                    sessionTimeout: (uint)_clientApplicationOptions.SessionTimeout,
                    identity: userIdentity,
                    preferredLocales: null,
                    cancellationToken);

                _sessionLocks[session] = new object();
                session.KeepAlive += OnKeepAlive;

                return session;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "An error occurred while creating a session.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Session creation was canceled.");
            return null!;
        }

        throw new OperationCanceledException("Session creation was canceled.", cancellationToken);
    }

    private void OnKeepAlive(ISession s, KeepAliveEventArgs e)
    {
        var session = (Session)s;

        if (e.Status != null && ServiceResult.IsBad(e.Status))
        {
            var sync = _sessionLocks[session];

            lock (sync)
            {
                if (_reconnectHandlers.TryGetValue(session, out var existing) && existing != null)
                {
                    return;
                }

                _logger.LogDebug("KeepAlive error: {Status}, Session ID: {sessionId}", e.Status, session.SessionId);

                var handler = new SessionReconnectHandler(_telemetry);
                _reconnectHandlers[session] = handler;

                handler.BeginReconnect(
                    session,
                    5000,
                    OnReconnectComplete);
            }
        }
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        var handler = sender as SessionReconnectHandler;
        if (handler == null || handler.Session == null)
        {
            return;
        }

        var newSession = handler.Session;
        newSession.KeepAlive += OnKeepAlive;

        var oldSession = _reconnectHandlers
            .Where(kvp => kvp.Value == handler)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        if (oldSession == null)
        {
            return;
        }
        oldSession.KeepAlive -= OnKeepAlive;

        var sync = _sessionLocks[oldSession];

        lock (sync)
        {
            handler.Dispose();
            _sessionLocks.Remove(oldSession);
            _logger.LogInformation("Reconnected to OPC UA server, SessionId: {Id}", newSession.SessionId);
        }
        _sessionLocks.Remove(oldSession);
        _sessionLocks[newSession] = sync;
    }

    // Subscriptions & MonitoredItems
    private async Task<Subscription> CreateSubscriptionAsync(ISession session, MonitoringProfileOptions monitoringProfile, CancellationToken cancellationToken)
    {
        var subscription = new Subscription(session.DefaultSubscription)
        {
            DisplayName = $"{monitoringProfile.Name}_{session.Subscriptions.Count() + 1}",
            PublishingInterval = monitoringProfile.PublishingInterval,
            PublishingEnabled = false,
        };

        session.AddSubscription(subscription);
        await subscription.CreateAsync(cancellationToken);

        return subscription;
    }

    private async Task<bool> ValidateNodeReadability(ISession session, string nodeName, NodeId nodeId, CancellationToken cancellationToken)
    {
        try
        {
            var readNode = await session.ReadNodeAsync(nodeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to read '{NodeName}' Node ({NodeId}).",
                nodeName,
                nodeId);
            return false;
        }

        return true;
    }

    private async Task<MonitoredItem> CreateMonitoredItemAsync(ISession session, MonitoringProfileOptions monitoringProfile, NodeOptions node, CancellationToken cancellationToken)
    {
        var nodeId = new NodeId(node.Address, node.NamespaceIndex);
        if (validateNodes)
        {
            await ValidateNodeReadability(session, node.Name, nodeId, cancellationToken);
        }

        return new MonitoredItem(_telemetry)
        {
            StartNodeId = nodeId,
            AttributeId = Attributes.Value,
            DisplayName = node.Name,
            SamplingInterval = monitoringProfile.SamplingInterval,
            QueueSize = 1000,
            DiscardOldest = true,
            MonitoringMode = MonitoringMode.Reporting,
            Handle = node.Name,
            Filter = monitoringProfile.Deadband.HasValue && node.DataType.IsNumeric()
                ? new DataChangeFilter()
                {
                    Trigger = DataChangeTrigger.StatusValue,
                    DeadbandType = (uint)DeadbandType.Absolute,
                    DeadbandValue = monitoringProfile.Deadband!.Value,
                }
                : new DataChangeFilter()
        };
    }

    // DTOs
    public sealed class DataChangeEvent
    {
        public string NodeId { get; init; } = string.Empty;

        public string NodeName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public StatusCode StatusCode { get; init; }

        public object? Value { get; init; }

        public string? ValueText { get; init; }

        public string? DataType { get; init; }

        public DateTime SourceTimestamp { get; init; }

        public DateTime ReceivedAt { get; init; }
    }
}