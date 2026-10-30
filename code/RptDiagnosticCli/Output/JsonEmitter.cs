using System.Text.Json;
using RptDiagnosticCli.Models;

namespace RptDiagnosticCli.Output;

public static class JsonEmitter
{
    public static void Write(DiagnosticResult result, string outputPath, bool pretty)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        string json = JsonSerializer.Serialize(result, options);
        File.WriteAllText(outputPath, json);
    }
}
