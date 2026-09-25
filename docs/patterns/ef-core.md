---
name: ef-core
description: Armadilhas e convenções de EF Core/Npgsql neste repo — change tracker desconectado, mapeamento de value objects e sum types, owned collections, índices sem FK, migrations seguras, N+1 e ordenação
metadata:
  type: pattern
  domains: [ef-core, postgres, infrastructure, migrations, performance]
  status: stable
---

# EF Core e Postgres — armadilhas e convenções

> Quase todo bug de persistência deste projeto passou nos testes com fake e só apareceu contra o
> Postgres de verdade. Este documento junta essas lições. Workflow de migration passo a passo: skill
> `ef-migration`.

## Quando ler

- Antes de mapear entidade/propriedade nova, criar migration ou escrever método de repositório.
- Quando um `SaveChanges` "não grava nada" sem erro.
- Quando uma migration vai rodar em banco com dado (produção!).
- Ao escrever uma leitura que pode crescer com a base.

## Convenções de mapeamento

- Um `IEntityTypeConfiguration<T>` por entidade em `Infrastructure/Configuracoes/`.
- `EFCore.NamingConventions` com `UseSnakeCaseNamingConvention` — banco em snake_case sem nomear
  coluna a coluna.
- **Enums como string** (`HasConversion<string>()`), sem `CHECK` do conjunto de valores. Valor novo
  no enum não pede migration (ex.: `Prioridade.Baixa`, `PermissaoRegra.Reserva`). Em compensação,
  **renomear um membro quebra a leitura** de toda linha gravada com o nome antigo (`Enum.Parse`) —
  por isso `Normal` nunca virou `Media` ([ADR-0021](../decisions/0021-prioridade-normal-mantida-como-media.md)).
  Na API, `JsonStringEnumConverter` pelo mesmo motivo.
