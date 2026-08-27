using Dispatch.Domain;

namespace Dispatch.Infrastructure.Persistencia;

// Mesmo padrão de RegraAlcadaRegistro: PayloadSugestao é uma hierarquia fechada com 4 variantes
// (Domain), e o EF Core não mapeia isso direto sem inheritance mapping. Aqui cada variante leva
// suas próprias colunas (nunca reaproveitadas entre variantes, mesmo quando o tipo bate — Guid
// de EscreventeOrfao e de RiscoQualidade significam coisas diferentes, misturar a coluna seria
// economia de clareza por muito pouco). `Tipo` é o discriminador; SugestaoRepository traduz
// pros/dos 4 tipos ricos do Domain.
internal sealed class SugestaoRegistro
{
    public Guid Id { get; set; }
    public string Chave { get; set; } = "";
    public TipoSugestaoRegistro Tipo { get; set; }

    public string? TipoDesconhecidoNomeTipo { get; set; }
    public Nivel? TipoDesconhecidoNivelSugerido { get; set; }

    public Guid? PrazoIrrealEquipeId { get; set; }
    public Etapa? PrazoIrrealEtapa { get; set; }
    public TipoPrazo? PrazoIrrealPrazoSugerido { get; set; }

    public Guid? EscreventeOrfaoEscreventeId { get; set; }
    public Guid? EscreventeOrfaoEquipeSugeridaId { get; set; }

    public Guid? RiscoQualidadeTipoAtoId { get; set; }
    public Nivel? RiscoQualidadeNivelRestrito { get; set; }

    public string Evidencia { get; set; } = "";
    public int Ocorrencias { get; set; }
    public StatusSugestao Status { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset AtualizadaEm { get; set; }
    public DateTimeOffset? DecididaEm { get; set; }
    public DateTimeOffset? DescartarAte { get; set; }
}

internal enum TipoSugestaoRegistro
{
    TipoDesconhecido,
    PrazoIrreal,
    EscreventeOrfao,
    RiscoQualidade
}
