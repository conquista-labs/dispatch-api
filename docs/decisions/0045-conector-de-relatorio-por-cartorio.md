---
name: adr-0045-conector-de-relatorio-por-cartorio
description: O arquivo que o sistema do cartório exporta (.xls) é convertido no back por um adaptador por formato (porta IConversorDeRelatorio na Application, implementação na Infrastructure com ExcelDataReader), exposto em POST /protocolos/importar/converter
metadata:
  type: decision
  status: accepted
---

# ADR-0045: Conector de relatório por cartório como adaptador no back

> A distribuidora envia o `.xls` que o sistema do cartório exporta e o back devolve as linhas genéricas da
> importação (`protocolo, tipoAto, escrevente, dataHoraAndamento`) mais a etapa e os totais conferidos. Cada
> formato de relatório é um **adaptador** (`IConversorDeRelatorio`, porta na Application; implementação na
> Infrastructure lendo o `.xls` com ExcelDataReader). A importação continua agnóstica de cartório.

## Status

`Accepted — 2026-09-25 (commit 0195331)`

Complementa [ADR-0005](0005-importacao-por-csv-com-pdf-fora-do-sistema.md): a colagem de CSV continua existindo e
o PDF continua fora do sistema; o que muda é que o `.xls` do cartório passa a entrar direto, sem conversão manual.

## Contexto

RF-05 pede "aceitar .csv/.xlsx ou colagem de linhas"; RF-06, "mapear colunas do relatório para protocolo, tipo
de ato, escrevente, etapa". Até aqui o back só aceitava linhas já prontas (JSON), e a distribuidora gerava um
CSV à mão a partir do relatório (ADR-0005, quando o relatório era PDF). O sistema do cartório passou a exportar
o "Relatório de Andamentos dos Protocolos" em **.xls** (Excel 97-2003, binário BIFF8), e ela quer enviar esse
arquivo direto.

O `.xls` não é uma tabela: é o layout de impressão jogado numa planilha — ~38 colunas, células mescladas,
cabeçalho do cartório repetido a cada página (inclusive no meio do bloco de um escrevente), um bloco por
escrevente com "Total Protocolos Escrevente: N" no fim e um total geral por andamento. O andamento
("Pré - Conferência"/"Pós - Conferência") vem escrito no arquivo, e a data/hora é horário de parede de Brasília.

A importação (`ImportarLote`) é deliberadamente agnóstica: só conhece `LinhaImportacao`. Outro cartório terá
outro sistema e outro relatório. A decisão do dono (25/09/2026) foi colocar o conector no back, como adaptador.

## Decisão

Vamos converter o relatório no back, com um adaptador por formato, porque o layout é frágil e precisa de uma
rede de segurança (conferência de totais) que tem de valer igual pra qualquer cliente da API, e porque o padrão
porta/adaptador já é como este repositório isola detalhe de fora (ADR-0001).

- **Porta** `IConversorDeRelatorio` (`Application/Portas/`): `Converter(Stream)` devolve a hierarquia fechada
  `ResultadoConversaoRelatorio` — `Convertido(conector, etapa, linhas, totalDeclarado, totalLido)`,
  `FormatoNaoReconhecido`, `AndamentoNaoEhConferencia`, `EtapasMisturadas`, `TotaisNaoConferem`,
  `RelatorioVazio`. Contrato: arquivo que não é do seu formato → `FormatoNaoReconhecido`, nunca exceção.
  *Porta/adaptador, em uma frase*: a Application declara **o que** precisa numa interface; a Infrastructure
  entrega **como**, numa classe que o container de DI liga à interface. Trocar ou somar formatos não toca no caso
  de uso nem no endpoint.
- **Caso de uso** `ConverterRelatorio`: recebe `IEnumerable<IConversorDeRelatorio>` (o DI entrega todos os
  registros da interface), copia o upload uma vez pra memória e devolve o resultado do primeiro conector que
  reconhecer o arquivo. Não grava nada.
- **Adaptador** `ConversorRelatorioDeAndamentosXls` (`Infrastructure/Conectores/`): máquina de estados que
  percorre as linhas ancorada em **conteúdo** de colunas conhecidas (título, andamento na col A, "Substituto:" na
  col V, número só-dígitos na col C com "L:" na col H, data/hora nas cols AB/AE, tipo de ato na col J, totais nas
  cols R e A) — nunca em número de linha, por causa das quebras de página. Confere cada bloco contra "Total
  Protocolos Escrevente", tudo o que foi lido contra a soma dos blocos, e a soma contra "Total Protocolos
  Andamento"; qualquer divergência recusa o arquivo inteiro. Data/hora vira `DateTimeOffset` com
  `FusoHorario.Brasilia`. A coluna do apresentante é ignorada.
- **Biblioteca**: **ExcelDataReader** 3.9 (MIT). Lê o `.xls` binário (e `.xlsx`, se um dia for preciso) só pra
  leitura, célula a célula, sem Excel instalado e sem dependência nativa. No .NET moderno ela exige registrar o
  `CodePagesEncodingProvider` (o `.xls` guarda texto em code page do Windows, ex. 1252, que o runtime não carrega
  por padrão) — feito no construtor estático do adaptador, que roda uma vez por processo antes do primeiro uso.
