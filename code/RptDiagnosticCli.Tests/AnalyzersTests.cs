using Majorsilence.Crystal.Model;
using Majorsilence.Crystal.Model.Fields;
using Majorsilence.Crystal.Model.Objects;
using RptDiagnosticCli.Analyzers;

namespace RptDiagnosticCli.Tests;

public class StructureAnalyzerTests
{
    [Fact]
    public void EmptySection_ProducesSectionEmptyWarning()
    {
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section { Type = SectionType.PageHeader, Objects = [] }
            ]
        };
        var context = new AnalysisContext();

        new StructureAnalyzer().Analyze(report, context);

        Assert.Single(context.Structure.Sections);
        Assert.Equal(0, context.Structure.Sections[0].ObjectCount);
        Assert.Contains(context.Warnings, w => w.Id == "SECTION_EMPTY");
    }

    [Fact]
    public void SectionWithObjects_CollectsDistinctFieldReferences()
    {
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects =
                    [
                        new FieldObject { Name = "F1", FieldName = "OrderID" },
                        new FieldObject { Name = "F2", FieldName = "OrderID" },
                        new FieldObject { Name = "F3", FieldName = "Amount" }
                    ]
                }
            ]
        };
        var context = new AnalysisContext();

        new StructureAnalyzer().Analyze(report, context);

        Assert.Equal(3, context.Structure.Sections[0].ObjectCount);
        Assert.Equal(["OrderID", "Amount"], context.Structure.Sections[0].FieldReferences);
        Assert.DoesNotContain(context.Warnings, w => w.Id == "SECTION_EMPTY");
    }

    [Fact]
    public void SuppressFormula_IsSerializedOnSection()
    {
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    SuppressFormula = "{IDR_ATVD_EXTE} = 0",
                    Objects = [new FieldObject { Name = "F1", FieldName = "Description" }]
                }
            ]
        };
        var context = new AnalysisContext();

        new StructureAnalyzer().Analyze(report, context);

        Assert.Equal("{IDR_ATVD_EXTE} = 0", context.Structure.Sections[0].SuppressFormula);
    }

    [Fact]
    public void SubreportSection_IsIncludedWithSubreportLabel()
    {
        var subReport = new ReportDefinition
        {
            Sections = [new Section { Type = SectionType.Details, Objects = [] }]
        };
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new SubreportObject { SubreportName = "Subreport1", Report = subReport }]
                }
            ]
        };
        var context = new AnalysisContext();

        new StructureAnalyzer().Analyze(report, context);

        Assert.Equal(2, context.Structure.Sections.Count);
        var subSection = context.Structure.Sections.Single(s => s.Subreport == "Subreport1");
        Assert.Equal(0, subSection.ObjectCount);
        Assert.Contains(context.Warnings, w => w.Id == "SECTION_EMPTY" && w.AffectedElement!.Contains("Subreport1"));
    }

    [Fact]
    public void RecordSelectionFormula_OnMainAndSubreport_AreBothSerialized()
    {
        var subReport = new ReportDefinition
        {
            RecordSelectionFormula = "{DSCP.CDGDSCP} = '92'",
            Sections = [new Section { Type = SectionType.Details, Objects = [] }]
        };
        var report = new ReportDefinition
        {
            RecordSelectionFormula = "{FC_RELA_HIST_ESCO_SUPE_SGS.ANO} = 2026",
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new SubreportObject { SubreportName = "Subreport1", Report = subReport }]
                }
            ]
        };
        var context = new AnalysisContext();

        new StructureAnalyzer().Analyze(report, context);

        Assert.Equal(2, context.Structure.SelectionFormulas.Count);
        Assert.Contains(context.Structure.SelectionFormulas, f => f.Subreport is null && f.RecordSelectionFormula == "{FC_RELA_HIST_ESCO_SUPE_SGS.ANO} = 2026");
        Assert.Contains(context.Structure.SelectionFormulas, f => f.Subreport == "Subreport1" && f.RecordSelectionFormula == "{DSCP.CDGDSCP} = '92'");
    }
}

