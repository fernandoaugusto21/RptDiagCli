using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Objects;
using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Builds the "structure" section of the diagnostic and flags empty sections.
///
/// Walks subreports too (see <see cref="SubreportWalker"/>) so a section that only exists
/// inside a subreport — and any SuppressFormula gating it — is still visible in the output,
/// along with each report node's own record/group selection formulas.</summary>
public sealed class StructureAnalyzer : IReportAnalyzer
{
    public void Analyze(ReportDefinition report, AnalysisContext context)
    {
        foreach (var (rep, subreport) in SubreportWalker.Walk(report))
        {
            foreach (var section in rep.Sections)
            {
                var fieldRefs = section.Objects
                    .OfType<FieldObject>()
                    .Select(f => f.FieldName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                context.Structure.Sections.Add(new SectionDto
                {
                    Type = section.Type.ToString(),
                    Subreport = subreport,
                    ObjectCount = section.Objects.Count,
                    FieldReferences = fieldRefs,
                    SuppressFormula = section.SuppressFormula
                });

                if (section.Objects.Count == 0)
                {
                    context.Warnings.Add(new Finding
                    {
                        Id = "SECTION_EMPTY",
                        Severity = "WARNING",
                        Message = subreport is null
                            ? $"{section.Type} section is defined but contains no objects"
                            : $"{section.Type} section (subreport:{subreport}) is defined but contains no objects",
                        AffectedElement = subreport is null
                            ? $"section::{section.Type}"
                            : $"section::{section.Type}::subreport:{subreport}",
                        Suggestion = "Remove empty sections or add content"
                    });
                }
            }

            if (rep.RecordSelectionFormula is not null || rep.GroupSelectionFormula is not null)
            {
                context.Structure.SelectionFormulas.Add(new SelectionFormulaDto
                {
                    Subreport = subreport,
                    RecordSelectionFormula = rep.RecordSelectionFormula,
                    GroupSelectionFormula = rep.GroupSelectionFormula
                });
            }

            context.Trace("DEBUG", subreport is null
                ? $"Found {rep.Sections.Count} sections: {string.Join(", ", rep.Sections.Select(s => s.Type))}"
                : $"[subreport:{subreport}] Found {rep.Sections.Count} sections: {string.Join(", ", rep.Sections.Select(s => s.Type))}");
        }
    }
}
