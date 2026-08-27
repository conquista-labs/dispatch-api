namespace Dispatch.Domain;

// Seção 8 do requisito. Registro de uma confirmação de importação — permite às três visões
// do RF-13 filtrarem "só este lote" em vez de todos os protocolos que já existiram.
public sealed class LoteImportacao
{
    public Guid Id { get; }
    public Etapa Etapa { get; }
    public DateTimeOffset LinhaDeCorte { get; }
    public DateTimeOffset ImportadoEm { get; }
    public int TotalLinhas { get; }

    public LoteImportacao(Guid id, Etapa etapa, DateTimeOffset linhaDeCorte, DateTimeOffset importadoEm, int totalLinhas)
    {
        Id = id;
        Etapa = etapa;
        LinhaDeCorte = linhaDeCorte;
        ImportadoEm = importadoEm;
        TotalLinhas = totalLinhas;
    }
}
