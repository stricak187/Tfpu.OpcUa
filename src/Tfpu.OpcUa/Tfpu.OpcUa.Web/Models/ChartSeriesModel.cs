namespace Tfpu.OpcUa.Web.Models;

public sealed record ChartSeriesModel(
    string Name,
    string Color,
    IReadOnlyList<double> Values);