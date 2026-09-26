using Dispatch.Domain;

namespace Dispatch.Application;

// RF-10: prévia agregada. RF-09: contagem de tipo desconhecido / escrevente sem equipe.
public sealed record ResumoImportacao(
    // Nulo na prévia (RF-11: nada persiste, então não existe lote de verdade ainda).
    Guid? LoteImportacaoId,
    int TotalNoArquivo,
    int IgnoradasPelaLinhaDeCorte,
    int Processadas,
    IReadOnlyList<AtribuicaoPorConferente> AtribuidosPorConferente,
    int EnviadosParaPool,
    int Excecoes,
    IReadOnlyList<string> TiposDesconhecidos,
    // Quantas linhas processadas (depois da linha de corte) caíram em cada tipo novo deste lote,
    // na mesma ordem de TiposDesconhecidos — que continua existindo como está (o front em
    // produção lê a lista de nomes; a contagem é aditiva).
    IReadOnlyList<TipoDesconhecidoContagem> TiposDesconhecidosContagem,
    IReadOnlyList<string> EscreventesSemEquipe,
    // RF-08: só na prévia (nulo na confirmação — o front não usa e o lote pode ter centenas de
    // linhas, não vale carregar isso na resposta de gravar).
    IReadOnlyList<LinhaPreviaImportacao>? Linhas);

public sealed record AtribuicaoPorConferente(Guid ConferenteId, int Quantidade);

// Nome já normalizado — o mesmo texto que vai em ResumoImportacao.TiposDesconhecidos.
public sealed record TipoDesconhecidoContagem(string Nome, int Quantidade);

// RF-08: pra cada linha, a regra que gerou o prazo (equipe + etapa) e quantos conferentes
// tinham alçada pra ela. Equipe vai por nome aqui (e não só um Id, como ProtocoloResumo faz)
// porque na prévia o escrevente pode nem existir no banco ainda (RF-09: só nasce na
// confirmação) — não dá pra o front cruzar com GET /escreventes como faz nas telas normais.
public sealed record LinhaPreviaImportacao(
    string Protocolo,
    string TipoAto,
    bool TipoConhecido,
    string Escrevente,
    string? Equipe,
    TipoPrazo? Prazo,
    DateTimeOffset? VencimentoEm,
    FaixaSemaforo? Semaforo,
    // true quando a linha caiu antes da linha de corte — nesse caso nada abaixo foi resolvido
    // de verdade (a linha nunca chega a ser distribuída), os campos acima ficam nulos.
    bool JaExiste,
    int ComAlcada);
