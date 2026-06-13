using Microsoft.AspNetCore.Components;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class Sparkline : ComponentBase
{
    [Parameter]
    public IReadOnlyList<double> Points { get; set; } = [];

    [Parameter]
    public double Max { get; set; } = 100;

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public string Accent { get; set; } = "";
}