- **Endpoint** `POST /protocolos/importar/converter` (grupo Distribuidora, igual às outras rotas de importação):
  `multipart/form-data`, campo `arquivo` (`IFormFile`). Multipart porque é o jeito nativo do navegador mandar
  arquivo (`FormData`), sem inflar ~33% com base64 dentro de JSON. `.DisableAntiforgery()` porque o minimal API
  liga a validação antiforgery em toda rota que lê formulário e, sem o middleware, a chamada falha em runtime
  (verificado: `InvalidOperationException ... contains anti-forgery metadata`); antiforgery protege contra CSRF,
  que depende de o navegador anexar um cookie sozinho — aqui a autenticação é Bearer no header, que ele nunca
  anexa sozinho. Limite de 5 MB (413 `arquivo_grande_demais`), teto de 10 MB no Kestrel.
- **Etapa**: o arquivo traz o andamento, e a resposta devolve a etapa detectada; o pedido de importação
  (`/pre-visualizar`, `/confirmar`) continua recebendo `etapa` como campo do lote (RF-05a), que a tela pode
  pré-preencher. Pré e pós no mesmo arquivo é recusado (`etapas_misturadas`).
- **Erros**: 400 `{ codigo, motivo }` com `formato_nao_reconhecido` (inclui andamento que não é pré/pós, com
  motivo próprio), `etapas_misturadas`, `totais_nao_conferem`, `relatorio_vazio`, `arquivo_ausente`; códigos em
  snake_case como os já existentes (`ContaEndpoints`, `AuthEndpoints`).

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Conversão no front com SheetJS (a API segue recebendo só linhas) | Nenhuma mudança no back; prévia instantânea no navegador | Lógica de layout frágil fora do alcance dos testes do back; qualquer outro cliente da API teria de reimplementar; a conferência de totais viraria "o front promete que conferiu"; SheetJS saiu do npm (distribuição pelo CDN próprio) | O dono decidiu pelo back: a regra "recusar se os totais não batem" é proteção de dado e fica no servidor, como autorização (RNF-04) |
| Conector genérico no back, com mapeamento de colunas configurável (tabela de "coluna X = protocolo") | Um cartório novo sem código novo | O relatório não é tabela: é layout de impressão com blocos, cabeçalho repetido, valor do escrevente numa linha e do protocolo em três — mapeamento coluna→campo não descreve isso; exigiria uma mini-linguagem de layout e tela pra editar | Complexidade desproporcional com um cartório só; um adaptador em código, testado, é mais simples e mais seguro. Se aparecerem vários cartórios com layout tabular, reavaliar |
| Continuar como está (CSV gerado à mão, ADR-0005) | Zero código | Passo manual propenso a erro a cada lote | O relatório agora sai em planilha legível por máquina; o passo manual deixou de ser necessário |
| NPOI em vez de ExcelDataReader | Lê e escreve .xls/.xlsx, API rica de célula/estilo | Pacote bem maior, com dependências de desenho/criptografia; escrita e estilos não são necessários | Só precisamos ler valores; ExcelDataReader é leve e focado nisso |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Operabilidade | ✅ Melhora | A distribuidora envia o arquivo exportado, sem montar CSV à mão |
| Confiabilidade do dado | ✅ Melhora | Conferência de totais recusa arquivo lido pela metade; etapa vem do arquivo |
| Extensibilidade | ✅ Melhora | Formato novo = classe nova implementando a porta + um registro no DI |
| Testabilidade | ✅ Melhora | Adaptador testado sobre `.xls` sintéticos regeráveis (`gerar_fixtures.py`) |
| Acoplamento a fornecedor | ⚠️ Piora | O adaptador conhece o layout de um sistema de cartório específico; mudança de layout quebra a leitura (recusada, não silenciosa) |
| Superfície de entrada | ⚠️ Piora | Upload de arquivo binário não confiável — mitigado por limite de tamanho, leitura só de valores e exceção da biblioteca virando 400 |

## Consequências

**Positivas** — a importação ganha um segundo modo de entrada sem mudar `ImportarLote`; a tela converte e
segue o fluxo pré-visualizar → confirmar com as mesmas linhas. `.xlsx` ou outro cartório entram como outro
adaptador.

**Negativas** — dependência nova (ExcelDataReader) na Infrastructure. O adaptador depende de rótulos em
português do relatório ("Substituto:", "Total Protocolos Escrevente:"); se o sistema do cartório trocar esses
textos, a leitura para (com 400) até ajustar o adaptador e regerar os fixtures.

**Riscos** — mudança de layout que preserve os totais mas troque colunas de lugar (ex.: tipo de ato numa coluna
diferente) faria o protocolo não fechar e a conferência recusar — percebido na hora, pela distribuidora, como
`totais_nao_conferem`. Um relatório com andamento diferente de pré/pós é recusado com motivo próprio.

## Referências

- `docs/historico.md`, "Conector de relatório do cartório (.xls)".
- `docs/patterns/endpoints.md` (rota e códigos de erro), `docs/patterns/arquitetura.md` (portas),
  `docs/patterns/conceitos-dotnet.md` (IFormFile, antiforgery, code pages, `IEnumerable<T>` no DI),
  `docs/patterns/testes.md` (fixtures sintéticos).
- Complementa ADR-0005; segue ADR-0001 (porta na Application, adaptador na Infrastructure).
- gaps §8 (RF-05) — passa a ✅ para `.xls`.
