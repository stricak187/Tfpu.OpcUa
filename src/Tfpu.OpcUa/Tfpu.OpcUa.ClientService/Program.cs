using System.Threading.Channels;
using Serilog;
using Tfpu.OpcUa.ClientService;
using Tfpu.OpcUa.ClientService.Grpc;
using Tfpu.OpcUa.ClientService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<ClientService>();

// Logging
builder.Services.AddSerilog(
    new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .CreateLogger());

// Options
builder.Services.AddOptions<ClientApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ClientApplicationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Channels
builder.Services.AddSingleton(_ =>
    Channel.CreateUnbounded<CommunicationService.DataChangeEvent>(
        new UnboundedChannelOptions()
        {
            SingleReader = false,
            SingleWriter = false
        }));

builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<CommunicationService.DataChangeEvent>>().Writer);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<CommunicationService.DataChangeEvent>>().Reader);

builder.Services.AddSingleton(_ =>
    Channel.CreateUnbounded<MessageProcessingService.SqlCommandBulk>(
        new UnboundedChannelOptions()
        {
            SingleReader = false,
            SingleWriter = false
        }));

builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<MessageProcessingService.SqlCommandBulk>>().Writer);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Channel<MessageProcessingService.SqlCommandBulk>>().Reader);

//DI
builder.Services.AddSingleton<CommunicationService>();
builder.Services.AddSingleton<MessageProcessingService>();
builder.Services.AddSingleton<WritingService>();

builder.Services.AddSingleton<DashboardSnapshotProvider>();
builder.Services.AddSingleton<RuntimeDashboardGrpcService>();

builder.Services.AddSingleton<PublisherService>();

builder.Services.AddHostedService<GrpcNamedPipeHostService>();

var host = builder.Build();
host.Run();
