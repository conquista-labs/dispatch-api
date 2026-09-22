namespace Dispatch.Domain;

public sealed class Equipe
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public Prazo PrazoPreConferencia { get; private set; }
    public Prazo PrazoPosConferencia { get; private set; }
    // Pedido do dono ("equipe X entra na etapa Y depois das 16h, vence às 10h do dia seguinte")
    // — acréscimo opcional ao TipoPrazo normal de cada etapa, não substituição: null nas duas
    // propriedades de uma etapa = sem corte, comportamento de sempre. Genérico por Equipe+Etapa,
    // não hardcoded pra uma equipe específica.
    public TimeOnly? CortePreConferenciaHorarioCorte { get; private set; }
    public TimeOnly? CortePreConferenciaHorarioVencimento { get; private set; }
    public TimeOnly? CortePosConferenciaHorarioCorte { get; private set; }
    public TimeOnly? CortePosConferenciaHorarioVencimento { get; private set; }

    public Equipe(
        Guid id, string nome, Prazo prazoPreConferencia, Prazo prazoPosConferencia,
        TimeOnly? cortePreConferenciaHorarioCorte = null, TimeOnly? cortePreConferenciaHorarioVencimento = null,
        TimeOnly? cortePosConferenciaHorarioCorte = null, TimeOnly? cortePosConferenciaHorarioVencimento = null)
    {
        Id = id;
        Nome = nome;
        PrazoPreConferencia = prazoPreConferencia;
        PrazoPosConferencia = prazoPosConferencia;
        CortePreConferenciaHorarioCorte = cortePreConferenciaHorarioCorte;
        CortePreConferenciaHorarioVencimento = cortePreConferenciaHorarioVencimento;
        CortePosConferenciaHorarioCorte = cortePosConferenciaHorarioCorte;
        CortePosConferenciaHorarioVencimento = cortePosConferenciaHorarioVencimento;
    }

    // `referencia` decide se a entrada foi depois do corte configurado (comparado em horário de
    // Brasília, ver FusoHorario) — se sim, devolve um Prazo(CorteDeHorario) com o vencimento do
    // dia seguinte; senão, cai no TipoPrazo normal da etapa (comportamento de sempre, sem corte).
    public Prazo PrazoPara(Etapa etapa, DateTimeOffset referencia)
    {
        var (prazoBase, horarioCorte, horarioVencimento) = etapa switch
        {
            Etapa.PreConferencia => (PrazoPreConferencia, CortePreConferenciaHorarioCorte, CortePreConferenciaHorarioVencimento),
            Etapa.PosConferencia => (PrazoPosConferencia, CortePosConferenciaHorarioCorte, CortePosConferenciaHorarioVencimento),
            _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, message: null)
        };

        if (horarioCorte is { } corte && horarioVencimento is { } vencimento &&
            FusoHorario.ParaHorarioLocal(referencia).TimeOfDay >= corte.ToTimeSpan())
        {
            return new Prazo(TipoPrazo.CorteDeHorario, vencimento);
        }

        return prazoBase;
    }

    // RF-35.
    public void Renomear(string novoNome) => Nome = novoNome;

    // RF-36 + corte de horário. RF-38 (recalcular vencimentos abertos) é responsabilidade de
    // quem chama isto (RecalculoDeVencimentos), não deste método.
    public void DefinirPrazos(
        Prazo prazoPreConferencia, Prazo prazoPosConferencia,
        TimeOnly? cortePreConferenciaHorarioCorte, TimeOnly? cortePreConferenciaHorarioVencimento,
        TimeOnly? cortePosConferenciaHorarioCorte, TimeOnly? cortePosConferenciaHorarioVencimento)
    {
        PrazoPreConferencia = prazoPreConferencia;
        PrazoPosConferencia = prazoPosConferencia;
        CortePreConferenciaHorarioCorte = cortePreConferenciaHorarioCorte;
        CortePreConferenciaHorarioVencimento = cortePreConferenciaHorarioVencimento;
        CortePosConferenciaHorarioCorte = cortePosConferenciaHorarioCorte;
        CortePosConferenciaHorarioVencimento = cortePosConferenciaHorarioVencimento;
    }
}
