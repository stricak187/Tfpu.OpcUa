using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class MetricBlock : ComponentBase
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Outlined.Circle;

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public long? Total { get; set; }

    [Parameter]
    public double Rate { get; set; }

    [Parameter]
    public string Suffix { get; set; } = "/ sec";

    [Parameter]
    public string Accent { get; set; } = "blue";
}
