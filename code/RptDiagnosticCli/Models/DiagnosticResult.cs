using System.Text.Json.Serialization;

namespace RptDiagnosticCli.Models;

public sealed class DiagnosticResult
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "rpt-diagnostic-v1";

    public MetadataDto Metadata { get; init; } = new();
    public StructureDto Structure { get; init; } = new();

    [JsonPropertyName("data_sources")]
    public DataSourcesDto DataSources { get; init; } = new();

    public DiagnosticsDto Diagnostics { get; init; } = new();

    [JsonPropertyName("complexity_metrics")]
    public ComplexityMetricsDto ComplexityMetrics { get; init; } = new();

    public List<Recommendation> Recommendations { get; init; } = [];
}

public sealed class MetadataDto
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; init; }

    [JsonPropertyName("report_name")]
    public string ReportName { get; init; } = string.Empty;

    [JsonPropertyName("report_title")]
    public string ReportTitle { get; init; } = string.Empty;

    [JsonPropertyName("crystal_version")]
    public string CrystalVersion { get; init; } = string.Empty;

    [JsonPropertyName("parsed_at_utc")]
    public string ParsedAtUtc { get; init; } = string.Empty;

    [JsonPropertyName("parser_version")]
    public string ParserVersion { get; init; } = string.Empty;
}

public sealed class StructureDto
{
    public List<SectionDto> Sections { get; init; } = [];

    [JsonPropertyName("selection_formulas")]
    public List<SelectionFormulaDto> SelectionFormulas { get; init; } = [];
}

public sealed class SectionDto
{
    public string Type { get; init; } = string.Empty;

    /// <summary>Null when the section belongs to the main report; otherwise the subreport
    /// path (e.g. "Subreport1") it was found in — see <c>SubreportWalker</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subreport { get; init; }

    [JsonPropertyName("object_count")]
    public int ObjectCount { get; init; }

    [JsonPropertyName("field_references")]
    public List<string> FieldReferences { get; init; } = [];

    /// <summary>Crystal formula text driving conditional suppression of this section; null when
    /// suppression is static (or absent).</summary>
    [JsonPropertyName("suppress_formula")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuppressFormula { get; init; }
}

/// <summary>Record/group selection formulas gate which rows a report (or subreport) pulls at
/// all — distinct from a section's SuppressFormula, which only hides an already-fetched row.
/// One entry per report node that actually has at least one of these set.</summary>
public sealed class SelectionFormulaDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subreport { get; init; }

    [JsonPropertyName("record_selection_formula")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecordSelectionFormula { get; init; }

    [JsonPropertyName("group_selection_formula")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GroupSelectionFormula { get; init; }
}

public sealed class DataSourcesDto
{
    [JsonPropertyName("database_connections")]
    public List<DatabaseConnectionDto> DatabaseConnections { get; init; } = [];

    public FieldsDto Fields { get; init; } = new();
}

public sealed class DatabaseConnectionDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Server { get; init; }
    public string? Database { get; init; }
    public string Status { get; init; } = "unvalidated";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subreport { get; init; }

    public List<TableDto> Tables { get; init; } = [];
}

public sealed class TableDto
{
    public string Name { get; init; } = string.Empty;
    public string Alias { get; init; } = string.Empty;

    [JsonPropertyName("join_condition")]
    public string? JoinCondition { get; init; }
}

public sealed class FieldsDto
{
    [JsonPropertyName("database_fields")]
    public List<DatabaseFieldDto> DatabaseFields { get; init; } = [];

    [JsonPropertyName("formula_fields")]
    public List<FormulaFieldDto> FormulaFields { get; init; } = [];
}

public sealed class DatabaseFieldDto
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("table_source")]
    public string TableSource { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subreport { get; init; }

    [JsonPropertyName("usage_count")]
    public int UsageCount { get; init; }

    [JsonPropertyName("used_in_sections")]
    public List<string> UsedInSections { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Flag { get; init; }
}

public sealed class FormulaFieldDto
{
    public string Name { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;

    [JsonPropertyName("formula_language")]
    public string FormulaLanguage { get; init; } = "Crystal";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subreport { get; init; }

    [JsonPropertyName("depends_on")]
    public List<string> DependsOn { get; init; } = [];

    [JsonPropertyName("syntax_valid")]
    public bool SyntaxValid { get; init; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

public sealed class DiagnosticsDto
{
    public List<Finding> Errors { get; init; } = [];
    public List<Finding> Warnings { get; init; } = [];
    public List<Finding> Info { get; init; } = [];
}

public sealed class ComplexityMetricsDto
{
    [JsonPropertyName("total_sections")]
    public int TotalSections { get; init; }

    [JsonPropertyName("total_objects")]
    public int TotalObjects { get; init; }

    [JsonPropertyName("total_database_fields")]
    public int TotalDatabaseFields { get; init; }

    [JsonPropertyName("total_formula_fields")]
    public int TotalFormulaFields { get; init; }

    [JsonPropertyName("total_tables")]
    public int TotalTables { get; init; }

    [JsonPropertyName("nesting_depth")]
    public int NestingDepth { get; init; }

    [JsonPropertyName("estimated_complexity_score")]
    public double EstimatedComplexityScore { get; init; }

    [JsonPropertyName("complexity_level")]
    public string ComplexityLevel { get; init; } = "low";
}
