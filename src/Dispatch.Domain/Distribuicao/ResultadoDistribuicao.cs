namespace Dispatch.Domain;

public abstract record ResultadoDistribuicao
{
    private ResultadoDistribuicao() { }

    // Elegiveis carrega a lista inteira (não só o escolhido) pelo mesmo motivo de EnviadoParaPool
    // e Excecao já carregarem: RF-08 precisa saber "quantos tinham alçada" mesmo quando um deles
    // acabou escolhido por urgência.
    public sealed record Atribuido(Conferente Conferente, AvaliacaoCandidato Avaliacao, IReadOnlyList<AvaliacaoCandidato> Elegiveis) : ResultadoDistribuicao;

    public sealed record EnviadoParaPool(IReadOnlyList<AvaliacaoCandidato> Elegiveis) : ResultadoDistribuicao;

    public sealed record Excecao(string Motivo, IReadOnlyList<AvaliacaoCandidato> Avaliacoes) : ResultadoDistribuicao;
}
