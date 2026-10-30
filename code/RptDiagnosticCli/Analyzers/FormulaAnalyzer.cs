using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Fields;
using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Builds "data_sources.fields.formula_fields" and validates that every
/// {Field} / {Table.Field} reference a formula depends on actually exists in the report.
///
/// Each subreport is its own independent <see cref="ReportDefinition"/> with its own field
/// namespace (see <see cref="SubreportWalker"/>), so validation is scoped per report node — a
/// subreport's formula can only depend on fields defined in that same subreport.</summary>
public sealed class FormulaAnalyzer : IReportAnalyzer
{
    public void Analyze(ReportDefinition report, AnalysisContext context)
    {
        bool anyBroken = false;

        foreach (var (rep, subreport) in SubreportWalker.Walk(report))
        {
            var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in rep.Fields.OfType<DatabaseField>())
            {
                knownNames.Add(f.Name);
                knownNames.Add(f.ColumnName);
            }
            foreach (var f in rep.Fields.OfType<FormulaField>())
                knownNames.Add(f.Name);
            foreach (var f in rep.Fields.OfType<RunningTotalField>())
                knownNames.Add(f.Name);

            foreach (var formula in rep.Fields.OfType<FormulaField>())
            {
                var dependsOn = FormulaReferenceExtractor.ExtractFieldDependencies(formula.FormulaText);
                string? missing = dependsOn.FirstOrDefault(f => !knownNames.Contains(f));
                bool valid = missing is null;
                if (!valid) anyBroken = true;

                context.DataSources.Fields.FormulaFields.Add(new FormulaFieldDto
                {
                    Name = formula.Name,
                    Expression = formula.FormulaText,
                    FormulaLanguage = formula.Syntax.ToString(),
                    Subreport = subreport,
                    DependsOn = dependsOn,
                    SyntaxValid = valid,
                    Error = valid ? null : $"Field '{missing}' does not exist in data source"
                });

                if (!valid)
                {
                    context.Errors.Add(new Finding
                    {
                        Id = "FORMULA_SYNTAX_INVALID",
                        Severity = "ERROR",
                        Message = subreport is null
                            ? $"Formula '{formula.Name}' references undefined field '{missing}'"
                            : $"Formula '{formula.Name}' (subreport:{subreport}) references undefined field '{missing}'",
                        AffectedElement = subreport is null
                            ? $"formula_field::{formula.Name}"
                            : $"formula_field::{formula.Name}::subreport:{subreport}",
                        Suggestion = "Remove or correct the formula reference"
                    });

                    context.Recommendations.Add(new Recommendation
                    {
                        Priority = "high",
                        Category = "data_quality",
                        Title = $"Fix broken formula '{formula.Name}'",
                        Action = "Correct formula syntax or remove reference to undefined field",
                        Impact = "report_execution"
                    });
                }
            }
        }

        if (anyBroken)
            context.Trace("INFO", "One or more formulas reference undefined fields");
    }
}
