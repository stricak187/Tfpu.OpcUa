using Grpc.Core;
using Tfpu.OpcUa.ClientService.Services;
using Tfpu.OpcUa.Contracts.Grpc;

namespace Tfpu.OpcUa.ClientService.Grpc;

public sealed class RuntimeDashboardGrpcService : RuntimeDashboard.RuntimeDashboardBase
{
    private readonly DashboardSnapshotProvider _snapshotProvider;
    private readonly CommunicationService _communicationService;
    private readonly PublisherService _publisherService;

    public RuntimeDashboardGrpcService(
        DashboardSnapshotProvider snapshotProvider,
        CommunicationService communicationService,
        PublisherService publisherService)
    {
        _snapshotProvider = snapshotProvider;
        _communicationService = communicationService;
        _publisherService = publisherService;
    }

    public override Task<DashboardSnapshotReply> GetDashboardSnapshot(DashboardSnapshotRequest request, ServerCallContext context)
    {
        return Task.FromResult(_snapshotProvider.GetSnapshot());
    }

    public override async Task<RuntimeCommandReply> EnableOpcUaPublishing(RuntimeCommandRequest request, ServerCallContext context)
    {
        try
        {
            await _communicationService.UpdatePublishingAsync(true, context.CancellationToken);

            return new RuntimeCommandReply
            {
                IsSuccess = true,
                Message = "OPC UA publishing enabled."
            };
        }
        catch (Exception ex)
        {
            return new RuntimeCommandReply
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<RuntimeCommandReply> DisableOpcUaPublishing(RuntimeCommandRequest request, ServerCallContext context)
    {
        try
        {
            await _communicationService.UpdatePublishingAsync(false, context.CancellationToken);

            return new RuntimeCommandReply
            {
                IsSuccess = true,
                Message = "OPC UA publishing disabled."
            };
        }
        catch (Exception ex)
        {
            return new RuntimeCommandReply
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<RuntimeCommandReply> StartPubSub(RuntimeCommandRequest request, ServerCallContext context)
    {
        try
        {
            _publisherService.Start();

            return new RuntimeCommandReply
            {
                IsSuccess = true,
                Message = "OPC UA PubSub publishing started."
            };
        }
        catch (Exception ex)
        {
            return new RuntimeCommandReply
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<RuntimeCommandReply> StopPubSub(RuntimeCommandRequest request, ServerCallContext context)
    {
        try
        {
            _publisherService.Stop();

            return new RuntimeCommandReply
            {
                IsSuccess = true,
                Message = "OPC UA PubSub publishing stopped."
            };
        }
        catch (Exception ex)
        {
            return new RuntimeCommandReply
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }
}