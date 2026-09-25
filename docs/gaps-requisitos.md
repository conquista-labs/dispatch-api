# Gaps do dispatch-api em relação ao documento de requisitos

**Referência**: `../dispatch-prototype/Dispatch - Requisitos.dc.html` (v2, rascunho)
**Escopo**: só o lado do back-end — o que a API não sustenta, sustenta diferente, ou decidiu adiar
**Aberto em**: 2026-09-25, colhendo o CLAUDE.md antigo ("gap", "fica pra depois", "fora de escopo",
"simplificação consciente", "divergência", "Decisões adiadas conscientemente") e uma releitura do
documento de requisitos contra o código

---

## Como ler

**Duas camadas.** As visões abaixo são o índice; o detalhamento (§1 em diante) é o registro com a
evidência. **A numeração nunca muda** — item fechado continua no lugar, marcado como fechado e com
onde foi resolvido. Item novo entra no fim com o próximo número. Cite como "gaps §12" em commit,
ADR ou comentário.

Cada item diz **como sabemos**: "CLAUDE.md" (registrado numa entrega — ver `docs/historico.md`),
"código" (lido/grep no repo), ou "requisito" (só leitura do documento, sem investigar o código a
fundo). Onde não investigamos, está dito.

| Marca | Significa |
| ----- | --------- |
| 🔴 | Em aberto — o back não sustenta o requisito |
| 🟡 | Parcial — parte existe, parte não |
| ✅ | Fechado — resolvido (com onde) |
| ⚪ | Fechado sem mudança — divergência consciente, decisão registrada, ou fora do back |
| ⏸ | Decisão adiada conscientemente — com gatilho para reavaliar |
| ❔ | Não verificado — suspeita a confirmar |

---

## Panorama — 2026-09-25

| Visão | Itens |
| ----- | ----- |
| 🔴 Em aberto | §2, §3, §4, §7, §12, §25, §26, §27, §33, §34, §35, §36, §37 |
| 🟡 Parcial | §16, §19 |
| ❔ Não verificado | §5, §6, §9, §11 |
| ⏸ Adiado | §18, §24, §29, §40, §43 |
| ⚪ Divergência consciente / fora do back | §8, §13, §21, §28, §30, §31, §38, §41, §42 |
| ✅ Fechado | §1, §10, §14, §15, §17, §20, §22, §23, §32, §39 |

**Leitura rápida.** O papel Administrador + Contas (§1) está fechado; a frente grande que resta é o
**Dashboard v2** (ritmo, metas, série, exportação, pesos — §34–§37), um projeto à parte. Fora isso, o
que resta são itens conhecidos e pequenos (mesclar tipos, RF-01m/n, auditoria de autenticação sem
leitura).

---

## Detalhamento

### Autenticação e contas

#### §1 ✅ Papel Administrador, tela Contas e o que a distribuidora não deve ver

- **Requisito**: seção 3 (três papéis; "para a distribuidora a API não devolve nível, score nem faixa, e
  rejeita escrita em regras, conferentes e contas"); 6.8 Contas — RF-44 a RF-48; RF-30a (Central só
  leitura para distribuidora); RF-43a (Produção por conferente sem nível/score/faixa); RF-29a
  (Conferentes vira só presença).
- **Fechado em 2026-09-25** — [ADR-0039](decisions/0039-perfil-administrador.md) e
  [ADR-0040](decisions/0040-troca-de-senha-obrigatoria-no-primeiro-acesso.md): `Papel.Administrador`
  (carrega também a claim `Distribuidora`), escritas de regras/conferentes/catálogo/sugestões e o grupo
  `/contas` só do admin, nível/score/faixa cortados na Application, senha inicial 8+ com troca
  obrigatória no primeiro acesso, conta desativada perde o token. Tabelas em
  `docs/patterns/autorizacao.md`.
- **Fora desta entrega**: reativar conta e trocar o papel de uma conta existente; a inferência residual
  das regras de nível (§43); RF-01n (§3) continua em aberto.

#### §2 🔴 RF-01m — liberação sem autenticador pela distribuidora

- **O que falta**: quem não registrou TOTP não se recupera sozinho; o código de uso único liberado pela
  distribuidora não existe.
