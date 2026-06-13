using GrpcDotNetNamedPipes;
using Tfpu.OpcUa.Contracts.Grpc;

namespace Tfpu.OpcUa.ClientService.Grpc;

public sealed class GrpcNamedPipeHostService : IHostedService
{
    private readonly ILogger<GrpcNamedPipeHostService> _logger;
    private readonly RuntimeDashboardGrpcService _dashboardService;
    private NamedPipeServer? _server;


    public GrpcNamedPipeHostService(
        ILogger<GrpcNamedPipeHostService> logger,
        RuntimeDashboardGrpcService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server = new NamedPipeServer("TfpuOpcUaPipe");

        RuntimeDashboard.BindService(
            _server.ServiceBinder,
            _dashboardService);
        _server.Start();

        _logger.LogInformation("gRPC named pipe server started. Pipe=TfpuOpcUaPipe");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Kill();
        _logger.LogInformation("gRPC named pipe server stopped.");

        return Task.CompletedTask;
    }
}