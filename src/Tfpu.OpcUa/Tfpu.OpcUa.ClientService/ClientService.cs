using Tfpu.OpcUa.ClientService.Services;

namespace Tfpu.OpcUa.ClientService;

public sealed class ClientService : BackgroundService
{
    private readonly CommunicationService _communicationService;
    private readonly MessageProcessingService _messageProcessingService;
    private readonly WritingService _writingService;
    private readonly PublisherService _publisherService;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        CommunicationService communicationService,
        MessageProcessingService messageProcessingService,
        WritingService writingService,
        PublisherService publisherService,
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService;
        _messageProcessingService = messageProcessingService;
        _writingService = writingService;
        _publisherService = publisherService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TFPU OPC UA ClientService starting.");

        var processingTask = _messageProcessingService.StartAsync(stoppingToken);
        var writingTask = _writingService.StartAsync(stoppingToken);

        try
        {
            await _communicationService.ConfigureMonitoringAsync(stoppingToken);
            await _communicationService.UpdatePublishingAsync(true, stoppingToken);

            _publisherService.Start();

            _logger.LogInformation("TFPU OPC UA ClientService started.");

            await Task.WhenAll(processingTask, writingTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TFPU OPC UA ClientService cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TFPU OPC UA ClientService failed.");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TFPU OPC UA ClientService stopping...");

        _publisherService.Stop();
        await _communicationService.CloseAllSessionsAsync(cancellationToken);
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("TFPU OPC UA ClientService stopped.");
    }
}