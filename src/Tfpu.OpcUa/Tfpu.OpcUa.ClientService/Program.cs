using System.Threading.Channels;
using Tfpu.OpcUa.ClientService;
using Tfpu.OpcUa.ClientService.Grpc;
using Tfpu.OpcUa.ClientService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<ClientService>();

// Options
builder.Services.AddOptions<ClientApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ClientApplicationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Channels
builder.Services.AddSingleton(_ =>
    Channel.CreateBounded<CommunicationService.DataChangeEvent>(
        new BoundedChannelOptions(100_000)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        }));

builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<CommunicationService.DataChangeEvent>>().Writer);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<CommunicationService.DataChangeEvent>>().Reader);

builder.Services.AddSingleton(_ =>
    Channel.CreateBounded<MessageProcessingService.SqlCommandBulk>(
        new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        }));

builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<MessageProcessingService.SqlCommandBulk>>().Writer);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<MessageProcessingService.SqlCommandBulk>>().Reader);

//DI
builder.Services.AddSingleton<CommunicationService>();
builder.Services.AddSingleton<MessageProcessingService>();
builder.Services.AddSingleton<WritingService>();

builder.Services.AddSingleton<DashboardSnapshotProvider>();
builder.Services.AddSingleton<RuntimeDashboardGrpcService>();

builder.Services.AddHostedService<ClientService>();

//TEST
builder.Services.AddHostedService<GrpcNamedPipeHostService>();

var host = builder.Build();
host.Run();
