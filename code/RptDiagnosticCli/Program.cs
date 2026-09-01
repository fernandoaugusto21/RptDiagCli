using System.CommandLine;
using RptDiagnosticCli;
using RptDiagnosticCli.Commands;

var fileOption = new Option<string>("--file")
{
    Description = "Path to the .rpt file to analyze",
    Required = true
};

var outputOption = new Option<string?>("--output")
{
    Description = "Output JSON file path (default: <report_name>_diagnostic.json)"
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Emit debug/trace output on stderr"
};

var formatOption = new Option<string>("--format")
{
    Description = "Output format",
    DefaultValueFactory = _ => "json-pretty"
};
formatOption.AcceptOnlyFromAmong("json", "json-pretty");

var rootCommand = new RootCommand("rpt-diagnostic: parses Crystal Reports .rpt files and emits a structured JSON diagnostic")
{
    fileOption,
    outputOption,
    verboseOption,
    formatOption
};

rootCommand.SetAction(parseResult =>
{
    string file = parseResult.GetValue(fileOption)!;
    string? output = parseResult.GetValue(outputOption);
    bool verbose = parseResult.GetValue(verboseOption);
    string format = parseResult.GetValue(formatOption)!;

    return new DiagnoseCommand().Run(file, output, verbose, format);
});

ParseResult parsed = rootCommand.Parse(args);
if (parsed.Errors.Count > 0)
{
    foreach (var error in parsed.Errors)
        Console.Error.WriteLine(error.Message);
    return ExitCode.CliUsageError;
}

return parsed.Invoke();
