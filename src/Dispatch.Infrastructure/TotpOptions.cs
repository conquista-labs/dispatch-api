namespace Dispatch.Infrastructure;

public sealed class TotpOptions
{
    public const string Secao = "Totp";

    // RNF-15: chave de cifragem do segredo TOTP em repouso — fora do banco, mesmo padrão de
    // JwtOptions.ChaveDeAssinatura (appsettings local em dev, secret de verdade em produção).
    // Base64 de 32 bytes (AES-256).
    public required string ChaveDeCifragem { get; init; }
}
