using Dispatch.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dispatch.Infrastructure.Configuracoes;

// Prazo é um value object de um campo só (TipoPrazo). OwnsOne exigiria uma propriedade de
// navegação com setter (e ainda assim nunca pode ser preenchida via parâmetro de construtor —
// é uma limitação do EF Core pra tipos owned). Um ValueConverter trata a coluna como texto
// simples e converte pra/do tipo rico do Domain, então a propriedade continua um parâmetro de
// construtor normal, sem exigir nenhuma concessão de mutabilidade no Domain.
internal static class PrazoConversoes
{
    public static readonly ValueConverter<Prazo, string> ParaTexto = new(
        prazo => prazo.Tipo.ToString(),
        valor => new Prazo(Enum.Parse<TipoPrazo>(valor)));

    public static readonly ValueConverter<Prazo?, string?> ParaTextoOpcional = new(
        prazo => prazo == null ? null : prazo.Tipo.ToString(),
        valor => valor == null ? null : new Prazo(Enum.Parse<TipoPrazo>(valor)));
}
