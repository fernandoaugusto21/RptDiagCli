using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Fields;
using Majorsilence.Crystal.Model.Objects;
using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Builds "data_sources" (connections + database fields) and flags orphan fields
/// and unresolvable connections — the parser has no live database to validate against.
///
/// A field counts as "used" when it is placed directly as a FieldObject anywhere in the
/// report (including inside subreports), or when a formula that depends on it is placed
/// anywhere — otherwise a field that only ever feeds a formula would be misreported as
/// orphan even though removing it would break that formula.
///
/// Subreports keep their own field/formula/data-source definitions (see <see cref="SubreportWalker"/>),
/// so connections, database fields and formula-dependency propagation are all built by walking
/// the whole report tree — otherwise a field or table that only exists inside a subreport
/// (e.g. a detail table rendered by a SubreportObject) would never appear in the output.</summary>
public sealed class DataSourceAnalyzer : IReportAnalyzer
{
    public void Analyze(ReportDefinition report, AnalysisContext context)
    {
        var nodes = SubreportWalker.Walk(report).ToList();

        // Formula names must be collected across the whole tree up front: CollectSectionUsage
        // uses this set to decide whether a placed FieldObject inside a subreport is a formula
        // placement or a plain field usage, and a subreport's own formulas are never present in
        // the top-level report.Fields.
        var formulaNames = new HashSet<string>(
            nodes.SelectMany(n => n.Report.Fields.OfType<FormulaField>()).Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var fieldUsage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var formulaPlacement = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        CollectSectionUsage(report, formulaNames, fieldUsage, formulaPlacement, labelSuffix: string.Empty);

        // A field referenced only through a formula (never placed directly) is still "used" —
        // propagate the sections where the formula itself is placed onto each of its dependencies.
        foreach (var (rep, _) in nodes)
        {
            foreach (var formula in rep.Fields.OfType<FormulaField>())
            {
                if (!formulaPlacement.TryGetValue(formula.Name, out var placedIn)) continue;
                foreach (var dep in FormulaReferenceExtractor.ExtractFieldDependencies(formula.FormulaText))
                {
                    if (!fieldUsage.TryGetValue(dep, out var list))
                    {
                        list = [];
                        fieldUsage[dep] = list;
                    }
                    list.AddRange(placedIn);
                }
            }
        }

        int dbIndex = 0;
        foreach (var (rep, subreport) in nodes)
        {
            foreach (var ds in rep.DataSources)
            {
                dbIndex++;
                string id = string.IsNullOrEmpty(ds.Name) ? $"db{dbIndex}" : ds.Name;

                var tables = ds.Tables.Select(t => new TableDto
                {
                    Name = t.Name,
                    Alias = t.Alias,
                    JoinCondition = null
                }).ToList();

                context.DataSources.DatabaseConnections.Add(new DatabaseConnectionDto
                {
                    Id = id,
                    Type = ds.Kind.ToString(),
                    Server = ds.ServerName,
                    Database = ds.DatabaseName,
                    Status = "unvalidated",
                    Subreport = subreport,
                    Tables = tables
                });

                context.Errors.Add(new Finding
                {
                    Id = "DB_CONNECTION_UNRESOLVABLE",
                    Severity = "ERROR",
                    Message = $"Database connection '{ds.ServerName ?? ds.DatabaseName ?? id}' cannot be validated in parsing context",
                    AffectedElement = $"database_connection::{id}",
                    Suggestion = "Verify database server is accessible; consider using local datasource for testing"
                });

                if (tables.Count >= 2)
                {
                    var joinDesc = string.Join(" ← ", tables.Select(t => t.Name));
                    context.Info.Add(new Finding
                    {
                        Id = "MULTI_TABLE_JOIN",
                        Severity = "INFO",
                        Message = $"Report uses {tables.Count} tables with join condition",
                        Details = $"{joinDesc} (inner join)"
                    });
                }
            }
        }

        bool hasOrphanFlagged = false;
        foreach (var (rep, subreport) in nodes)
        {
            foreach (var field in rep.Fields.OfType<DatabaseField>())
            {
                fieldUsage.TryGetValue(field.ColumnName, out var usage);
                var distinctUsage = usage?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
                int usageCount = distinctUsage.Count;
                bool orphan = usageCount == 0;
                if (orphan) hasOrphanFlagged = true;

                context.DataSources.Fields.DatabaseFields.Add(new DatabaseFieldDto
                {
                    Name = field.Name,
                    Type = field.DataType,
                    TableSource = field.TableName,
                    Subreport = subreport,
                    UsageCount = usageCount,
                    UsedInSections = distinctUsage,
                    Flag = orphan ? "ORPHAN" : null
                });

                if (orphan)
                {
                    context.Warnings.Add(new Finding
                    {
                        Id = "FIELD_ORPHAN",
                        Severity = "WARNING",
                        Message = subreport is null
                            ? $"Field '{field.Name}' is defined but never referenced in report sections"
                            : $"Field '{field.Name}' (subreport:{subreport}) is defined but never referenced in report sections",
                        AffectedElement = subreport is null
                            ? $"database_field::{field.Name}"
                            : $"database_field::{field.Name}::subreport:{subreport}",
                        Suggestion = "Remove unused field to simplify data source"
                    });
                }
            }
        }

        if (hasOrphanFlagged)
        {
            context.Recommendations.Add(new Recommendation
            {
                Priority = "medium",
                Category = "optimization",
                Title = "Remove orphan fields",
                Action = "Delete unused fields from the data source to reduce query complexity",
                Impact = "performance"
            });
        }

        int totalDbFields = nodes.Sum(n => n.Report.Fields.OfType<DatabaseField>().Count());
        int totalFormulaFields = nodes.Sum(n => n.Report.Fields.OfType<FormulaField>().Count());
        context.Trace("DEBUG", $"Found {totalDbFields} database fields, {totalFormulaFields} formula fields across {nodes.Count} report node(s)");
    }

    /// <summary>Walks the report's own sections and every nested subreport, recording where
    /// each placed FieldObject appears — formula placements go to <paramref name="formulaPlacement"/>
    /// keyed by formula name (so callers can attribute dependency usage), everything else to
    /// <paramref name="fieldUsage"/> keyed by field/column name.</summary>
    private static void CollectSectionUsage(
        ReportDefinition report,
        HashSet<string> formulaNames,
        Dictionary<string, List<string>> fieldUsage,
        Dictionary<string, List<string>> formulaPlacement,
        string labelSuffix)
    {
        foreach (var section in report.Sections)
        {
            string label = section.Type + labelSuffix;
            foreach (var obj in section.Objects)
            {
                switch (obj)
                {
                    case FieldObject fieldObj when !string.IsNullOrEmpty(fieldObj.FieldName):
                        // Crystal names a formula's placed FieldObject with a leading '@' (e.g.
                        // "@Discounted Price"); strip it to match FormulaField.Name.
                        string raw = fieldObj.FieldName;
                        string trimmed = raw.StartsWith('@') ? raw[1..] : raw;
                        var target = formulaNames.Contains(trimmed) ? formulaPlacement : fieldUsage;
                        string key = formulaNames.Contains(trimmed) ? trimmed : raw;
                        if (!target.TryGetValue(key, out var list))
                        {
                            list = [];
                            target[key] = list;
                        }
                        list.Add(label);
                        break;

                    case SubreportObject { Report: not null } sub:
                        string subLabel = $" (subreport:{(string.IsNullOrEmpty(sub.SubreportName) ? "?" : sub.SubreportName)})";
                        CollectSectionUsage(sub.Report, formulaNames, fieldUsage, formulaPlacement, labelSuffix + subLabel);
                        break;
                }
            }
        }
    }
}
