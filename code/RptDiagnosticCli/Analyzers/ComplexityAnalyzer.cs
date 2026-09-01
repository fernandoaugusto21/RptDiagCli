using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Fields;
using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Computes size/complexity metrics from the already-populated context plus the raw report.
///
/// Totals are summed across the whole report tree — main report plus every subreport (see
/// <see cref="SubreportWalker"/>) — so they stay consistent with "structure" and
/// "data_sources", which are built the same way.</summary>
public sealed class ComplexityAnalyzer : IReportAnalyzer
{
    public void Analyze(ReportDefinition report, AnalysisContext context)
    {
        var nodes = SubreportWalker.Walk(report).Select(n => n.Report).ToList();
        var allSections = nodes.SelectMany(r => r.Sections).ToList();

        int totalSections = allSections.Count;
        int totalObjects = allSections.Sum(s => s.Objects.Count);
        int totalDbFields = nodes.Sum(r => r.Fields.OfType<DatabaseField>().Count());
        int totalFormulaFields = nodes.Sum(r => r.Fields.OfType<FormulaField>().Count());
        int totalTables = nodes.Sum(r => r.DataSources.Sum(d => d.Tables.Count));
        int nestingDepth = allSections.Count > 0 ? allSections.Max(s => s.GroupLevel) + 1 : 0;
        int totalGroups = nodes.Sum(r => r.Groups.Count);

        // Weighted 0..1 heuristic: objects and formulas dominate visual/logic complexity,
        // tables and groups contribute a smaller amount each, all soft-capped via /(x+10).
        double score =
            0.35 * Cap(totalObjects, 80) +
            0.30 * Cap(totalFormulaFields, 20) +
            0.15 * Cap(totalTables, 8) +
            0.10 * Cap(totalGroups, 6) +
            0.10 * Cap(nestingDepth, 5);
        score = Math.Round(Math.Clamp(score, 0, 1), 2);

        string level = score switch
        {
            < 0.3 => "low",
            < 0.6 => "moderate",
            _ => "high"
        };

        context.ComplexityMetrics = new ComplexityMetricsDto
        {
            TotalSections = totalSections,
            TotalObjects = totalObjects,
            TotalDatabaseFields = totalDbFields,
            TotalFormulaFields = totalFormulaFields,
            TotalTables = totalTables,
            NestingDepth = nestingDepth,
            EstimatedComplexityScore = score,
            ComplexityLevel = level
        };

        if (totalFormulaFields > 0)
        {
            context.Recommendations.Add(new Recommendation
            {
                Priority = "low",
                Category = "maintenance",
                Title = "Add comments to complex formulas",
                Action = "Document formula logic for future maintainers",
                Impact = "maintainability"
            });
        }
    }

    private static double Cap(int value, int softMax) => Math.Min(1.0, value / (double)softMax);
}
