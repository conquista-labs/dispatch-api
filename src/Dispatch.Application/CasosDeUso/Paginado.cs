namespace Dispatch.Application;

// Primeira paginação de verdade do sistema — todo o resto do app usa "busca + rolagem contida"
// no front (mitigação client-side, ver dispatch-web/CLAUDE.md). Record genérico simples: só o
// necessário (a página pedida + o total pra montar os controles de página), sem inventar cursor
// nem metadata a mais que nenhum consumidor precisa ainda.
public sealed record Paginado<T>(IReadOnlyList<T> Itens, int Total);
