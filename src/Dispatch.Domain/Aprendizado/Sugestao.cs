namespace Dispatch.Domain;

// RF-39 a RF-41. Chave é o mecanismo de dedup (seção 7: "chave única por proposta — repetição
// incrementa um contador em vez de duplicar") — cada rodada do gerador recalcula do zero;
// achar uma Sugestao Pendente com a mesma chave atualiza ocorrências/evidência em vez de criar
// outra linha. DescartarAte é o segundo mecanismo ("descarte com memória — não reaparece por
// N dias"): enquanto no futuro, o gerador pula essa chave.
public sealed class Sugestao
{
    public Guid Id { get; }
    public string Chave { get; }
    public PayloadSugestao Payload { get; }
    public string Evidencia { get; private set; }
    public int Ocorrencias { get; private set; }
    public double IndiceConfianca { get; private set; }
    public StatusSugestao Status { get; private set; }
    public DateTimeOffset CriadaEm { get; }
    public DateTimeOffset AtualizadaEm { get; private set; }
    public DateTimeOffset? DecididaEm { get; private set; }
    public DateTimeOffset? DescartarAte { get; private set; }

    // Os últimos quatro parâmetros só existem pra reidratar uma sugestão já persistida (mesmo
    // padrão de RegraAlcada com `origem`/`ativa`) — criar uma sugestão nova nunca os informa,
    // ela sempre nasce Pendente.
    public Sugestao(
        Guid id, string chave, PayloadSugestao payload, string evidencia, int ocorrencias, double indiceConfianca, DateTimeOffset criadaEm,
        DateTimeOffset? atualizadaEm = null, StatusSugestao status = StatusSugestao.Pendente,
        DateTimeOffset? decididaEm = null, DateTimeOffset? descartarAte = null)
    {
        Id = id;
        Chave = chave;
        Payload = payload;
        Evidencia = evidencia;
        Ocorrencias = ocorrencias;
        IndiceConfianca = indiceConfianca;
        Status = status;
        CriadaEm = criadaEm;
        AtualizadaEm = atualizadaEm ?? criadaEm;
        DecididaEm = decididaEm;
        DescartarAte = descartarAte;
    }

    // Rodada nova do gerador achou a mesma chave de novo — atualiza em vez de duplicar (o
    // índice de confiança é recalculado junto, mesma cadência de ocorrências/evidência).
    public void AtualizarEvidencia(int ocorrencias, string evidencia, double indiceConfianca, DateTimeOffset agora)
    {
        Ocorrencias = ocorrencias;
        Evidencia = evidencia;
        IndiceConfianca = indiceConfianca;
        AtualizadaEm = agora;
    }

    // RF-40: "aplicar" executa a mudança de verdade — a mudança em si (classificar tipo, mudar
    // prazo, alocar escrevente, criar regra) é feita pelo caso de uso antes de chamar isto;
    // aqui só fecha o ciclo de vida da sugestão.
    public void Aplicar(DateTimeOffset agora)
    {
        Status = StatusSugestao.Aplicada;
        DecididaEm = agora;
    }

    public void Descartar(DateTimeOffset agora, DateTimeOffset descartarAte)
    {
        Status = StatusSugestao.Descartada;
        DecididaEm = agora;
        DescartarAte = descartarAte;
    }
}
