namespace Dispatch.Domain;

// Um ajuste manual da Duracao final de um protocolo, já aplicado — pedido do dono: a
// distribuidora, como admin, pode corrigir um tempo de conferência que saiu errado (ex.: um
// esquecimento de pausar/retomar inflou o número). Igual a CicloConferencia/PausaConferencia,
// registra o "porquê" (RNF-02: toda decisão registra quem e quando), não só sobrescreve o
// valor — guarda o valor anterior e o novo, pra ficar auditável se editado mais de uma vez.
public sealed class AjusteDeDuracao
{
    public Guid AjustadoPorId { get; }
    public DateTimeOffset AjustadoEm { get; }
    public TimeSpan? DuracaoAnterior { get; }
    public TimeSpan DuracaoNova { get; }
    public string? Motivo { get; }

    public AjusteDeDuracao(Guid ajustadoPorId, DateTimeOffset ajustadoEm, TimeSpan? duracaoAnterior, TimeSpan duracaoNova, string? motivo)
    {
        AjustadoPorId = ajustadoPorId;
        AjustadoEm = ajustadoEm;
        DuracaoAnterior = duracaoAnterior;
        DuracaoNova = duracaoNova;
        Motivo = motivo;
    }
}
