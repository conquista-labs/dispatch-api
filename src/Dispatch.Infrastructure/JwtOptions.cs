namespace Dispatch.Infrastructure;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    public required string ChaveDeAssinatura { get; init; }
    public required string Emissor { get; init; }
    public required string Audiencia { get; init; }
    public int ExpiracaoMinutos { get; init; } = 60;
}
