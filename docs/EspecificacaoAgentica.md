# RPT Diagnostic CLI — Especificação LLM-Agêntica

**Data**: 2026-08-20  
**Objetivo**: CLI .NET que parseia .rpt (Crystal Reports) e emite diagnóstico estruturado (JSON) para consumo por Claude Code ou outro LLM agêntico em pipeline local.

**Escopo**: Parsing, extração estrutural, diagnóstico básico — sem renderização de relatório.

---

## 1. Premissas e Contexto

- **Entrada**: arquivo `.rpt` (binário, formato OLE/TSLV proprietário)
- **Saída**: JSON estruturado contendo metadados, problemas detectados, recomendações
- **Consumidor**: Claude Code (agentic) lê JSON e realiza análise semântica + diagnóstico avançado
- **Ambiente**: Linux + .NET 9.0+ (self-contained), sem GUI, sem Crystal Reports runtime SAP
- **Interoperabilidade**: saída JSON deve ser previsível, schema documentado

---

## 2. Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│  CLI: rpt-diagnostic [--file] [--output] [--verbose]         │
└───────────┬─────────────────────────────────────────────────┘
            │
            ├─→ RptParser (Majorsilence.Crystal.Parser)
            │   ├─ Lê binário OLE
            │   ├─ Decodifica TSLV stream
            │   └─ Extrai ReportDefinition (AST neutro)
            │
            ├─→ DiagnosticAnalyzer
            │   ├─ Valida estrutura (seções, campos, fórmulas)
            │   ├─ Detecta anomalias (campos órfãos, conexões inválidas)
            │   ├─ Coleta métricas (complexidade, dependências)
            │   └─ Gera recomendações
            │
            └─→ JsonEmitter
                └─ Serializa resultado estruturado
                   ├─ metadata (nome, versão, tamanho)
                   ├─ sections (relatório structure)
                   ├─ fields (banco de dados + fórmulas)
                   ├─ diagnostics (problemas)
                   └─ recommendations (ações)
```

---

## 3. Entrada e Saída

### 3.1 Entrada

```bash
$ rpt-diagnostic \
  --file /path/to/report.rpt \
  --output report_diagnostic.json \
  --verbose
