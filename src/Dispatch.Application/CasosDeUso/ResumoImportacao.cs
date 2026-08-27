namespace Dispatch.Application;

// RF-10: prévia agregada. RF-09: contagem de tipo desconhecido / escrevente sem equipe.
public sealed record ResumoImportacao(
    int TotalNoArquivo,
    int IgnoradasPelaLinhaDeCorte,
    int Processadas,
    IReadOnlyList<AtribuicaoPorConferente> AtribuidosPorConferente,
    int EnviadosParaPool,
    int Excecoes,
    IReadOnlyList<string> TiposDesconhecidos,
    IReadOnlyList<string> EscreventesSemEquipe);

public sealed record AtribuicaoPorConferente(Guid ConferenteId, int Quantidade);
