---
name: add-domain-rule
description: Use when adding or changing a business rule in the motor de distribuição or in the alçada (permission) engine, inside Dispatch.Domain. Ensures the requirements doc is checked, tests are written first including precedence edge cases, and Domain stays framework-free.
---

# Adicionar ou alterar uma regra de domínio

Esta skill cobre mudanças no motor de distribuição e nas regras de alçada — a lógica mais
sujeita a erro de interpretação do sistema (ver seção 4 do documento de requisitos, especialmente
a parte de precedência entre regra por pessoa e regra por nível).

## Passos

1. **Releia o requisito antes de codificar.** O documento vive em
   `../dispatch-prototype/Dispatch - Requisitos.dc.html`. Seções relevantes: 2 (glossário),
   4 (motor de distribuição e precedência de regras), 5 (prazo e semáforo). Se a mudança
   envolve precedência pessoa/nível, releia o exemplo resolvido da seção 4 antes de escrever
   qualquer código — é fácil implementar a interpretação errada (o próprio documento avisa que
   o protótipo atual diverge do comportamento correto).
2. **Teste primeiro.** Escreva o teste em `Dispatch.Domain.Tests` cobrindo o cenário do
   requisito antes de implementar, incluindo casos de borda de precedência:
   - regra por pessoa existe para o alvo → substitui a regra de nível sobre aquele mesmo alvo
   - dentro do mesmo escopo, negação vence permissão
   - ausência de regra aplicável → permitido
3. **Implemente dentro de `Dispatch.Domain` apenas.** Nenhuma classe aqui deve referenciar
   EF Core, ASP.NET ou qualquer pacote de infraestrutura. Se sentir necessidade disso, a
   abstração provavelmente pertence à `Dispatch.Application`, não ao Domain.
4. **Preserve a auditabilidade (RNF-02).** Toda decisão automática do motor precisa carregar
   a regra que a originou, não só o efeito — ao implementar, garanta que o resultado retornado
   expõe isso (ex: um campo com a regra aplicada), não apenas true/false.
5. **Rode `dotnet test`** e confirme que passa antes de considerar a tarefa terminada.
6. Se a mudança alterar um comportamento já documentado (modo de operação, faixas de
   semáforo, etc.), atualize `CLAUDE.md` na raiz do `dispatch-api` de acordo.
