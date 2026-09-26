---
name: adr-0046-pool-em-ordem-e-limite-na-mao
description: O conferente só pega do pool o primeiro da vez (prioridade, vencimento, entrada) e só enquanto tem menos atos na mão que o limite da Configuração; a Minha fila dele não mostra o escrevente antes de conferir
metadata:
  type: decision
  status: accepted
---

# ADR-0046: Pool em ordem obrigatória e limite de atos na mão

> Decisão de produto do dono (26/09/2026): no `POST /minha-fila/{id}/pegar`, o conferente só pega o
> **primeiro da vez** do pool dele (Alta primeiro, depois quem vence antes, depois quem entrou antes) e só
> enquanto tem **menos atos na mão** (atribuídos + em conferência) que `Configuracao.LimiteDeAtosNaMao`
> (padrão 5). A ordem obrigatória é uma chave (`PoolEmOrdemObrigatoria`, padrão ligada). A lista inteira
> continua visível; na `GET /minha-fila` o escrevente (e por ele a equipe) sai `null` no pool e nas
> atribuídas.

## Status

`Accepted — 2026-09-26 (commits 45a9a73, 4f98f27)`

## Contexto

O documento de requisitos não fala em ordem para pegar: RF-19 diz que o pool disponível é o que está
dentro da alçada do conferente; RF-20, que "Pegar este" move do pool para a fila pessoal; RF-21 limita só
os atos **em conferência** ao mesmo tempo (`LimiteDeAtosSimultaneos`, padrão 1); RF-24f dá ao conferente
filtros sobre as três colunas, "incluindo o pool — é assim que o conferente encontra o que quer pegar".

Na prática, isso deixou uma brecha que o dono viu em uso: o conferente abre os cards do pool, vê de quem é
o ato (escrevente, equipe), pula pro fim da fila e pega o mais fácil — e nada impede que pegue o pool
inteiro de uma vez, esvaziando o pool dos colegas. `PegarProtocolo` aceitava **qualquer** card do pool que
estivesse na alçada.

O dono decidiu em 26/09/2026, como regra de produto nova (não é interpretação de RF):

1. pegar só na ordem da fila;
2. um limite de atos "na mão" por conferente, configurável pelo Administrador;
3. na fila do próprio conferente, esconder escrevente e equipe dos atos que ainda não estão em
   conferência (acréscimo no mesmo dia, pelo mesmo motivo).

## Decisão

Vamos impor a vez e o limite **no servidor, no `PegarProtocolo`**, com a ordem calculada no Domain e a
mesma lista usada pela leitura e pela ação, porque o botão que o front mostra e o que o servidor aceita
não podem discordar, e a restrição entre papéis/pessoas é sempre no servidor (RNF-04).

- **A vez** — `OrdemDoPool.Ordenar` (Domain, `Fila/RegraDoPool.cs`): prioridade decrescente (Alta,
  Normal, Baixa — posto explícito, não o valor numérico do enum); dentro da prioridade, `VencimentoEm`
  crescente com nulo por último; empate por `AndamentoEm` (quem entrou antes); depois `Numero` (ordinal)
  e `Id`, pra que empate total nunca dependa da ordem do banco. "Alta primeiro" foi lido como ordem
  decrescente de prioridade (Baixa depois de Normal), não "Alta e o resto empatado".
- **Na mão** = atribuídos + em conferência do conferente (pausado é `Conferindo`, conta). É diferente do
  RF-21 (só em conferência, trava o Iniciar), que continua como está.
