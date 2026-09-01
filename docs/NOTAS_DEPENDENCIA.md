# Notas de Implementação — Desvios da Especificação

## Parser de .rpt

A especificação (seção 4) cita os pacotes NuGet `Majorsilence.Crystal.Parser` e
`Majorsilence.Crystal.Model` (versão 1.0.0). Esses pacotes **não existem no
NuGet.org** — foi verificado via `azuresearch-usnc.nuget.org` que o autor
"Majorsilence" publica apenas bibliotecas de relatórios RDL
(`Majorsilence.Reporting.*`) e o conversor `Majorsilence.CrystalCmd`, nenhum
deles um parser estrutural de `.rpt`.

O código-fonte real existe, porém, no repositório GitHub
[majorsilence/majorsilence.crystal](https://github.com/majorsilence/majorsilence.crystal)
— um projeto que reverte-engenharia o formato binário OLE/TSLV do Crystal
Reports e expõe exatamente os projetos `Majorsilence.Crystal.Model` e
`Majorsilence.Crystal.Parser` citados na especificação, com a API
`RptParser.Parse(path) -> ParseResult { Success, Report, Warnings, Errors }`.
Licença tri-licenciada (MIT/Apache-2.0/BSD-3-Clause).

**Decisão**: como o pacote não está publicado no NuGet, o repositório foi
adicionado como dependência de código-fonte em
[`code/external/majorsilence.crystal`](../code/external/majorsilence.crystal)
— um **git submodule** apontando para o upstream, fixado no commit `44e4680`
— e referenciado via `ProjectReference` (não `PackageReference`) a partir de
`RptDiagnosticCli.csproj`. Ver [`../code/README.md`](../code/README.md#clonando-o-repositório)
para instruções de clone/atualização do submódulo.

## Target Framework

A especificação pede **.NET 9.0**. A biblioteca `Majorsilence.Crystal.*` exige
**.NET 10.0** (`net10.0` em todos os `.csproj`). Como o SDK 10.0.301 está
disponível no ambiente, o `RptDiagnosticCli` (e o projeto de testes) também
foram alvejados para `net10.0` para manter a solução consistente.

## Limitação conhecida herdada do parser: `data_sources.database_connections`

O stream OLE `QESession`, que carrega a string de conexão e a lista de
tabelas/joins, é criptografado com uma chave proprietária que a biblioteca
upstream não decodifica (ver `README.md` do majorsilence.crystal, seção
"Known Limitations"). Na prática, `ReportDefinition.DataSources` costuma vir
**vazio** para relatórios reais, mesmo quando o relatório usa múltiplas
tabelas — os diagnósticos `DB_CONNECTION_UNRESOLVABLE` e `MULTI_TABLE_JOIN`
estão implementados e funcionam quando há dados de `DataSource`, mas não
disparam para a maioria dos `.rpt` do mundo real até que essa decodificação
seja resolvida a montante. Campos de banco de dados (`database_fields`) e
tabela de origem (`table_source`) continuam sendo extraídos corretamente a
partir das referências `{Table.Column}` embutidas em objetos e fórmulas.

## Detecção de campos órfãos (`FIELD_ORPHAN`) — subrelatórios e fórmulas

Validado contra um relatório real de produção (SESI-SP,
`Historico_Escolar_Superior.rpt`, 3.5 MB), a primeira versão do
`DataSourceAnalyzer` marcava 64 de 67 campos de banco como `ORPHAN` porque só
contava uso via `FieldObject` posicionado diretamente nas seções do
relatório principal — ignorando (a) campos usados apenas dentro de
subrelatórios (`SubreportObject.Report`, uma árvore `ReportDefinition`
separada) e (b) campos usados apenas como dependência de uma fórmula
colocada no relatório.

O analisador foi estendido para: percorrer recursivamente os subrelatórios
somando o uso encontrado neles (rotulado como
`"<SectionType> (subreport:<Nome>)"` em `used_in_sections`), e propagar o
posicionamento de uma fórmula para cada campo do qual ela depende. Isso
reduziu os órfãos desse relatório de 64 para 33 — o campo `ALUNO`, por
exemplo, passou de `usage_count: 0` para `usage_count: 3`, usado em
`Subreport1` e `Subreport5`. Os 33 restantes são candidatos legítimos a
limpeza (ou usados por mecanismos que o parser não expõe, como fórmulas de
seleção de registro ou vínculos de parâmetro de subrelatório).

## System.CommandLine

A especificação cita `System.CommandLine 2.0.0-beta4.22272.1`. Essa versão
beta está obsoleta; a API mudou significativamente a caminho do GA. Foi usada
a versão estável **2.0.11** (GA, lançada em novembro/2025), com a API atual
(`Option<T>`, `Command.SetAction(ParseResult -> int)`).
