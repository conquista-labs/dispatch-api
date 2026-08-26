---
name: ef-migration
description: Use when creating or applying an Entity Framework Core migration in Dispatch.Infrastructure. Project rule is that the Neon Postgres schema only ever changes through versioned EF Core Migrations, never by hand.
---

# Criar ou aplicar uma migration do EF Core

O schema do Postgres no Neon só muda por migration versionada — nunca editando o banco na mão
(regra de qualidade do `CLAUDE.md`). Todos os comandos abaixo rodam a partir da raiz do
`dispatch-api`.

## Pré-requisito (uma vez só)

Confirme que a ferramenta `dotnet-ef` está disponível:

```
dotnet tool list
```

Se não aparecer, instale localmente ao projeto (evita depender de instalação global na máquina):

```
dotnet new tool-manifest   # só se ainda não existir um .config/dotnet-tools.json
dotnet tool install dotnet-ef
```

## Passos

1. Altere a entidade e/ou o `DbContext` em `Dispatch.Infrastructure`.
2. Gere a migration, apontando `--project` pra onde os arquivos de migration devem ser
   escritos e `--startup-project` pro executável que tem a configuração de conexão:

   ```
   dotnet ef migrations add NomeDaMigration --project src/Dispatch.Infrastructure --startup-project src/Dispatch.Api
   ```

3. **Revise o arquivo gerado** em `src/Dispatch.Infrastructure/Migrations/` antes de aplicar —
   confira `Up()` e `Down()`, principalmente colunas `NOT NULL` novas em tabela que já teria
   dados (precisam de valor default ou de um passo de backfill).
4. Aplique no banco:

   ```
   dotnet ef database update --project src/Dispatch.Infrastructure --startup-project src/Dispatch.Api
   ```

5. **Nunca** conecte um client SQL direto no Neon pra alterar schema manualmente — isso
   quebra o histórico de migrations e diverge do código.
6. Recálculo de dados de negócio (ex: RF-38 — "alterar prazo de equipe recalcula vencimentos
   abertos") é lógica de aplicação, não faz parte da migration de schema. Mantenha a migration
   restrita à forma das tabelas, não ao dado.