- **Como sabemos**: CLAUDE.md ("TOTP e recuperação de senha") — **fora de escopo por decisão explícita
  do dono**, sem tela no protótipo na época.
- **Onde entraria**: caso de uso na Application + endpoint de gestão; tabela de código de uso único.

#### §3 🔴 RF-01n — códigos de recuperação de emergência do administrador

- **O que falta**: os 8 códigos de uso único da implantação (`codigo_recuperacao` no modelo da seção 8).
- **Como sabemos**: CLAUDE.md (mesma decisão do §2). Depende do §1 (administrador).
- **Onde entraria**: entidade `CodigoRecuperacao`, geração na implantação, etapa alternativa em
  `ValidarCodigoRecuperacao`.

#### §4 🔴 RF-01i — bloqueio "por conta **e por origem**"

- **O que falta**: bloqueio de TOTP e de login por senha é **por conta**; a origem (`X-Forwarded-For`) só
  é registrada em `EventoAutenticacao.Origem`, não bloqueia.
- **Como sabemos**: CLAUDE.md ("Bloqueio de tentativas de login por senha"); requisito.
- **Onde entraria**: contagem por origem (a partir dos eventos já gravados) em `Autenticar`/
  `ValidarCodigoRecuperacao`.

#### §5 ❔ RF-01h — "responde em tempo constante"

- **Situação**: a resposta é idêntica para e-mail inexistente (anti-enumeração implementada), mas não foi
  verificado se o **tempo** é constante — com e-mail inexistente não há verificação de hash nem escrita
  de auditoria, o que tende a responder mais rápido.
- **Como sabemos**: leitura do CLAUDE.md ("só grava evento de auditoria se o usuário existir"); não medido.
- **Onde entraria**: hash fictício no caminho de e-mail inexistente (`Autenticar`,
  `IniciarRecuperacaoSenha`, `ValidarCodigoRecuperacao`).

#### §6 ❔ RNF-15 — "hash de derivação lenta (Argon2id ou bcrypt com custo ajustado)"

- **Situação**: senhas usam `PasswordHasher<T>` do ASP.NET Core Identity (PBKDF2). Não é nenhum dos dois
  algoritmos citados; parâmetros de iteração não conferidos.
- **Como sabemos**: CLAUDE.md ("Autenticação e autorização"); requisito. ADR-0003.
- **Onde entraria**: novo adapter de `IHashDeSenha` com rehash no login (o formato do hash identifica o
  algoritmo).

#### §7 🔴 RNF-16 / RF-01k — trilha de auditoria de autenticação sem leitura

- **O que falta**: `EventoAutenticacao` é gravado (com autor, origem, horário), mas não existe endpoint
  para a distribuidora consultar ("o evento entra na trilha de auditoria visível à distribuidora").
- **Como sabemos**: código (comentário em `TipoEventoAutenticacao.cs`: "Sem tela de consulta ainda (gap
  consciente)").
- **Onde entraria**: caso de uso de leitura paginada (`Paginado<T>`, ADR-0029) + endpoint de gestão.

### Importação

#### §8 ⚪ RF-05 — `.csv/.xlsx` e upload de arquivo

- **Situação**: o back recebe CSV como texto colado; `.xlsx` não é aceito. O relatório real é PDF,
  convertido fora do sistema. Decisão registrada: ADR-0005.

#### §9 ❔ RF-10a — excluir linha do lote no passo 2

- **Situação**: não investigado. Como a confirmação reprocessa exatamente as linhas enviadas (nada fica
  guardado entre prévia e confirmação), é provável que baste o front não enviar a linha — sem mudança no
  back.
- **Como sabemos**: requisito + desenho de `ImportarLote` (CLAUDE.md).

#### §10 ✅ `LoteImportacao` (seção 8)

- Adiado na importação e trazido no dia seguinte — ver histórico "Visão de distribuição (RF-13/RF-14)".

#### §11 ❔ RNF-01 — lote de 500 linhas em segundos

- **Situação**: nunca medido. As buscas O(n×m) de `ImportarLote` viraram dicionários e a continuidade é
  uma busca única (auditoria de 2026-09-03), mas não há número.

### Distribuição e motor

#### §12 🔴 Modos de operação (seção 4: Híbrido / Tudo atribuído / Tudo no pool)