- **Todo `DateTimeOffset` é normalizado para UTC na escrita** (`DateTimeOffsetParaUtcConverter` em
  `DispatchDbContext.ConfigureConventions`). O Npgsql recusa offset ≠ 0 em `timestamptz` ("only
  offset 0 (UTC) is supported") — um payload com `-03:00` (ISO válido) dava **500**. Ficou latente
  porque o `Date.toISOString()` do front sempre manda UTC. Conversor de mesmo tipo não altera schema
  (`dotnet ef migrations has-pending-model-changes` confirmou). Consequência: tudo que sai do banco
  tem offset 0 — compare horários locais via `FusoHorario` (ver `motor-e-prazos.md`).

## Propriedade só com getter: declare na configuração

Entidade nova com `bool`/`int`/`Guid`/`DateTimeOffset?` só de `get` (ou `private set`): o EF às vezes
falha o constructor binding **em tempo de design** (`dotnet ef migrations add`), achando que não
consegue ligar o parâmetro. Solução: `builder.Property(x => x.Campo)` explícito na configuração antes
de gerar a migration. Já mordeu: `Conferente.NaEscala`/`CargaAtual`, `Usuario.Ativo`,
`Usuario.TentativasLoginFalhas`/`BloqueadoAte`, `EventoAutenticacao.Origem`, `SugestaoRegistro`.

## Value objects

- **`Prazo` usa `ValueConverter` (`PrazoConversoes`), não `OwnsOne`.** EF não liga navegação owned
  via parâmetro de construtor, só via propriedade com setter — abriria mão da imutabilidade de
  `Equipe`/`Protocolo` por causa do ORM.
- **Conversor que omite campo perde dado no round-trip.** `Prazo` ganhou `HorarioDeVencimento` (corte
  de horário); o conversor serializava só `Tipo` — reabrir um protocolo recarregado quebraria com
  `NullReferenceException`. Formato composto retrocompatível: `"Tipo"` ou `"Tipo|HH:mm"` (linhas
  antigas não têm `|`). Confira o tamanho da coluna: `"CorteDeHorario|10:00"` tem 20 chars, colunas
  subiram de `varchar(20)` para `varchar(30)`.

## Sum types: classe-registro achatada + `CHECK`

`SujeitoAlcada`/`AlvoAlcada` e `PayloadSugestao` não são mapeados direto. Há uma classe paralela só
para o EF em `Persistencia/` (`RegraAlcadaRegistro`, `SugestaoRegistro`) com colunas achatadas, um
**discriminador explícito** (`AlvoTipoRegistro { Etapa, TipoAto, Equipe, TodosOsAtos, Grupo,
EquipeEEtapa }`, `TipoSugestaoRegistro`) e um `CHECK` no Postgres (`num_nonnulls`/por discriminador)
garantindo o invariante também no banco. Quem traduz de volta para o tipo rico é o repositório.

- O padrão "par nulo/preenchido" sem discriminador não representa variante sem payload
  (`PorTodosOsAtos`) nem payload legitimamente nulo (`PorEquipeDeEscrevente(null)`).
- Variante nova pode **reaproveitar colunas** de outra (`PorEquipeEEtapa` usa `alvo_etapa` +
  `alvo_equipe_id`) — aí todo `as AlvoAntigo` na leitura precisa ser auditado (ver `endpoints.md`).
- O `CHECK` é a última linha de defesa: a validação da Api e o `CHECK` podem divergir com o tempo,
  por isso há teste de integração cobrindo os dois.

## Objeto desconectado do change tracker (mordeu 3×)

Se o objeto que você muta **não é a instância que o `DbContext` atual rastreia**, `SaveChanges` não vê
mudança nenhuma e não dá erro. Três formas de cair nisso aqui:

1. **Tradução de classe-registro**: `ObterPorIdAsync` de `Sugestao`/`RegraAlcada` devolve um objeto de
   Domain **novo a cada chamada**. `sugestao.Aplicar()` mutava a cópia; o status ficava `Pendente` no
   banco para sempre (aplicar duas vezes devolvia 204 duas vezes). Solução: métodos de repositório
   que buscam o registro rastreado e mutam direto (`AplicarAsync`/`DescartarAsync`/
   `AtualizarEvidenciaAsync`, `AtivarAsync`/`DesativarAsync`). Os métodos do Domain continuam
   existindo (documentam a regra, são exercitados pelos fakes), só não são o caminho de persistência.
2. **Projeção**: entidade construída dentro de `.Select()` (ex.: `Conferente` com `CargaAtual`
   calculada) sai desconectada. Por isso só as leituras de lista projetam; `ObterPorIdAsync`, usado
   pelos fluxos que mutam, é query simples.
3. **Cache**: objeto em `IMemoryCache` veio do `DbContext` (scoped) de uma requisição anterior, já
   descartado. `AtualizarConfiguracao` lê por `ObterParaEdicaoAsync` (sem cache) e chama
   `InvalidarCache()` depois de salvar.

Entidades com **mapeamento direto** (`Protocolo`, `TipoAto`, `Conferente` via `ObterPorIdAsync`)
voltam rastreadas: mutar e salvar basta. Fakes com lista em memória **não têm change tracker** — esse
bug nunca aparece em `Dispatch.Application.Tests`; há teste de integração dedicado.

## Coleções owned (`OwnsMany`)

`CiclosAnteriores`, `Pausas`, `AjustesDeDuracao` de `Protocolo`: tabela própria
(`ciclos_conferencia`, `pausas_conferencia`, `ajustes_de_duracao`), FK `protocolo_id` com cascade,
chave via shadow property `Id` (não têm identidade fora do protocolo). Coleção owned **não precisa**
de `UsePropertyAccessMode`: o EF acha o backing field `_ciclosAnteriores` pela convenção de nome
(`_<propriedadeEmCamelCase>`), a mesma que resolve `IReadOnlyList<T>` sem setter público. Crie índice
pensando no consumidor (`conferente_id`, para o Dashboard somar por pessoa).

## Chaves estrangeiras e índices

- EF cria índice automático em toda FK (`DonoId`, `EscreventeId`, `LoteImportacaoId`,
  `escreventes.equipe_id`...).
- Referências **sem FK de propósito** (auditoria, não dependência): `Protocolo.RegraAplicadaId`
  (remover uma regra não pode travar a leitura de protocolo antigo), `TipoAtoId` em protocolo e em
  regra (por isso `RemoverTipoAto` checa uso na aplicação). **Sem FK, sem índice de graça**: virou
  alvo de filtro/agregação, precisa de `HasIndex` manual. O contador de usos de regra fazia seq scan
  por regra até `ix_protocolos_regra_aplicada_id`.
- Índices existentes em `protocolos` pensados por consulta: `status` (pool de toda Minha fila),
  `numero` (não único — [ADR-0006](../decisions/0006-linha-de-corte-no-lugar-de-dedup-por-numero.md);
  continuidade e histórico), composto `(status, concluido_em)` (Dashboard e corte de concluídos).

## Performance: N+1 e varreduras disfarçadas

- **N+1 já corrigidos** — use como molde: contador de usos por regra (`ContarPorRegraAplicadaAsync`,
  `GroupBy` + `Count`, depois `Dictionary` + `GetValueOrDefault` tratando ausência como 0); sugestões
  por chave (`ObterMaisRecentesPorChavesAsync`, `WHERE chave IN (...)`); pedidos de reabertura
  (`ObterVariosPorIdsAsync`); nomes de usuário (`IUsuarioRepository.ObterVariosPorIdsAsync`).
- **Filtro em memória depois de `ObterTodosAsync` é full scan.** `RecalculoDeVencimentos` lia a tabela
  inteira de escreventes para filtrar por equipe — virou `ObterPorEquipeIdAsync` (índice de FK já
  existia). Mesma coisa para `ObterCoberturaDeAlcada`, que carregava `protocolos` inteira só para
  `DISTINCT tipo_ato_id` (virou `ObterTipoAtoIdsDistintosAsync`).
- Existence check em vez de carregar coleção (`ExisteComTipoAtoAsync`).
- Laços sobre lote: troque `FirstOrDefault` por `Dictionary<string, T>` construído antes (importação
  com centenas de linhas era O(n×m)).
- Latência do Neon é real — ~96 round-trips sequenciais numa chamada eram visíveis em produção e
  invisíveis no Postgres local.

## Ordenação

Postgres **não garante ordem sem `ORDER BY`**: listas "pulavam" a cada refetch. Ordene sempre, e
**desempate por `Id`** (`OrderBy(Nome).ThenBy(Id)`) — nomes empatam. Filas ordenam por
`VencimentoEm ?? DateTimeOffset.MaxValue` (sem vencimento por último).

## Migrations que rodam em banco com dado

- Coluna `NOT NULL` nova precisa de `DEFAULT` coerente com o invariante (`peso_complexidade DEFAULT 1`,
  não 0; `tentativas_login_falhas DEFAULT 0`; `interval DEFAULT '00:00:00'`).
- **`CHECK` novo exige backfill antes** (`UPDATE ... WHERE alvo_etapa IS NOT NULL`) — senão quebra em
  qualquer banco com linha existente (motor v2).
- **Dropar coluna com dado: backfill para o destino novo antes** e `Down()` simétrico (ciclos de
  conferência).
- Linha obrigatória (tabela de linha única) entra por `InsertData` na migration; o repositório usa
  `SingleAsync` (ausência é erro de setup).
- Widening de `varchar` é aditivo; valide o tamanho do maior valor possível.
- Migration que adiciona coluna referenciada pelo modelo precisa estar **aplicada em produção antes**
  do deploy do código (push em `main` já faz deploy — ver `deploy.md`).
- Valide contra um clone anonimizado de produção quando a migration mexe em dado (ver `deploy.md`).
- Recálculo de dado de negócio (RF-38) é lógica de aplicação, não migration.
- `EnableRetryOnFailure()` está ligado: operações multi-passo com transação explícita precisariam
  rodar dentro de uma execution strategy. Hoje não há `BeginTransactionAsync` — mantenha assim ou
  envolva na strategy.

## Miudezas de C# que já custaram bug

- `Dictionary<Guid, Guid>.GetValueOrDefault(chaveInexistente)` devolve `Guid.Empty`, não `null`, mesmo
  atribuído a `Guid?`. Tipar o dicionário como `Dictionary<Guid, Guid?>` (`Guid? (p) => p.Id` no
  seletor) — senão o JSON sai com `00000000-...`.

## Referências

- Skill `ef-migration` (comandos).
- ADR-0011, ADR-0017, ADR-0021, ADR-0023, ADR-0026, ADR-0032, ADR-0037.
- `testes.md` (por que fakes não pegam nada disto).
