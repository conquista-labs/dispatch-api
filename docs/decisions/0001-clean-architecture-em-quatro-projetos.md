---
name: adr-0001-clean-architecture-em-quatro-projetos
description: Back-end organizado em Clean Architecture com quatro projetos (Domain, Application, Infrastructure, Api) e minimal APIs na borda HTTP
metadata:
  type: decision
  status: accepted
---

# ADR-0001: Clean Architecture em quatro projetos, com minimal APIs

> O back-end é uma solution com quatro projetos sob `src/` — `Dispatch.Domain`,
> `Dispatch.Application`, `Dispatch.Infrastructure`, `Dispatch.Api` — onde cada camada só referencia
> as que estão mais para dentro, e o Domain não tem nenhum pacote de framework. A borda HTTP usa
> minimal APIs, não controllers.

## Status

Accepted — 2026-08-26 (scaffold inicial, commit `2e2f6ed`). Reconstituído em 2026-09-25 a partir
das seções "Arquitetura" e "Premissas de qualidade" do CLAUDE.md original.

## Contexto

O coração do sistema é um motor de distribuição e um resolvedor de precedência de alçada (seção 4
do documento de requisitos) — "a parte do sistema mais fácil de acertar errado e mais cara de errar
em produção". Essa lógica precisa ser testável em memória, sem banco, e sobreviver a mudanças de
persistência e de front. Além disso, o projeto é também veículo de aprendizado de .NET para o dono,
então a estrutura precisa deixar explícito onde cada tipo de código mora.

## Decisão

Vamos separar o back-end em quatro projetos com regra de dependência de fora pra dentro:

```
Dispatch.Domain          entidades, value objects, motor — C# puro, zero dependência externa
Dispatch.Application     casos de uso e portas (interfaces) — depende só de Domain
Dispatch.Infrastructure  EF Core, repositórios, adapters — depende de Application + Domain
Dispatch.Api             Program.cs, endpoints, composition root — depende de Application + Infrastructure
```

- `Domain` nunca ganha pacote NuGet de framework (EF Core, ASP.NET...). Se uma classe de `Domain`
  ou `Application` "precisa" de EF Core, a abstração está no lugar errado.
- Portas (`IProtocoloRepository`, `IRelogio`, `IUnitOfWork`...) ficam em `Application/Portas/`;
  a implementação real em `Infrastructure`.
- Um projeto de teste por camada sob `tests/`, criado quando a camada ganha lógica que justifique
  (hoje: `Dispatch.Domain.Tests`, `Dispatch.Application.Tests`, `Dispatch.Api.Tests` — ver
  ADR-0036).
- Borda HTTP com **minimal APIs** (`Endpoints/*.cs`, `MapGroup`/`MapGet`...), não controllers.

## Alternativas consideradas

As alternativas abaixo não foram debatidas por escrito na época — o scaffold já nasceu com esta
forma, e a stack registrou "minimal APIs, não controllers" sem justificar. Ficam aqui como o que a
escolha exclui, para quem for revisitar.

| Alternativa | Prós | Contras | Por que foi descartada |
| ----------- | ---- | ------- | ---------------------- |
| Controllers MVC (`[ApiController]`) | Convenção mais antiga e mais documentada; filtros/atributos prontos | Mais cerimônia por endpoint; classe por recurso | Registrado só como "minimal APIs, não controllers" — preferência de enxugar a borda |
| Projeto único (endpoints chamando EF Core direto) | Menos arquivos, menos indireção | Motor e precedência de alçada acoplados ao ORM; teste de domínio exigiria banco | Contraria a premissa de testar a lógica de domínio isolada, antes ou junto da implementação |

## Characteristics impactadas (-ilities)

| Characteristic | Impacto | Justificativa |
| -------------- | ------- | ------------- |
| Testabilidade | ✅ Melhora | Domain e Application testados com fakes em memória (centenas de testes rápidos) |
| Manutenibilidade | ✅ Melhora | Motor de alçada reescrito 3 vezes (ADR-0017, 0018, 0024) sem tocar Api nem Infrastructure além do necessário |
| Simplicidade | ⚠️ Piora | Todo caso de uso novo precisa de porta, fake, registro no DI e endpoint |

## Consequências

**Positivas** — regras de negócio vivem no Domain e são testadas sem banco; a Api nunca vê entidade
do EF.

**Negativas** — fakes em memória não reproduzem FK, change tracker nem restrições do Npgsql; vários
bugs reais só apareceram contra o Postgres (ver `docs/patterns/testes.md`). Esquecer de registrar
um caso de uso no DI só falha em runtime (ver `docs/patterns/arquitetura.md`).

**Riscos** — vazamento de framework para o Domain; percebido em revisão (a skill
`add-domain-rule` reforça a regra).

## Referências

- `docs/patterns/arquitetura.md` — regras de camada, portas, composition root, convenções de caso de uso.
- `docs/patterns/conceitos-dotnet.md` — conceitos de .NET introduzidos ao longo do projeto.
- `docs/historico.md`, "Estado atual (scaffold, motor, prazos, primeira persistência)".
