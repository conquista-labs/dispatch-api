---
name: adr-0039-perfil-administrador
description: Papel Administrador acima da Distribuidora, modelado como papel efetivo que carrega também a claim Distribuidora; o que é só do admin ganha RequireRole(Administrador) na rota, e cargo/avaliação/regras de nível são cortados na Application por uma flag que o endpoint passa
metadata:
  type: decision
  status: accepted
---

# ADR-0039: Perfil Administrador

> Existe um terceiro papel, `Administrador`, que faz tudo o que a distribuidora faz e, só ele, vê
> cargo e avaliação, cadastra pessoas, edita a Central de regras e gerencia contas. O admin carrega
> também a claim `Distribuidora`; o que é exclusivo dele ganha `RequireRole(Administrador)` na rota.

## Status

`Accepted — 2026-09-25`

## Contexto

Requisitos v2, §3: três papéis. "Para a distribuidora a API não devolve nível, score nem faixa, e
rejeita escrita em regras, conferentes e contas." RF-29a (Conferentes vira só presença), RF-30a
(Central só leitura, Regras em vigor), RF-43a (Produção por conferente, sem nível/score/faixa, em
ordem alfabética), 6.8 Contas (RF-44 a 48). Até aqui a distribuidora **era** o administrador. A
restrição tem de ser no servidor (RNF-04): esconder na tela não basta.

## Decisão

Vamos modelar `Administrador` como mais um valor de `Papel` e somar claims, porque não muda nenhuma das
~40 rotas de gestão existentes:

- `PapeisEfetivos`: `Administrador → [Administrador, Distribuidora]` (+ `Conferente` se vinculado). A
  claim `Distribuidora` passa a significar "tem acesso de gestão". A ordem importa (o front usa
  `papeis[0]`).
- Rota só do admin: `RequireRole(Administrador)` **somado** ao `RequireRole(Distribuidora)` do grupo —
  políticas de grupo e de rota combinam com **E**, então a distribuidora leva 403 e o admin passa.
  Grupos inteiramente do admin (`/tipos-ato` de gestão, `/sugestoes`, `/contas`) trocam o papel do grupo.
- **O corte de dado mora na Application**, por uma flag que o endpoint passa
  (`ClaimsPrincipal.EhAdministrador()`), assim é testável com fakes: `ListarConferentes(incluirNivel)`,
  `ObterDashboard(incluirAvaliacaoDePessoal = false)` (default fechado) e `ListarRegrasAlcada(incluirNivel)`.
- **Invariante**: o nível só sai do servidor num token de Administrador. DTO único com campos nullable
  (mesmo padrão de `MediaDaCasa`).
- Dashboard sem a flag: lista **ordenada por nome** — a ordem por score, sozinha, entregaria o ranking.
- Regras em vigor: regra por nível sai com `SujeitoNivel` null e `RegraBase` (o front mostra "Regra
  base da alçada"). A trilha do detalhe **não** precisou ser escondida: o painel só usa o
  `regraAplicadaId`, que o front resolve contra essa mesma lista mascarada; a trilha por camada só
  aparece no simulador Testar, que é do admin.
- `OnTokenValidated` passa a recusar conta desativada (antes, até 8h de sobrevida do token).
- **Primeiro admin em produção** — SQL avulso, não migration (identidade inicial já tinha precedente, e
  uma migration poria um e-mail real no repositório). Rodar com a skill `prod-ops`:

  ```sql
  BEGIN;
  UPDATE usuarios SET papel = 'Administrador', sessoes_validas_apartir_de = now()
  WHERE email = '<email>' AND papel = 'Distribuidora';
  SELECT id, nome, email, papel FROM usuarios WHERE email = '<email>';  -- conferir 1 linha
  COMMIT;
  ```

  O bump de `sessoes_validas_apartir_de` força novo login (claims novas). Em produção, a primeira admin
  é a Maria Vittoria (decisão do dono, 25/09/2026); a conta dela também confere, e fica com os três
  papéis efetivos.

## Alternativas consideradas

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Named policies (`"Gestao"`, `"Admin"`) | Nome explícito por intenção | Mesma checagem de claim por baixo, sem ganho de segurança; reescreveria as ~40 rotas | Só vale quando surgir requisito customizado de verdade |
| Tabela de permissões por usuário | Granularidade fina | Modelagem e tela novas pra 3 papéis fixos do requisito | Desproporcional |
| Esconder só no front | Rápido | Contraria RNF-04 e o §3 ("a API não devolve") | Inaceitável |
| Corte no endpoint em vez da Application | Menos parâmetros | Não testável com fakes; cada endpoint repetiria a regra | A Application já é onde a regra de leitura mora |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Segurança / privacidade | ✅ Melhora | Cargo e avaliação não saem pra quem não é admin; conta desativada perde o token |
| Retrocompatibilidade | ✅ | Nenhuma rota de gestão existente mudou de política |
| Testabilidade | ✅ | Corte por flag na Application, coberto com fakes e integração |

## Consequências

**Positivas** — a regra de "quem vê o quê" fica explícita e testada; promover alguém é um UPDATE.

**Negativas** — a distribuidora perde, no deploy, a edição de regras e o cadastro de pessoas: até o
primeiro admin ser promovido, **ninguém edita regra**. A ordem de deploy (`deploy.md`) importa.

**Riscos / inferência residual (aceita)** — a distribuidora ainda percebe que uma "Regra base" nega
uns conferentes e outros não, e o "quem pode conferir" do detalhe mostra quem está barrado. A
solução limpa (um sujeito `SujeitoAlcada.Todos`) fica para depois.

## Referências

- `docs/patterns/autorizacao.md` (tabela do que foi cortado); `docs/gaps-requisitos.md` §1.
- ADR-0028 (conta com dois papéis), ADR-0040 (troca de senha no primeiro acesso).
- Testes: `AutenticarTests`, `ListarConferentesTests`, `ObterDashboardTests`, `ListarRegrasAlcadaTests`,
  `AdministradorIntegracaoTests`.
