namespace Dispatch.Domain;

// Um ato candidato ao ritmo: o tempo real atribuído a quem se mede (nulo = sem tempo medido) e a
// referência efetiva do tipo (nulo = ato sem tipo, ou tipo fora do catálogo).
public sealed record AtoParaRitmo(TimeSpan? TempoReal, int? ReferenciaMinutos);

// RF-46a: ritmo = tempo real ÷ tempo esperado, onde o esperado soma, ato a ato, a referência do tipo.
// Razão de SOMAS (não média das razões por ato): é o que o requisito descreve ("o tempo esperado soma,
// ato a ato") e o que a frase do RF-46b explica ("16 min bruto; a referência para o mesmo conjunto
// seria 21 min; ritmo 0,76×"). `TempoMedioReferencia` é o "21 min" dessa frase.
public sealed record RitmoCalculado(double Valor, int Atos, TimeSpan TempoReal, TimeSpan TempoReferencia)
{
    public TimeSpan TempoMedioReferencia => TempoReferencia / Atos;
}

public static class Ritmo
{
    // Nulo sem nenhum ato elegível (com tempo e com referência) — "0×" diria "infinitamente rápido".
    public static RitmoCalculado? Calcular(IEnumerable<AtoParaRitmo> atos)
    {
        var elegiveis = atos
            .Where(a => a.TempoReal is not null && a.ReferenciaMinutos is > 0)
            .ToList();
        if (elegiveis.Count == 0)
        {
            return null;
        }

        var real = elegiveis.Aggregate(TimeSpan.Zero, (soma, a) => soma + a.TempoReal!.Value);
        var referencia = TimeSpan.FromMinutes(elegiveis.Sum(a => a.ReferenciaMinutos!.Value));
        return new RitmoCalculado(real / referencia, elegiveis.Count, real, referencia);
    }

    // "Média da casa" (RF-45/RF-46a): média simples entre as pessoas que têm ritmo.
    public static double? MediaSimples(IEnumerable<double?> ritmos)
    {
        var comValor = ritmos.Where(r => r is not null).Select(r => r!.Value).ToList();
        return comValor.Count == 0 ? null : comValor.Average();
    }
}
