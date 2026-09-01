# RptDiagnosticCli

CLI .NET que faz parsing de arquivos `.rpt` (Crystal Reports) e emite um
diagnóstico estruturado em JSON, para consumo por Claude Code ou outro LLM
agêntico. Ver especificação completa em [`../docs/EspecificacaoAgentica.md`](../docs/EspecificacaoAgentica.md)
e desvios de implementação em [`../docs/NOTAS_DEPENDENCIA.md`](../docs/NOTAS_DEPENDENCIA.md).

## Estrutura

```
code/
├── RptDiagnosticCli/            # CLI principal
│   ├── Program.cs
│   ├── ExitCode.cs
│   ├── Commands/DiagnoseCommand.cs
│   ├── Analyzers/               # StructureAnalyzer, DataSourceAnalyzer, FormulaAnalyzer, ComplexityAnalyzer
│   ├── Models/                  # DTOs da saída JSON (schema rpt-diagnostic-v1)
│   └── Output/JsonEmitter.cs
├── RptDiagnosticCli.Tests/      # Testes unitários (xUnit) dos analyzers
└── external/majorsilence.crystal/  # Dependência de código-fonte: parser OLE/TSLV do .rpt
```

## Build

```bash
dotnet build code/RptDiagnosticCli
```

## Testes

```bash
dotnet test code/RptDiagnosticCli.Tests
```

## Uso

```bash
dotnet run --project code/RptDiagnosticCli -- \
  --file /path/to/report.rpt \
  --output report_diagnostic.json \
  --verbose
```

Opções:
- `--file <path>` (obrigatório): caminho para o `.rpt`
- `--output <path>`: JSON de saída (padrão: `<report_name>_diagnostic.json`)
- `--verbose`: traces `[TRACE]`/`[DEBUG]`/`[INFO]` em stderr
- `--format <json|json-pretty>` (padrão: `json-pretty`)

### Exit codes

| Código | Significado |
|---|---|
| 0 | Sucesso |
| 1 | Arquivo inválido/não encontrado |
| 2 | Erro de parsing (.rpt corrompido/mal-formado) |
| 3 | Erro interno de análise |
| 64 | Erro de argumentos de CLI |

## Publish (self-contained, linux-x64)

```bash
dotnet publish code/RptDiagnosticCli -c Release -r linux-x64 --self-contained
```

Gera o executável em
`code/RptDiagnosticCli/bin/Release/net10.0/linux-x64/publish/rpt-diagnostic`.

## Atualizando a dependência do parser

`code/external/majorsilence.crystal` é um clone raso (`git clone --depth 1`)
do repositório upstream, não um submódulo. Para atualizar:

```bash
rm -rf code/external/majorsilence.crystal
git clone --depth 1 https://github.com/majorsilence/majorsilence.crystal.git code/external/majorsilence.crystal
```

## Changelog

### 2026-08-20 — Travessia de subrelatórios

**Problema**: rodar o CLI contra um `.rpt` real (`Historico_Escolar_Superior.rpt`,
SGSET) mostrou que todo campo/fórmula/seção definido dentro de um
`SubreportObject.Report` ficava invisível no JSON de saída — `DataSourceAnalyzer`,
`FormulaAnalyzer` e `StructureAnalyzer` só liam as listas do relatório principal
(`report.Fields`, `report.Sections`), nunca as do subrelatório propriamente. Como
subrelatório é onde relatórios Crystal costumam colocar tabelas de detalhe
(ex.: disciplinas/atividades de um histórico escolar), a análise ficava cega para
justamente a parte mais provável de conter a lógica condicional a diagnosticar.

**Mudanças**:
- Novo `Analyzers/SubreportWalker.cs`: percorre recursivamente o relatório e todo
  `SubreportObject.Report` alcançável, devolvendo cada nó com um rótulo (`null`
  para o relatório principal, `"NomeDoSubreport"` — ou `"Externo > Interno"` se
  aninhado) usado pelos analyzers abaixo.
- `DataSourceAnalyzer`: `database_fields`, `formula_fields` (via propagação de
  dependência de fórmula) e `database_connections` agora agregam os definidos
  dentro de cada subrelatório, cada um marcado com `subreport` no JSON. Corrigido
  também um bug latente onde o conjunto de nomes de fórmula só considerava o
  relatório principal, fazendo um campo de fórmula placed dentro de um subreport
  ser contado como campo simples em vez de fórmula.
- `FormulaAnalyzer`: valida `{Field}`/`{Table.Field}` por nó da árvore — cada
  subrelatório tem seu próprio namespace de campos, então uma fórmula de
  subreport só pode depender de campos definidos nesse mesmo subreport.
- `StructureAnalyzer`: emite também as seções de cada subrelatório; serializa
  `Section.SuppressFormula` como `suppress_formula` (antes existia no modelo mas
  nunca chegava ao JSON); novo `structure.selection_formulas[]` com
  `RecordSelectionFormula`/`GroupSelectionFormula` de cada nó da árvore.
- `ComplexityAnalyzer`: todos os totais (`total_sections`, `total_objects`,
  `total_database_fields`, `total_formula_fields`, `total_tables`,
  `nesting_depth`) agora somam a árvore inteira, para não ficarem inconsistentes
  com `structure`/`data_sources`, que passaram a incluir os subrelatórios.
- DTOs (`Models/DiagnosticResult.cs`): campo `subreport` (nullable, omitido
  quando o item pertence ao relatório principal) em `SectionDto`,
  `DatabaseFieldDto`, `FormulaFieldDto` e `DatabaseConnectionDto`; novo
  `SelectionFormulaDto`.
- Schema documentado em [`../docs/EspecificacaoAgentica.md`](../docs/EspecificacaoAgentica.md#32-saída-json--schema)
  (seção 3.2), incluindo os novos campos.
- 6 novos testes em `RptDiagnosticCli.Tests` cobrendo subreport walking,
  `suppress_formula` e `selection_formulas` (suíte total: 15/15).

**Validação**: contra o `.rpt` do SGSET, o total de campos de banco expostos
subiu de 67 (só relatório principal) para 91 em 6 nós (principal + 5
subrelatórios), e fórmulas de 15 para 36 — incluindo as fórmulas do subrelatório
que decidem `RESULTADO = 18 → 'AE'` (Aproveitamento de Estudos), relevantes ao
achado em aberto sobre a supressão condicional de "Atividades de Extensão".
