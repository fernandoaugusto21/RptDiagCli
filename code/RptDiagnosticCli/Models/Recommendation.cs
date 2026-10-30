namespace RptDiagnosticCli.Models;

public sealed class Recommendation
{
    public string Priority { get; init; } = "low";
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
}
