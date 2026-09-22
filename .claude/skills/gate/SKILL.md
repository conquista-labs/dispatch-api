---
name: gate
description: Roda a cadeia de verificação completa do dispatch-api uma vez, no fim de uma tarefa — build, suíte de testes (unidade + integração), relatório de cobertura — e persegue o que ela apontar. Use quando terminar uma mudança, antes de commitar, ou quando o usuário pedir "roda tudo", "está tudo verde?", "confere antes de subir".
---

# gate

Roda a cadeia inteira **uma vez, quando a tarefa está pronta** — não entre uma edição e outra.
Rodar a suíte repetidamente durante o desenvolvimento pra *descobrir* o que quebrou é mais lento
que ler o arquivo: um nome de método errado ou uma porta faltando no DI aparece na fonte.

## Tiers

Nesta ordem — cada um é mais lento que o anterior e não faz sentido rodar se o anterior está
vermelho.

1. **`dotnet build`** — pega erro de compilação e, com `TreatWarnings` do projeto, avisos novos.
2. **`dotnet test`** — as 3 suítes: `Dispatch.Domain.Tests` e `Dispatch.Application.Tests`
   (fakes, rápidas) e `Dispatch.Api.Tests` (integração: sobe a API em memória contra um Postgres
   efêmero via Testcontainers — **exige Docker rodando**).
3. **`dotnet run --project src/Dispatch.Api`** — obrigatório depois de adicionar caso de uso,
   endpoint ou dependência nova de construtor. Minimal API só falha ao montar o endpoint em
   runtime quando falta registro no DI (`Failure to infer one or more parameters`) — `build` e
   `test` passam limpos mesmo assim. Ver skill `verify-integration` pro smoke test manual.

Pra mudança só de documentação, o tier 1 basta.

## Cobertura

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport \
  -reporttypes:"Html;TextSummary" -classfilters:"-Dispatch.Infrastructure.Migrations.*"
```

**O `-classfilters` não é opcional.** Sem ele as migrations do EF Core (código gerado, dezenas de
milhares de linhas, todas executadas pelo `Database.Migrate()` do fixture de integração) entram
no denominador e mentem: `Dispatch.Infrastructure` aparece com ~97% quando o número real dos
repositórios é ~65%, e o total do projeto pula de ~71% pra ~92%. Cobertura inflada por código
gerado é pior que nenhuma cobertura — dá falsa confiança exatamente onde falta teste.

Leia `coveragereport/Summary.txt` (o `TextSummary`), não o HTML inteiro — o HTML é pra abrir no
navegador quando você quer navegar arquivo a arquivo, despejar ele no transcript não ajuda.

Não existe threshold/gate de cobertura no back: `coverlet.collector` (data collector) não sabe
enforçar limite, e não há CI pra avaliar isso de qualquer forma. O número é pra olhar e decidir,
não pra travar build.

## Lendo a saída sem inundar o transcript

```bash
dotnet test 2>&1 | grep -E "Aprovado!|Com falha|error"
dotnet build 2>&1 | grep -iE "aviso|erro|êxito"
```

Quando um teste de integração falha com status HTTP inesperado, a mensagem do `Assert` já traz o
corpo da resposta (`IntegracaoTestBase` monta assim de propósito) — em `Development` o
`DeveloperExceptionPage` devolve a stack trace real da API no corpo, que é onde está a causa.

## Armadilhas que este repositório já pagou

- **Docker precisa estar de pé.** `Dispatch.Api.Tests` sobe um container Postgres por execução;
  sem Docker, a suíte inteira falha no fixture, não em um teste específico.
- **`sleep` longo é bloqueado no harness.** Pra esperar a API subir, use um laço `until` com
  `curl` no `/health`, não `sleep` encadeado.
- **Nunca deixe credencial vazar no log.** Ao filtrar saída de teste/`curl`, passe por
  `grep -viE "password|senha|token"` por hábito.
- **`dotnet test` verde não prova que a API sobe.** Nenhum teste de unidade instancia o
  composition root inteiro — daí o tier 3.

## Reporte

Diga os números crus: quantos testes por suíte, as 4 métricas de cobertura (linha/branch/método),
se a API subiu. Se algo está vermelho, cite a asserção que falhou — não resuma.
