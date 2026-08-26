namespace Dispatch.Domain;

public sealed record RegraAlcada(
    Guid Id,
    SujeitoAlcada Sujeito,
    PermissaoRegra Permissao,
    AlvoAlcada Alvo,
    bool Ativa = true);