```

**Argumentos obrigatórios**:
- `--file <path>`: Caminho para .rpt (relativo ou absoluto)

**Argumentos opcionais**:
- `--output <path>`: Arquivo de saída JSON (padrão: `<report_name>_diagnostic.json`)
- `--verbose`: Flag booleana, saída debug/trace em stderr
- `--format <json|json-pretty>`: Formato JSON (padrão: `json-pretty`)

### 3.2 Saída JSON — Schema

```json
{
  "$schema": "rpt-diagnostic-v1",
  "metadata": {
    "file_path": "/path/to/report.rpt",
    "file_size_bytes": 102400,
    "report_name": "OrderDetails",
    "report_title": "Order Summary Report",
    "crystal_version": "2016",
    "parsed_at_utc": "2026-08-20T14:30:45Z",
    "parser_version": "rpt-diagnostic-1.0.0"
  },
  "structure": {
    "sections": [
      {
        "type": "ReportHeader",
        "object_count": 3,
        "field_references": ["OrderID", "CustomerName"]
      },
      {
        "type": "Details",
        "object_count": 12,
        "field_references": ["LineItem", "Amount", "ProductCode"],
        "suppress_formula": "{Orders.Status} = 'Cancelled'"
      },
      {
        "type": "Details",
        "subreport": "Subreport1",
        "object_count": 4,
        "field_references": ["LineNote"]
      },
      {
        "type": "ReportFooter",
        "object_count": 2,
        "field_references": ["TotalAmount"]
      }
    ],
    "selection_formulas": [
      {
        "subreport": "Subreport1",
        "record_selection_formula": "{OrderItems.Category} = 'Extension'"
      }
    ]
  },
  "data_sources": {
    "database_connections": [
      {
        "id": "db1",
        "type": "SQL Server",
        "server": "prod-sql.internal",
        "database": "OrderDB",
        "status": "unvalidated",
        "tables": [
          {
            "name": "Orders",
            "alias": "O",
            "join_condition": null
          },
          {
            "name": "OrderItems",
            "alias": "OI",
            "join_condition": "O.OrderID = OI.OrderID"
          }
        ]
      }
    ],
    "fields": {
      "database_fields": [
        {
          "name": "OrderID",
          "type": "int",
          "table_source": "Orders",
          "usage_count": 5,
          "used_in_sections": ["ReportHeader", "Details", "ReportFooter"]
        },
        {
          "name": "UnusedField",
          "type": "varchar",
          "table_source": "OrderItems",
          "usage_count": 0,
          "flag": "ORPHAN"
        }
      ],
      "formula_fields": [
        {
          "name": "TotalWithTax",
          "expression": "{Amount} * 1.1",
          "formula_language": "Crystal",
          "depends_on": ["Amount"],
          "syntax_valid": true
        },
        {
          "name": "BrokenFormula",
          "expression": "{InvalidField} + 100",
          "depends_on": ["InvalidField"],
          "syntax_valid": false,
          "error": "Field 'InvalidField' does not exist in data source"
        }
      ]
    }
  },
  "diagnostics": {
    "errors": [
      {
        "id": "DB_CONNECTION_UNRESOLVABLE",
        "severity": "ERROR",
        "message": "Database connection 'prod-sql.internal' cannot be validated in parsing context",
        "affected_element": "database_connection::db1",
        "suggestion": "Verify database server is accessible; consider using local datasource for testing"
      },
      {
        "id": "FORMULA_SYNTAX_INVALID",
        "severity": "ERROR",
        "message": "Formula 'BrokenFormula' references undefined field 'InvalidField'",
        "affected_element": "formula_field::BrokenFormula",
        "suggestion": "Remove or correct the formula reference"
      }
    ],
    "warnings": [
      {
        "id": "FIELD_ORPHAN",
        "severity": "WARNING",
        "message": "Field 'UnusedField' is defined but never referenced in report sections",
        "affected_element": "database_field::UnusedField",
        "suggestion": "Remove unused field to simplify data source"
      },
      {
        "id": "SECTION_EMPTY",
        "severity": "WARNING",
        "message": "PageHeader section is defined but contains no objects",
        "affected_element": "section::PageHeader",
        "suggestion": "Remove empty sections or add content"
      }
    ],
    "info": [
      {
        "id": "MULTI_TABLE_JOIN",
        "severity": "INFO",
        "message": "Report uses 2 tables with join condition",
        "details": "Orders ← OrderItems (inner join)"
      }
    ]
  },
  "complexity_metrics": {
    "total_sections": 5,
    "total_objects": 42,
    "total_database_fields": 18,
    "total_formula_fields": 7,
    "total_tables": 2,
    "nesting_depth": 2,
    "estimated_complexity_score": 0.62,
    "complexity_level": "moderate"
  },
  "recommendations": [
    {
      "priority": "high",
      "category": "data_quality",
      "title": "Fix broken formula 'BrokenFormula'",
      "action": "Correct formula syntax or remove reference to undefined field",
      "impact": "report_execution"
    },
    {
      "priority": "medium",
      "category": "optimization",
      "title": "Remove orphan fields",
      "action": "Delete 'UnusedField' from data source to reduce query complexity",
      "impact": "performance"
    },
    {
      "priority": "low",
      "category": "maintenance",
      "title": "Add comments to complex formulas",
      "action": "Document formula logic for future maintainers",
      "impact": "maintainability"
    }
  ]
}
```

**Subrelatórios**: um `SubreportObject` embute uma `ReportDefinition` própria (campos, fórmulas,
seções e data sources independentes do relatório principal). Todo item de `structure.sections`,
`data_sources.fields.database_fields`, `data_sources.fields.formula_fields` e
`data_sources.database_connections` originado dentro de um subrelatório carrega um campo
`subreport` (nome do subreport, ou "Externo > Interno" se aninhado); ausente/`null` significa
relatório principal. `structure.selection_formulas` lista, por relatório/subrelatório, as
fórmulas de `RecordSelectionFormula`/`GroupSelectionFormula` que controlam quais linhas
chegam a ser lidas — distintas de `suppress_formula` em uma seção, que só oculta uma linha já
lida. `complexity_metrics` soma os totais da árvore inteira (principal + subrelatórios).

---

## 4. Dependências

### NuGet Packages
```xml
<ItemGroup>
  <PackageReference Include="Majorsilence.Crystal.Parser" Version="1.0.0" />
  <PackageReference Include="Majorsilence.Crystal.Model" Version="1.0.0" />
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
</ItemGroup>
```

### Runtime
- **.NET 9.0** (self-contained publish, sem dependências externas)
- **Linux**: ubuntu:22.04 ou posterior
- **Sem**: Crystal Reports runtime, SAP BI libraries, GUI frameworks

---

## 5. Estrutura de Código Esperada

```
RptDiagnosticCli/
├── Program.cs                      # Entry point, CLI args
├── Commands/
│   └── DiagnoseCommand.cs          # Handler para --file
├── Analyzers/
│   ├── IReportAnalyzer.cs          # Interface
│   ├── StructureAnalyzer.cs        # Valida sections, objects
│   ├── DataSourceAnalyzer.cs       # BD conexões, campos
│   ├── FormulaAnalyzer.cs          # Parse e validate fórmulas
│   └── ComplexityAnalyzer.cs       # Métricas
├── Models/
│   ├── DiagnosticResult.cs         # DTO da saída JSON
│   ├── Finding.cs                  # Error/Warning/Info
│   └── Recommendation.cs           # Ação recomendada
├── Output/
│   └── JsonEmitter.cs              # Serialização estruturada
└── rpt-diagnostic.csproj           # Project file
```

---

## 6. Comportamento Esperado

### 6.1 Sucesso (`exit code 0`)
```bash
$ rpt-diagnostic --file valid_report.rpt --output result.json
✓ Parsed report.rpt (42 objects, 7 formulas)
✓ Analysis complete: 2 errors, 1 warning, 3 info
✓ Diagnostic JSON written to result.json
```

Arquivo `result.json` contém JSON válido, schema conforme **3.2**.

### 6.2 Falha — Arquivo inválido (`exit code 1`)
```bash
$ rpt-diagnostic --file not_a_report.txt
✗ Error: File not_a_report.txt is not a valid Crystal Reports .rpt file
✗ Details: OLE header not found
```

Stderr contém mensagem legível. Sem JSON output.

### 6.3 Falha — Erro parsing (`exit code 2`)
```bash
$ rpt-diagnostic --file corrupted.rpt
✗ Error: Failed to parse corrupted.rpt
✗ Details: TSLV stream decoder failed at offset 0x2F4A
```

### 6.4 Verbose (`--verbose`)
```bash
$ rpt-diagnostic --file report.rpt --verbose
[TRACE] Opening file: /path/to/report.rpt
[TRACE] Validating OLE header... OK
[TRACE] Reading TSLV stream... 512 KB
[TRACE] Parsing ReportDefinition AST
[DEBUG] Found 5 sections: ReportHeader, PageHeader, Details, PageFooter, ReportFooter
[DEBUG] Found 18 database fields, 7 formula fields
[INFO] Analysis: 2 errors, 1 warning, 3 info
[TRACE] Emitting JSON...
✓ Done.
```

---

## 7. Instruções para Claude Code

Quando Claude Code executar este documento:

1. **Crie projeto .NET**:
   ```bash
   dotnet new console -n RptDiagnosticCli
   cd RptDiagnosticCli
   ```

2. **Instale dependências** (NuGet conforme **seção 4**)

3. **Implemente classes** conforme **seção 5**:
   - Estrutura de modelos (DTO)
   - Parsers de análise (Analyzers)
   - Emitter JSON
   - CLI handler (System.CommandLine)

4. **Validate**:
   ```bash
   dotnet test  # Testes unitários para parsers
   dotnet build --configuration Release --self-contained
   ```

5. **Publish**:
   ```bash
   dotnet publish -c Release -r linux-x64 --self-contained
   ```
   Gera executável em `bin/Release/net9.0/linux-x64/publish/rpt-diagnostic`

6. **Testes de aceitação**:
   - Carregue um `.rpt` real (seus próprios relatórios SESI-SENAI)
   - Valide JSON output vs schema **3.2**
   - Verifique detecção de campos órfãos, fórmulas inválidas
   - Teste flags `--verbose`, `--output`, `--format`

7. **Integração local**:
   ```bash
   # Copy executável para /usr/local/bin
   sudo cp bin/Release/net9.0/linux-x64/publish/rpt-diagnostic /usr/local/bin/
   
   # Claude Code depois roda:
   $ rpt-diagnostic --file report.rpt --output report.json
   $ cat report.json | jq .diagnostics
   ```

---

## 8. Exit Codes

| Código | Significado |
|--------|------------|
| 0 | Sucesso: parsing e análise completos |
| 1 | Erro de entrada: arquivo inválido/não encontrado |
| 2 | Erro de parsing: .rpt corrompido ou mal-formado |
| 3 | Erro de análise: falha em Analyzer interno |
| 64 | Erro de CLI: argumentos inválidos |

---

## 9. Observações para LLM Agêntico

### Ao consumir esta saída

```python
# Pseudocódigo para Claude Code
import json
import subprocess