public class DataSourceAnalyzerTests
{
    [Fact]
    public void UnreferencedDatabaseField_IsFlaggedOrphan()
    {
        var report = new ReportDefinition
        {
            Fields = [new DatabaseField { Name = "Unused", ColumnName = "Unused", TableName = "T", DataType = "String" }],
            Sections = [new Section { Type = SectionType.Details, Objects = [] }]
        };
        var context = new AnalysisContext();

        new DataSourceAnalyzer().Analyze(report, context);

        var dbField = Assert.Single(context.DataSources.Fields.DatabaseFields);
        Assert.Equal("ORPHAN", dbField.Flag);
        Assert.Equal(0, dbField.UsageCount);
        Assert.Contains(context.Warnings, w => w.Id == "FIELD_ORPHAN");
        Assert.Contains(context.Recommendations, r => r.Title == "Remove orphan fields");
    }

    [Fact]
    public void ReferencedDatabaseField_IsNotFlagged()
    {
        var report = new ReportDefinition
        {
            Fields = [new DatabaseField { Name = "OrderID", ColumnName = "OrderID", TableName = "Orders", DataType = "int" }],
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new FieldObject { Name = "F1", FieldName = "OrderID" }]
                }
            ]
        };
        var context = new AnalysisContext();

        new DataSourceAnalyzer().Analyze(report, context);

        var dbField = Assert.Single(context.DataSources.Fields.DatabaseFields);
        Assert.Null(dbField.Flag);
        Assert.Equal(1, dbField.UsageCount);
        Assert.Equal(["Details"], dbField.UsedInSections);
        Assert.DoesNotContain(context.Warnings, w => w.Id == "FIELD_ORPHAN");
    }

    [Fact]
    public void DataSourceWithTwoTables_ProducesMultiTableJoinInfo()
    {
        var report = new ReportDefinition
        {
            DataSources =
            [
                new DataSource
                {
                    Name = "db1",
                    Kind = DataSourceKind.Odbc,
                    Tables =
                    [
                        new TableDefinition { Name = "Orders", Alias = "O" },
                        new TableDefinition { Name = "OrderItems", Alias = "OI" }
                    ]
                }
            ]
        };
        var context = new AnalysisContext();

        new DataSourceAnalyzer().Analyze(report, context);

        Assert.Contains(context.Info, i => i.Id == "MULTI_TABLE_JOIN");
        Assert.Contains(context.Errors, e => e.Id == "DB_CONNECTION_UNRESOLVABLE");
    }

    [Fact]
    public void FieldDefinedOnlyInSubreport_IsExposedWithSubreportLabel()
    {
        var subReport = new ReportDefinition
        {
            Fields = [new DatabaseField { Name = "DSCP_NOME", ColumnName = "DSCP_NOME", TableName = "PR_RELA_HIST_ESCO_DSCP_CURS_SUPE_SGS", DataType = "String" }],
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new FieldObject { Name = "F1", FieldName = "DSCP_NOME" }]
                }
            ]
        };
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new SubreportObject { SubreportName = "Subreport1", Report = subReport }]
                }
            ]
        };
        var context = new AnalysisContext();

        new DataSourceAnalyzer().Analyze(report, context);

        var dbField = Assert.Single(context.DataSources.Fields.DatabaseFields);
        Assert.Equal("Subreport1", dbField.Subreport);
        Assert.Null(dbField.Flag);
        Assert.Equal(1, dbField.UsageCount);
    }

    [Fact]
    public void OrphanFieldInSubreport_IsFlaggedWithSubreportInAffectedElement()
    {
        var subReport = new ReportDefinition
        {
            Fields = [new DatabaseField { Name = "Unused", ColumnName = "Unused", TableName = "T", DataType = "String" }],
            Sections = [new Section { Type = SectionType.Details, Objects = [] }]
        };
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new SubreportObject { SubreportName = "Subreport1", Report = subReport }]
                }
            ]
        };
        var context = new AnalysisContext();

        new DataSourceAnalyzer().Analyze(report, context);

        var dbField = Assert.Single(context.DataSources.Fields.DatabaseFields);
        Assert.Equal("ORPHAN", dbField.Flag);
        Assert.Contains(context.Warnings, w => w.Id == "FIELD_ORPHAN" && w.AffectedElement!.Contains("Subreport1"));
    }
}

