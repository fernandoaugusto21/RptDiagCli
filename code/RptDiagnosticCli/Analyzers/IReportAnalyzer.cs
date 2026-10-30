using Majorsilence.Crystal.Model;

namespace RptDiagnosticCli.Analyzers;

public interface IReportAnalyzer
{
    void Analyze(ReportDefinition report, AnalysisContext context);
}
