using Microsoft.AspNetCore.Components;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class StatusCard : ComponentBase
{
    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public string Sub { get; set; } = "";

    [Parameter]
    public string Accent { get; set; } = "blue";
}
