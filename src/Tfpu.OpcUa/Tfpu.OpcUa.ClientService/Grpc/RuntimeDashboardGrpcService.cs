using Grpc.Core;
using Tfpu.OpcUa.ClientService.Services;
using Tfpu.OpcUa.Contracts.Grpc;

namespace Tfpu.OpcUa.ClientService.Grpc;

public sealed class RuntimeDashboardGrpcService : RuntimeDashboard.RuntimeDashboardBase
{
    private readonly DashboardSnapshotProvider _snapshotProvider;

    public RuntimeDashboardGrpcService(DashboardSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public override Task<DashboardSnapshotReply> GetDashboardSnapshot(
        DashboardSnapshotRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(_snapshotProvider.GetSnapshot());
    }
}