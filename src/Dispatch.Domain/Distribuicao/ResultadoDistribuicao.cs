namespace Dispatch.Domain;

public abstract record ResultadoDistribuicao
{
    private ResultadoDistribuicao() { }

    public sealed record Atribuido(Conferente Conferente, AvaliacaoCandidato Avaliacao) : ResultadoDistribuicao;

    public sealed record EnviadoParaPool(IReadOnlyList<AvaliacaoCandidato> Elegiveis) : ResultadoDistribuicao;

    public sealed record Excecao(string Motivo, IReadOnlyList<AvaliacaoCandidato> Avaliacoes) : ResultadoDistribuicao;
}