public class FormulaAnalyzerTests
{
    [Fact]
    public void FormulaReferencingMissingField_IsFlaggedInvalid()
    {
        var report = new ReportDefinition
        {
            Fields =
            [
                new FormulaField { Name = "BrokenFormula", FormulaText = "{InvalidField} + 100" }
            ]
        };
        var context = new AnalysisContext();

        new FormulaAnalyzer().Analyze(report, context);

        var formula = Assert.Single(context.DataSources.Fields.FormulaFields);
        Assert.False(formula.SyntaxValid);
        Assert.Equal("Field 'InvalidField' does not exist in data source", formula.Error);
        Assert.Contains(context.Errors, e => e.Id == "FORMULA_SYNTAX_INVALID");
        Assert.Contains(context.Recommendations, r => r.Priority == "high");
    }

    [Fact]
    public void FormulaReferencingKnownField_IsValid()
    {
        var report = new ReportDefinition
        {
            Fields =
            [
                new DatabaseField { Name = "Amount", ColumnName = "Amount", TableName = "Orders", DataType = "Float64" },
                new FormulaField { Name = "TotalWithTax", FormulaText = "{Orders.Amount} * 1.1" }
            ]
        };
        var context = new AnalysisContext();

        new FormulaAnalyzer().Analyze(report, context);

        var formula = Assert.Single(context.DataSources.Fields.FormulaFields);
        Assert.True(formula.SyntaxValid);
        Assert.Null(formula.Error);
        Assert.Equal(["Amount"], formula.DependsOn);
        Assert.DoesNotContain(context.Errors, e => e.Id == "FORMULA_SYNTAX_INVALID");
    }

    [Fact]
    public void ParameterReference_IsNotTreatedAsMissingField()
    {
        var report = new ReportDefinition
        {
            Fields = [new FormulaField { Name = "Greeting", FormulaText = "\"Hello \" & {?UserName}" }]
        };
        var context = new AnalysisContext();

        new FormulaAnalyzer().Analyze(report, context);

        var formula = Assert.Single(context.DataSources.Fields.FormulaFields);
        Assert.True(formula.SyntaxValid);
        Assert.Empty(formula.DependsOn);
    }

    [Fact]
    public void FormulaDefinedOnlyInSubreport_IsValidatedAgainstItsOwnFieldNamespace()
    {
        var subReport = new ReportDefinition
        {
            Fields =
            [
                new DatabaseField { Name = "CDGDSCP", ColumnName = "CDGDSCP", TableName = "PR_RELA_HIST_ESCO_DSCP_CURS_SUPE_SGS", DataType = "String" },
                new FormulaField { Name = "IsExtensao", FormulaText = "{PR_RELA_HIST_ESCO_DSCP_CURS_SUPE_SGS.CDGDSCP} = '92'" }
            ]
        };
        var report = new ReportDefinition
        {
            Sections =
            [
                new Section
                {
                    Type = SectionType.Details,
                    Objects = [new SubreportObject { SubreportName = "Subreport1", Report = subReport }]
                }
            ]
        };
        var context = new AnalysisContext();

        new FormulaAnalyzer().Analyze(report, context);

        var formula = Assert.Single(context.DataSources.Fields.FormulaFields);
        Assert.Equal("Subreport1", formula.Subreport);
        Assert.True(formula.SyntaxValid);
        Assert.DoesNotContain(context.Errors, e => e.Id == "FORMULA_SYNTAX_INVALID");
    }
}

public class ComplexityAnalyzerTests
{
    [Fact]
    public void EmptyReport_HasZeroMetricsAndLowComplexity()
    {
        var report = new ReportDefinition();
        var context = new AnalysisContext();

        new ComplexityAnalyzer().Analyze(report, context);

        Assert.Equal(0, context.ComplexityMetrics.TotalSections);
        Assert.Equal(0.0, context.ComplexityMetrics.EstimatedComplexityScore);
        Assert.Equal("low", context.ComplexityMetrics.ComplexityLevel);
    }
}
