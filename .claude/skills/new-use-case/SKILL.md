---
name: new-use-case
description: Use when adding a new use case to Dispatch.Application (an application-layer operation like "importar lote" or "distribuir protocolo"). Scaffolds it consistently and keeps the interface-in-Application / implementation-in-Infrastructure boundary intact.
---

# Adicionar um caso de uso na Application

## Passos

1. **Nomeie pelo verbo de negócio** do documento de requisitos (`ImportarLote`,
   `DistribuirProtocolo`, `RedistribuirPool`, `AprovarAto`...) — não use nomes genéricos como
   "Service" ou "Manager".
2. **Portas em Application, adapters em Infrastructure.** Se o caso de uso precisa de algo
   externo (banco, relógio, arquivo), defina a interface em `Dispatch.Application`
   (ex: `IProtocoloRepository`, `IClock`) — a implementação concreta mora em
   `Dispatch.Infrastructure`, nunca o inverso.
3. **Tipos de entrada/saída próprios.** Não deixe entidades do EF Core (ou qualquer tipo de
   `Dispatch.Infrastructure`) vazarem como retorno de um caso de uso.
4. **Regra de negócio de verdade vive no Domain.** O caso de uso orquestra chamadas ao
   `Dispatch.Domain` e às portas — ele não reimplementa o motor de distribuição, o cálculo de
   prazo ou a precedência de alçada.
5. **Registre no composition root.** A interface e a implementação real são ligadas via DI em
   `Dispatch.Api/Program.cs`.
6. **Teste isolado.** Teste o caso de uso com fakes/in-memory das interfaces das quais ele
   depende — sem banco real. Validar contra o banco/HTTP real é papel da skill
   `verify-integration`, não desta.