- **O que falta**: o motor é sempre Híbrido (urgentes atribuídos, resto no pool). Não há configuração.
- **Como sabemos**: código (grep sem `ModoOperacao`; `Configuracao` sem campo de modo); requisito
  (seção 4, seção 8 `config`, RF-30c).
- **Onde entraria**: campo em `Configuracao` + passo 6 de `MotorDistribuicao` (ver
  `docs/patterns/motor-e-prazos.md`).

#### §13 ⚪ Algoritmo de alçada diverge da seção 4

- **Situação**: o back implementa a cascata de 3 camadas + reserva + equipe-e-etapa absoluto (v3/v4) da
  ferramenta interativa aprovada; a seção 4 do documento descreve o modelo anterior. Decisão consciente
  de não editar o documento (gerado por ferramenta externa do dono) — ADR-0017, ADR-0018, ADR-0030.

#### §14 ✅ RF-17 — "definir alçada" como resolução de exceção

- Ficou de fora em 2026-08-27 por não haver CRUD de regra; o CRUD existe desde a Central de Regras
  (`/regras-alcada`). A ação no card de exceção é de interface.

#### §15 ✅ RF-14 — equipe e escrevente no card

- Fechado em "Protocolo.EscreventeId — fecha RF-14 e RF-38" (`ProtocoloResumo.EscreventeId` + `GET /escreventes`).

#### §16 🟡 "Filas e listas ordenam sempre pelo vencimento mais próximo" (seção 5)

- **Feito**: pool (Distribuição e Minha fila) e "Atribuídas a você".
- **Falta**: buckets `emConferencia`, `concluidos`, `excecoes`, `atribuidos` da Distribuição sem
  `ORDER BY` (registrado como "gap conhecido à parte" no corte de concluídos).
- **Onde entraria**: `ObterVisaoDistribuicao`.

#### §17 ✅ `/protocolos/distribuicao` cresce sem limite

- Mitigado pela janela de 30 dias no bucket `concluidos` — ADR-0026.

#### §18 ⏸ Janela de 30 dias fixa e sem override

- Constante `ObterVisaoDistribuicao.DiasHistoricoDeConcluidos`, não campo de `Configuracao`; sem
  parâmetro "ver histórico completo". **Reavaliar** se a operação pedir histórico maior na aba
  Concluídos (acréscimo pequeno, mesmo molde do `loteImportacaoId` opcional).

#### §19 🟡 "Toda transição gera registro de auditoria com autor e horário" (seção 8)

- **Existe**: `PedidoReabertura` (pedido e decisão), `CicloConferencia`, `PausaConferencia`,
  `AjusteDeDuracao` (com autor e motivo), `EventoAutenticacao`, `RegraAplicadaId`, `CorrigidoEm`/
  `ReabertoEm`.
- **Falta**: log genérico — `AtribuidoEm` guarda só a última atribuição; atribuição manual, devolução ao
  pool, descarte, exclusão/restauração e mudança de prioridade não registram **quem** fez. Sem
  `evento_decisao` por decisão (ADR-0009).
- **Onde entraria**: tabela de eventos de protocolo, se e quando uma leitura precisar.

### Minha fila

#### §20 ✅ RF-21 — cronômetro ao vivo

- Fechado em "ProtocoloResumo ganha IniciadoEm".

#### §21 ⚪ RF-24c — "cronômetro reiniciado" na reabertura

- Reabrir devolve para Atribuído sem ligar o cronômetro (ADR-0031) e recalcula o vencimento (ADR-0034).
  Diverge do texto por pedido do dono, com motivo registrado.

#### §22 ✅ RF-24k — rodada de conferência e motivo da não aprovação

- **Fechado em 2026-09-25** (ADR-0038): `NumeroDaConferencia` calculado na leitura (sem coluna
  `rodada`) em `ProtocoloResumo`, no detalhe e em cada linha do histórico; o motivo da não aprovação é a
  `Observacao` da linha reprovada (decisão do dono — `Reprovar` continua sem motivo próprio).
- **Diverge de**: §8 do documento (coluna `rodada` gravada) — ver as alternativas no ADR.
- Não destrava §33 sozinho: "aprovado na 1ª" pede o resultado original antes de uma correção, que
  continua não guardado.

