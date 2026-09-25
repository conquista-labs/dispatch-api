namespace Dispatch.Domain;

// RF-42c — "gráfico de barras de conferidos por dia útil (por semana no trimestre), separando no prazo
// e estourados". O período aparece INTEIRO, pra o gráfico não "crescer" ao longo do mês: os dias (ou
// semanas) que ainda não chegaram vêm com `Futuro` e zeros. Dia útil = segunda a sexta no dia de
// Brasília, sem feriado (mesma simplificação de Prazo.ProximoDiaUtil); sábado/domingo só entram se
// alguém conferiu neles — não some trabalho feito, mas também não abre buraco vazio no gráfico.
// Semana (trimestre) começa na segunda: a 1ª e a última podem começar/terminar fora do trimestre,
// mas só contam o que foi concluído dentro do período (13 ou 14 pontos).
public sealed record SerieDoPeriodo(GranularidadeSerie Granularidade, IReadOnlyList<PontoDaSerie> Pontos)
{
    public static SerieDoPeriodo Montar(PeriodoDashboard periodo, DateTimeOffset agora, IEnumerable<ConclusaoNaSerie> conclusoes)
    {
        var hoje = FusoHorario.DiaLocal(agora);
        var primeiroDia = CalendarioDoPeriodo.PrimeiroDia(periodo, hoje);
        var ultimoDia = CalendarioDoPeriodo.UltimoDia(periodo, hoje);
        var porSemana = periodo == PeriodoDashboard.Trimestre;

        var contagens = conclusoes
            .Select(c => (Dia: FusoHorario.DiaLocal(c.ConcluidoEm), c.Estourado))
            .Where(c => c.Dia >= primeiroDia && c.Dia <= ultimoDia)
            .GroupBy(c => porSemana ? CalendarioDoPeriodo.SegundaDaSemana(c.Dia) : c.Dia)
            .ToDictionary(g => g.Key, g => (Conferidos: g.Count(), Estourados: g.Count(c => c.Estourado)));

        PontoDaSerie Ponto(DateOnly inicio)
        {
            var (conferidos, estourados) = contagens.GetValueOrDefault(inicio);
            return new PontoDaSerie(inicio, conferidos, estourados, Futuro: inicio > hoje);
        }

        if (porSemana)
        {
            var semanas = new List<PontoDaSerie>();
            for (var segunda = CalendarioDoPeriodo.SegundaDaSemana(primeiroDia); segunda <= ultimoDia; segunda = segunda.AddDays(7))
            {
                semanas.Add(Ponto(segunda));
            }

            return new SerieDoPeriodo(GranularidadeSerie.Semana, semanas);
        }

        var dias = new List<PontoDaSerie>();
        for (var dia = primeiroDia; dia <= ultimoDia; dia = dia.AddDays(1))
        {
            var fimDeSemana = dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!fimDeSemana || contagens.ContainsKey(dia))
            {
                dias.Add(Ponto(dia));
            }
        }

        return new SerieDoPeriodo(GranularidadeSerie.Dia, dias);
    }
}

public enum GranularidadeSerie
{
    Dia,
    Semana
}

// Um protocolo concluído, reduzido ao que a série precisa. `Estourado` = concluído depois do
// vencimento — quem monta passa a mesma definição do "no prazo" dos KPIs (ObterDashboard.EstaNoPrazo).
public sealed record ConclusaoNaSerie(DateTimeOffset ConcluidoEm, bool Estourado);

// `Inicio` = o dia (Dia) ou a segunda-feira (Semana), no calendário de Brasília. `Conferidos` inclui
// os estourados (no prazo = Conferidos − Estourados).
public sealed record PontoDaSerie(DateOnly Inicio, int Conferidos, int Estourados, bool Futuro);
