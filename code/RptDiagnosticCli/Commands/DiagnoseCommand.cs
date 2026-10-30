using Majorsilence.Crystal.Parser;
using RptDiagnosticCli.Analyzers;
using RptDiagnosticCli.Models;
using RptDiagnosticCli.Output;

namespace RptDiagnosticCli.Commands;

public sealed class DiagnoseCommand
{
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public int Run(string filePath, string? outputPath, bool verbose, string format)
    {
        void Trace(string level, string message)
        {
            if (verbose) Console.Error.WriteLine($"[{level}] {message}");
        }

        Trace("TRACE", $"Opening file: {filePath}");

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"✗ Error: File {filePath} not found");
            return ExitCode.InvalidFile;
        }

        if (!LooksLikeOleFile(filePath))
        {
            Console.Error.WriteLine($"✗ Error: File {filePath} is not a valid Crystal Reports .rpt file");
            Console.Error.WriteLine("✗ Details: OLE header not found");
            return ExitCode.InvalidFile;
        }
        Trace("TRACE", "Validating OLE header... OK");

        ParseResult parseResult;
        try
        {
            parseResult = RptParser.Parse(filePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Error: Failed to parse {filePath}");
            Console.Error.WriteLine($"✗ Details: {ex.Message}");
            return ExitCode.ParseError;
        }

        if (!parseResult.Success || parseResult.Report is null)
        {
            Console.Error.WriteLine($"✗ Error: Failed to parse {filePath}");
            Console.Error.WriteLine($"✗ Details: {string.Join("; ", parseResult.Errors)}");
            return ExitCode.ParseError;
        }

        var report = parseResult.Report;
        Trace("TRACE", "Reading TSLV stream... done");
        Trace("TRACE", "Parsing ReportDefinition AST");

        try
        {
            var fileInfo = new FileInfo(filePath);
            var context = new AnalysisContext { Trace = Trace };

            IReportAnalyzer[] analyzers =
            [
                new StructureAnalyzer(),
                new DataSourceAnalyzer(),
                new FormulaAnalyzer(),
                new ComplexityAnalyzer()
            ];
            foreach (var analyzer in analyzers)
                analyzer.Analyze(report, context);

            var result = new DiagnosticResult
            {
                Metadata = new MetadataDto
                {
                    FilePath = Path.GetFullPath(filePath),
                    FileSizeBytes = fileInfo.Length,
                    ReportName = Path.GetFileNameWithoutExtension(filePath),
                    ReportTitle = report.ReportTitle,
                    CrystalVersion = report.CrVersion.ToString(),
                    ParsedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ParserVersion = "rpt-diagnostic-1.0.0"
                },
                Structure = context.Structure,
                DataSources = context.DataSources,
                Diagnostics = new DiagnosticsDto
                {
                    Errors = context.Errors,
                    Warnings = context.Warnings,
                    Info = context.Info
                },
                ComplexityMetrics = context.ComplexityMetrics,
                Recommendations = context.Recommendations
            };

            Trace("INFO", $"Analysis: {context.Errors.Count} errors, {context.Warnings.Count} warnings, {context.Info.Count} info");

            string resolvedOutput = outputPath ?? $"{Path.GetFileNameWithoutExtension(filePath)}_diagnostic.json";
            bool pretty = !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);

            Trace("TRACE", "Emitting JSON...");
            JsonEmitter.Write(result, resolvedOutput, pretty);

            Console.WriteLine($"✓ Parsed {Path.GetFileName(filePath)} ({context.ComplexityMetrics.TotalObjects} objects, {context.ComplexityMetrics.TotalFormulaFields} formulas)");
            Console.WriteLine($"✓ Analysis complete: {context.Errors.Count} errors, {context.Warnings.Count} warnings, {context.Info.Count} info");
            Console.WriteLine($"✓ Diagnostic JSON written to {resolvedOutput}");
            Trace("TRACE", "Done.");

            return ExitCode.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("✗ Error: Analysis failed");
            Console.Error.WriteLine($"✗ Details: {ex.Message}");
            return ExitCode.AnalysisError;
        }
    }

    private static bool LooksLikeOleFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            if (stream.Length < OleSignature.Length) return false;
            Span<byte> header = stackalloc byte[OleSignature.Length];
            int read = stream.ReadAtLeast(header, OleSignature.Length, throwOnEndOfStream: false);
            return read == OleSignature.Length && header.SequenceEqual(OleSignature);
        }
        catch
        {
            return false;
        }
    }
}
