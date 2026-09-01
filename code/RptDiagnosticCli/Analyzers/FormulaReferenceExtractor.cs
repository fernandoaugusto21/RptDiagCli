using System.Text.RegularExpressions;

namespace RptDiagnosticCli.Analyzers;

/// <summary>Shared {Table.Field} / {Field} reference extraction, used both to validate
/// formula syntax and to attribute a database field as "used" when only a formula
/// that depends on it is placed on the report.</summary>
public static partial class FormulaReferenceExtractor
{
    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex BracedReference();

    /// <summary>Field/column names referenced by <paramref name="formulaText"/>, with the
    /// table prefix stripped and {?Parameter} references excluded.</summary>
    public static List<string> ExtractFieldDependencies(string formulaText)
    {
        var result = new List<string>();
        foreach (Match m in BracedReference().Matches(formulaText))
        {
            string token = m.Groups[1].Value.Trim();
            if (token.StartsWith('?')) continue;

            int dot = token.IndexOf('.');
            string fieldName = dot >= 0 ? token[(dot + 1)..] : token;
            if (!string.IsNullOrEmpty(fieldName))
                result.Add(fieldName);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
