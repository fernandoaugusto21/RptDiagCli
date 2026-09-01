# RptDiagCli

CLI .NET que faz parsing de arquivos `.rpt` (Crystal Reports) e emite um
diagnóstico estruturado em JSON, para consumo por Claude Code ou outro LLM
agêntico.

## Estrutura do repositório

```
.
├── code/    # CLI, testes e dependência de parser (ver code/README.md)
└── docs/    # Especificação e notas de implementação
```

- [`code/README.md`](code/README.md) — build, testes, uso da CLI, clone com
  submódulo e changelog.
- [`docs/EspecificacaoAgentica.md`](docs/EspecificacaoAgentica.md) —
  especificação completa (schema JSON, arquitetura).
- [`docs/NOTAS_DEPENDENCIA.md`](docs/NOTAS_DEPENDENCIA.md) — desvios de
  implementação em relação à especificação.

## Início rápido

```bash
git clone --recurse-submodules https://github.com/fernandoaugusto21/RptDiagCli.git
cd RptDiagCli
dotnet run --project code/RptDiagnosticCli -- --file /path/to/report.rpt --verbose
```

Detalhes de build, testes e opções da CLI: [`code/README.md`](code/README.md).

## Licença

[MIT](LICENSE). Compatível com a licença tri-licenciada
(MIT/Apache-2.0/BSD-3-Clause) do submódulo
[`majorsilence.crystal`](code/external/majorsilence.crystal).