- **`RegraDoPool`** (Domain): `{ OrdemObrigatoria, LimiteNaMao, NaMao, ProximoId }` + `Avaliar(id)` →
  `Permitido` / `LimiteNaMao` / `ForaDaVez`. O limite vence a vez (mão cheia recusa até o primeiro — a
  mensagem certa é "conclua algum"). `ProximoId` é o primeiro da ordem enquanto `NaMao < LimiteNaMao`,
  senão `null`; **com a chave desligada continua indicando o primeiro** (o campo quer dizer "o próximo da
  vez", não muda de sentido conforme a chave; o front pode ignorar).
- **Uma lista só** — `PoolDoConferente` (Application, `internal`) monta o pool disponível (alçada via
  `VerificadorDeAlcada` + `OrdemDoPool`) e a regra. `ObterMinhaFila` (as duas leituras de fila) e
  `PegarProtocolo` passam por ele. "Fora da alçada" no pegar passou a ser "não está no pool disponível
  dele" — o mesmo filtro, não uma segunda checagem parecida.
- **`PegarProtocolo`**: não encontrado (404) → não está no pool (409 `{ motivo }`) → sem alçada (403) →
  mão cheia (409 `{ codigo: "limite_na_mao", motivo }`) → fora da vez com a chave ligada (409
  `{ codigo: "fora_da_vez", motivo }`). Os três primeiros não mudaram de status nem de corpo. Quem perde a
  corrida pelo mesmo primeiro recebe o "não está no pool" de sempre. `ResultadoPegarProtocolo` virou
  hierarquia fechada (era enum) porque `LimiteNaMao` carrega os números da mensagem.
- **Só o conferente pegando** é travado. Atribuição manual da distribuidora (`AtribuirManualmente`,
  `atribuir-ao-menos-carregado`), o motor e a continuidade continuam podendo passar do limite. "Atribuídas
  a você" continuam livres: o Iniciar não olha a vez, só o RF-21.
- **Configuração**: `LimiteDeAtosNaMao` (int ≥ 1, padrão 5) e `PoolEmOrdemObrigatoria` (bool, padrão
  `true`) na linha única (ADR-0023), com `DefinirRegraDoPool` validando no Domain. `PUT /config` (só
  Administrador, como o resto do PUT) aceita os dois como opcionais — ausente/`null` mantém —, 400 com
  motivo para limite < 1. Migration `AdicionaRegraDoPoolEmConfiguracao` com `DEFAULT 5` / `DEFAULT true`
  (a linha existente nasce com a regra ligada); o default mora só na migration, como nas metas (ADR-0042).
- **Leitura**: `GET /minha-fila` e `GET /conferentes/{id}/fila` ganham `regraDoPool` e devolvem
  `poolDisponivel` já na ordem da vez. "Atribuídas a você" continuam por vencimento.
- **Escrevente escondido**: em `GET /minha-fila`, `escreventeId` sai `null` nos itens de `poolDisponivel` e
  `atribuidos` (em `emConferencia` continua — o ato já é dele e o escrevente ajuda a trabalhar).
  `ProtocoloResumo.EscreventeId` ficou `Guid?` só na resposta; o domínio não mudou. `GET
  /conferentes/{id}/fila` (gestão) continua com tudo. É um recorte da rota (a fila vista por quem vai
  pegar), não um corte por papel: uma conta de gestão que também confere continua vendo o escrevente
  pelas telas de gestão. O conferente não tem outro caminho para ligar escrevente a um ato do pool: o
  detalhe (`GET /protocolos/{id}/detalhe`) é só Distribuidora, e `GET /escreventes` lista nomes sem ligar a
  ato.

## Alternativas consideradas

As três primeiras foram postas pelo dono e recusadas por ele.

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Mostrar ao conferente **só o próximo** do pool | Nada para escolher; zero chance de o front e o servidor discordarem | O conferente perde a noção do tamanho e da urgência do pool; RF-19 fala em coluna "pool disponível" | Dono preferiu a lista inteira visível com o botão só no primeiro |
| Lista inteira **sem detalhes** (sem abrir o card) | Tira a informação que motivava a escolha | Continua permitindo pegar qualquer um; esconde também o que ajuda a trabalhar | Não resolve "pegar na ordem"; o recorte escolhido foi esconder só escrevente/equipe antes de conferir |
| Limite na mão valendo também para a **distribuidora e o motor** | Um teto único e previsível por pessoa | A distribuidora perde a válvula de "mandar este pra fulano agora" (ADR-0027); o motor passaria a mandar mais coisa pro pool/exceção sem ninguém pedir | Dono decidiu: limite só no pegar |
| Ordenar/checar a vez no front (servidor aceita qualquer um) | Nenhuma mudança no back | Qualquer chamada direta à API fura a regra (RNF-04) | Restrição é sempre no servidor |
| Checar a vez com uma segunda consulta própria no `PegarProtocolo` (sem compartilhar com a leitura) | Menos acoplamento entre os casos de uso | Duas implementações de "pool disponível + ordem" que podem divergir — o botão aparece e o servidor recusa | `PoolDoConferente` único |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Justiça na distribuição | ✅ Melhora | Ninguém escolhe o ato fácil nem esvazia o pool |
| Configurabilidade | ✅ Melhora | Limite e chave editáveis pelo Administrador sem deploy |
| Compatibilidade | ✅ Mantida | Campos novos no fim dos DTOs; opcionais no `PUT /config`; desfechos antigos do pegar intactos |
| Consistência leitura/ação | ✅ Melhora | Mesma lista e mesma ordem na leitura e no pegar |
| Desempenho do pegar | ⚠️ Piora | O pegar agora carrega o pool, os escreventes e os tipos (o mesmo custo de um `GET /minha-fila`) em vez de um protocolo só |
| Concorrência | ➖ Neutro | Igual a antes: sem token de concorrência em `Protocolo`, duas chamadas **simultâneas** podem ambas passar (a corrida sequencial dá o 409 esperado) |

## Consequências

**Positivas** — o front desenha o botão a partir de `regraDoPool.proximoId`, sem recalcular regra; o
servidor recusa com `codigo` estável, e o front escolhe a mensagem por ele; a ordem é testada no Domain com
as precedências.

**Negativas** — RF-24f perde parte do sentido para o pool: o conferente ainda filtra, mas só pode pegar o
primeiro da vez **do pool inteiro dele**, não o primeiro do recorte filtrado (o front precisa usar
`proximoId`, não o topo da lista filtrada). O filtro por equipe no pool/atribuídas também deixa de
funcionar na Minha fila do conferente (o `escreventeId` é o que liga o ato à equipe). A ordem do pool deixa
de ser "sempre pelo vencimento" (seção 5): prioridade vem antes — gaps §16/§44.

**Riscos** — duas requisições **simultâneas** de pegar podem furar o limite ou pegar o mesmo protocolo
(já era assim antes desta regra); se aparecer em uso, a resposta é um token de concorrência (`xmin`) em
`Protocolo`, com ADR próprio. Um protocolo no topo que só alguns conferentes podem pegar não trava os
outros: a vez é calculada sobre o pool **de cada um** (dentro da alçada dele).

## Referências

- RF-19, RF-20, RF-21, RF-24f; seção 5 (ordenação por vencimento); RNF-04.
- `docs/historico.md`, entrada de 2026-09-26 "Pool em ordem obrigatória e limite de atos na mão".
- `docs/patterns/motor-e-prazos.md` → "Semáforo" e "Ciclo de vida"; `docs/patterns/endpoints.md`;
  `docs/patterns/autorizacao.md` → "Grupos e policies".
- `docs/gaps-requisitos.md` §16, §44.
- Complementa ADR-0023 (linha única de configuração), ADR-0027 (atribuição manual sem alçada — continua sem
  limite), ADR-0042 (default só na migration).