result = subprocess.run(
    ["rpt-diagnostic", "--file", "report.rpt", "--output", "diag.json"],
    capture_output=True,
    text=True
)

if result.returncode == 0:
    with open("diag.json") as f:
        diag = json.load(f)
    
    # Análise semântica
    errors = diag["diagnostics"]["errors"]
    recommendations = diag["recommendations"]
    
    # Claude Code: 
    # - Priorize erros (ERRORS → alto risco)
    # - Agrupe recomendações por categoria
    # - Correlacione campos órfãos com fórmulas quebradas
    # - Gere relatório de impacto e plano de remediação
else:
    print(f"Parsing failed: {result.stderr}")
```

### Pontos de extensão

1. **Custom Analyzers**: Adicione `IReportAnalyzer` subclasses para domínios específicos (e.g., validação de convenção de nomes SESI-SENAI)
2. **Database Validation**: Integre conexão live com BD para validar queries em tempo de análise
3. **Version Control Integration**: Diffing de .rpt usando JSON intermediário
4. **Audit Logging**: Registre quem, quando, que relatórios foram analisados

---

## 10. Checklist de Sucesso

- [ ] Executável standalone `.NET 9.0` compila sem erros
- [ ] CLI aceita argumentos conforme **seção 3.1**
- [ ] JSON output valida contra schema **3.2**
- [ ] Detecta ≥3 tipos de issues: orphan fields, broken formulas, unresolvable connections
- [ ] Exit codes implementados conforme **seção 8**
- [ ] `--verbose` emite traces úteis
- [ ] Processa `.rpt` real (≥10KB) em <5 segundos
- [ ] Sem avisos de build (`dotnet build`)
- [ ] README atualizado com exemplos de uso

---

**Pronto para Claude Code executar.**