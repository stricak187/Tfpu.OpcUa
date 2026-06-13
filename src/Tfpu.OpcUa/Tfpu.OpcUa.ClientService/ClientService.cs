using Tfpu.OpcUa.ClientService.Services;

namespace Tfpu.OpcUa.ClientService;

public sealed class ClientService : BackgroundService
{
    private readonly CommunicationService _communicationService;
    private readonly MessageProcessingService _messageProcessingService;
    private readonly WritingService _writingService;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        CommunicationService communicationService,
        MessageProcessingService messageProcessingService,
        WritingService writingService,
        ILogger<ClientService> logger)
    {
        _communicationService = communicationService;
        _messageProcessingService = messageProcessingService;
        _writingService = writingService;
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
            await _communicationService.EnablePublishingAsync(stoppingToken);

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
        _logger.LogInformation("TFPU OPC UA ClientService stopping.");

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("TFPU OPC UA ClientService stopped.");
    }
}