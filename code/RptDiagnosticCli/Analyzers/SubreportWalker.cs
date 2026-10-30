using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Objects;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Depth-first walk of a report and every subreport reachable from its sections.
///
/// A Crystal subreport (<see cref="SubreportObject.Report"/>) is its own independent
/// <see cref="ReportDefinition"/> — separate Fields, Sections, DataSources, selection
/// formulas — only linked in via the placed <see cref="SubreportObject"/>. Analyzers that
/// only look at the top-level report's own lists silently miss everything defined inside a
/// subreport (fields, formulas, sections, suppress/selection formulas), which is exactly the
/// kind of report content most likely to carry conditional logic worth diagnosing.</summary>
internal static class SubreportWalker
{
    /// <summary>Yields (report, subreportPath) for <paramref name="root"/> and every nested
    /// subreport. <c>subreportPath</c> is null for <paramref name="root"/> itself, the
    /// subreport's name for a direct child, and "Outer &gt; Inner" for nested subreports.</summary>
    public static IEnumerable<(ReportDefinition Report, string? Subreport)> Walk(ReportDefinition root, string? path = null)
    {
        yield return (root, path);

        foreach (var section in root.Sections)
        {
            foreach (var sub in section.Objects.OfType<SubreportObject>())
            {
                if (sub.Report is null) continue;
                string name = string.IsNullOrEmpty(sub.SubreportName) ? "?" : sub.SubreportName;
                string nestedPath = path is null ? name : $"{path} > {name}";
                foreach (var pair in Walk(sub.Report, nestedPath))
                    yield return pair;
            }
        }
    }
}
