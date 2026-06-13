using Microsoft.AspNetCore.Components;
using System.Globalization;
using Tfpu.OpcUa.Web.Models;

namespace Tfpu.OpcUa.Web.Components.Pages;

public partial class SvgMultiLineChart : ComponentBase
{
    [Parameter]
    public int Height { get; set; } = 260;

    [Parameter]
    public IReadOnlyList<ChartSeriesModel> Series { get; set; } = [];

    [Parameter]
    public IReadOnlyList<DateTime> Timestamps { get; set; } = [];

    [Parameter]
    public double MaxValue { get; set; } = 100;

    [Parameter]
    public string Unit { get; set; } = "";

    private static string BuildPolyline(
        IReadOnlyList<double> values,
        double maxValue,
        int height)
    {
        if (values.Count == 0)
        {
            return "";
        }

        const double left = 72;
        const double right = 880;
        const double top = 18;
        var bottom = height - 38.0;

        var safeMax = Math.Max(maxValue, 1);
        var width = right - left;
        var graphHeight = bottom - top;
        var points = new List<string>(values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1
                ? right
                : left + i * (width / (values.Count - 1));

            var normalized = Math.Clamp(values[i] / safeMax, 0, 1);
            var y = bottom - normalized * graphHeight;

            points.Add($"{ToInv(x)},{ToInv(y)}");
        }

        return string.Join(" ", points);
    }

    private static IReadOnlyList<int> GetTimeLabelIndices(int count)
    {
        if (count <= 1)
        {
            return [0];
        }

        if (count == 2)
        {
            return [0, 1];
        }

        return [0, (count - 1) / 2, count - 1];
    }

    private static string GetAnchor(int index, int count) =>
        index == 0 ? "start" :
        index == count - 1 ? "end" :
        "middle";

    private static string FormatValue(double value) =>
        value switch
        {
            >= 1_000_000 => $"{value / 1_000_000:0.#}M",
            >= 1_000 => $"{value / 1_000:0.#}k",
            >= 10 => value.ToString("0", CultureInfo.InvariantCulture),
            _ => value.ToString("0.#", CultureInfo.InvariantCulture)
        };

    private static string ToInv(double value) =>
        value.ToString("F1", CultureInfo.InvariantCulture);

    private static RenderFragment SvgLabel(
        double x,
        double y,
        string anchor,
        string value) => builder =>
        {
            builder.OpenElement(0, "text");
            builder.AddAttribute(1, "x", ToInv(x));
            builder.AddAttribute(2, "y", ToInv(y));
            builder.AddAttribute(3, "text-anchor", anchor);
            builder.AddAttribute(4, "fill", "#8b95a7");
            builder.AddAttribute(5, "font-size", "11");
            builder.AddAttribute(6, "font-family", "inherit");
            builder.AddContent(5, value);
            builder.CloseElement();
        };
}