### Prazos

#### §23 ✅ RF-38 — alterar prazo de equipe recalcula vencimentos abertos

- Fechado em "Protocolo.EscreventeId — fecha RF-14 e RF-38"; otimizado em 2026-09-22.

#### §24 ⏸ Feriados no cálculo de prazo

- Só fim de semana é considerado (`ProximoDiaUtil`) — simplificação consciente (ADR-0013). **Reavaliar**
  quando um vencimento cair num feriado e a operação reclamar; exige calendário por cartório/cidade.

### Central de regras

#### §25 🔴 RF-34c — mesclar dois tipos de ato

- **O que falta**: migrar `Protocolo.TipoAtoId` e `AlvoAlcada.PorTipoAto` de um tipo para outro + evento
  no histórico de aprendizado. Ficou mais importante com o cadastro automático na importação
  (ADR-0012), que cria um tipo por grafia.
- **Como sabemos**: CLAUDE.md ("Tipos de ato — CRUD completo": "próximo passo, não foi esquecido").
- **Onde entraria**: caso de uso `MesclarTiposAto`; sem FK em `TipoAtoId`, a migração é da aplicação.

#### §26 🔴 RF-34a/RF-34g — tempo de referência e origem do tipo

- **O que falta**: tempo de referência por tipo com origem (informado · mediana de N atos · estimado),
  stepper e "usar histórico"; origem do tipo (catálogo, manual, reconhecido via importação). Já existe
  contagem de uso e de conferentes com alçada (`ListarTiposAtoComUso`).
- **Como sabemos**: requisito; grep sem `TempoReferencia`. Base também do §35.

#### §27 🔴 RF-32a–c — construtor guiado: efeito antes de criar, regra parecida, "Por quê"

- **O que falta**: RF-32c (campo "Por quê" gravado com a regra — `nota` na seção 8) não tem coluna nem
  campo no request. RF-32a (quem ganha/perde alcance antes de criar) e RF-32b (regra parecida) não foram
  investigados — podem exigir uma simulação no back (`SimularAlcada` é por caso, não por regra
  hipotética).
- **Como sabemos**: requisito; grep sem `Nota`/`PorQue`.

#### §28 ✅/⚪ Tabela `config` (seção 8)

- ✅ Fechado em 2026-09-03 (ADR-0023): 12 constantes editáveis via `GET`/`PUT /config`.
- ⚪ Divergência: linha única tipada em vez de `config(chave, valor)`. Ainda **fora** dela: modo de
  operação (§12), pesos do score (§34), metas (§36), janela de 30 dias (§18), `DuracaoTipica` (estrutura,
  de propósito).

### Aprendizado

#### §29 ⏸ "Job diário" de sugestões

- Roda sob demanda (`POST /sugestoes/gerar`); não existe scheduler/`IHostedService`. **Reavaliar** quando
  a distribuidora esquecer de gerar, ou quando outra tarefa periódica aparecer. No Render free a
  instância hiberna — um job em processo não rodaria de forma confiável.

#### §30 ⚪ Proposta "Tipo desconhecido" quase não dispara

- Com o cadastro automático de tipo na importação (ADR-0012), a proposta só aparece para protocolos sem
  nome de tipo aproveitável. Coerente com RF-34g ("reconhecido via importação").

### Dashboard

#### §31 ⚪ RF-43 — "custo por ato"

