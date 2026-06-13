using ApexCharts;
using MudBlazor.Services;
using Tfpu.OpcUa.Web.Components;
using Tfpu.OpcUa.Contracts.Grpc;
using GrpcDotNetNamedPipes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddApexCharts();

builder.Services.AddSingleton(_ =>
{
    var channel = new NamedPipeChannel(".", "TfpuOpcUaPipe");
    return new RuntimeDashboard.RuntimeDashboardClient(channel);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
