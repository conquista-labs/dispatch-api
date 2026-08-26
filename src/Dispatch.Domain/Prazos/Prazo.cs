namespace Dispatch.Domain;

// "Vence no fim do dia D" está modelado aqui como "vence no início do dia seguinte" — o
// mesmo instante, forma mais simples de calcular. A fronteira exata é um dos pontos que o
// próprio documento de requisitos marca como "a confirmar com a operação" (seção 11).
public sealed record Prazo(TipoPrazo Tipo)
{
    public DateTimeOffset CalcularVencimento(DateTimeOffset momentoDeReferencia) => Tipo switch
    {
        TipoPrazo.UmaHora => momentoDeReferencia.AddHours(1),
        TipoPrazo.D0 => FimDoDia(momentoDeReferencia, diasAFrente: 0),
        TipoPrazo.D1 => FimDoDia(momentoDeReferencia, diasAFrente: 1),
        TipoPrazo.D2 => FimDoDia(momentoDeReferencia, diasAFrente: 2),
        _ => throw new ArgumentOutOfRangeException(nameof(Tipo), Tipo, message: null)
    };

    private static DateTimeOffset FimDoDia(DateTimeOffset referencia, int diasAFrente)
    {
        var inicioDoDia = new DateTimeOffset(referencia.Date, referencia.Offset);
        return inicioDoDia.AddDays(diasAFrente + 1);
    }
}