- Não havia dado de custo; não inventamos valor. O documento v2 removeu o KPI (RF-42b: "Custo por ato
  sai do dashboard").

#### §32 ✅ RF-43 — cumprimento de prazo por equipe e etapa

- Fechado em 2026-09-01 ("Cumprimento de prazo por equipe").

#### §33 🔴 RF-43 — "% aprovado **na 1ª**"

- **Situação**: usa o resultado **atual** (`Status == Aprovado`) — simplificação consciente registrada
  no Dashboard: não há histórico do resultado original antes de uma correção, e `CorrigidoEm == null` não
  é confiável para bonificação.
- **Onde entraria**: persistir o resultado original / rodada (§22).

#### §34 🔴 RF-46 — pesos do score configuráveis

- Pesos 40/30/20/10 e limiares de faixa 85/70 estão no código (`ObterDashboard`).
- **Onde entraria**: `Configuracao` (a seção 8 lista "pesos do score" em `config`).

#### §35 🔴 RF-46a–c — ritmo no lugar de tempo médio; tempo por tipo do conferente

- **O que falta**: ritmo = tempo real ÷ tempo esperado (soma dos tempos de referência), tabela "seu tempo
  por tipo", precedência informado → mediana (≥30 conferências, sem outliers >4×) → estimativa 15 min ×
  peso. Depende do §26.
- **Como sabemos**: requisito; grep sem `Ritmo`.

#### §36 🔴 RF-42a–c — "Hoje, agora", tendência e meta, série do período

- **O que falta** (não investigado em detalhe): faixa "Hoje, agora"/"Seu dia" com gargalo por equipe,
  variação contra o período anterior, metas configuráveis (95%/90%), série por dia útil. Nenhum campo
  equivalente no `DashboardResponse` conhecido.
- **Como sabemos**: requisito.

#### §37 🔴 RF-44 (Dashboard) — exportar CSV

- **Como sabemos**: requisito; grep sem `text/csv`. Pode ser resolvido no front a partir do JSON, mas
  a decisão não foi tomada.

#### §38 ⚪ RF-45 — "sem faixa de bônus" lido como nem a própria faixa

- Leitura conservadora registrada: `Faixa` nula nas duas linhas da visão restrita. Ver
  `docs/patterns/indicadores-e-aprendizado.md`.

### Conferentes

#### §39 ✅ RF-25 — listar com nome/e-mail, editar perfil

- Fechado em "GET /conferentes — fecha o gap encontrado planejando o front" e "Conferentes, ajustes
  achados testando a tela de verdade".

### Transversais

#### §40 ⏸ Versionamento de endpoints (`/v1/...` ou header)

- Registrado originalmente em "Decisões adiadas conscientemente": não fazia sentido sem consumidor; o
  gatilho era "quando o front começar ou antes do primeiro deploy". **Os dois aconteceram sem
  versionamento** — ainda há um único consumidor (`dispatch-web`), deployado junto, e mudanças de shape
  (ex.: paginação, ADR-0029) foram coordenadas nos dois repos na mesma rodada. **Reavaliar** quando
  surgir um segundo consumidor (script do cartório, integração); um prefixo de rota simples resolve,
  sem biblioteca.

#### §41 ⚪ Comportamentos além do documento (sem RF)

Registrados para ninguém "corrigir" achando que é desvio: continuidade de conferência (ADR-0022 — a
seção 11 deixava em aberto), pausar conferência (ADR-0033), ajuste manual de duração (ADR-0035), corte
de horário por equipe+etapa (ADR-0037), atribuição manual ampliada sem validação de alçada (ADR-0027 —
responde, na prática, "quem pode sobrepor alçada numa urgência" da seção 11), prioridade manual
(`POST /protocolos/{id}/definir-prioridade` — o relatório não traz prioridade), `"hora de entrada"` no
cadastro manual. A flag "também confere" da seção 3 existe, mas **derivada** do vínculo `Conferente`,
não como coluna (ADR-0028).

#### §42 ⚪ Requisitos só de interface

Fora do back: RF-03, RF-04, RF-05c/d, RF-12, RF-18e (filtros — client-side, sem endpoint), RF-18c,
RF-24e–j, RF-24f (filtros da fila — só precisou abrir `GET /equipes|escreventes|tipos-ato` ao
Conferente), RF-30b–d, RNF-05 a RNF-13. Ver `../dispatch-web/CLAUDE.md`.

#### §43 ⏸ Inferência residual do nível nas Regras em vigor

- **O que falta**: a distribuidora não recebe o nível de ninguém (ADR-0039), mas ainda percebe que uma
  "Regra base da alçada" nega uns conferentes e outros não, e o "quem pode conferir" do detalhe mostra
  quem está barrado — dá pra inferir o grupo, não o cargo.
- **Como sabemos**: código (`ListarRegrasAlcada`, `ObterDetalheProtocolo`), análise da Feature 3.
- **Onde entraria**: um sujeito `SujeitoAlcada.Todos` para regras que valem pra todos, deixando as de
  nível só como exceções visíveis ao admin. **Reavaliar** se o dono achar a inferência um problema.
