using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Shared, mutable state that every <see cref="IReportAnalyzer"/> reads from and writes into.</summary>
public sealed class AnalysisContext
{
    public StructureDto Structure { get; } = new();
    public DataSourcesDto DataSources { get; } = new();
    public List<Finding> Errors { get; } = [];
    public List<Finding> Warnings { get; } = [];
    public List<Finding> Info { get; } = [];
    public List<Recommendation> Recommendations { get; } = [];
    public ComplexityMetricsDto ComplexityMetrics { get; set; } = new();

    public Action<string, string> Trace { get; init; } = (_, _) => { };
}
