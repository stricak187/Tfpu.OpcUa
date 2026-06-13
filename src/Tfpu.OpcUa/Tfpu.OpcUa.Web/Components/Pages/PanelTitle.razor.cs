using Microsoft.AspNetCore.Components;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class PanelTitle : ComponentBase
{
    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public string Subtitle { get; set; } = "";
}
