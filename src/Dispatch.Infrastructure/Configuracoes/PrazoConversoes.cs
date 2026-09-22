using System.Globalization;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dispatch.Infrastructure.Configuracoes;

// Prazo é um value object pequeno (Tipo + HorarioDeVencimento opcional, só preenchido quando
// Tipo == CorteDeHorario). OwnsOne exigiria uma propriedade de navegação com setter (e ainda
// assim nunca pode ser preenchida via parâmetro de construtor — é uma limitação do EF Core pra
// tipos owned). Um ValueConverter trata a coluna como texto simples e converte pra/do tipo rico
// do Domain, então a propriedade continua um parâmetro de construtor normal, sem exigir nenhuma
// concessão de mutabilidade no Domain.
//
// Formato composto ("Tipo" ou "Tipo|HH:mm") — retrocompatível: toda linha gravada antes do corte
// de horário existir é só "D0"/"D1"/"D2"/"UmaHora", nunca contém "|", então o split continua
// funcionando sem migração de dado nenhuma.
internal static class PrazoConversoes
{
    public static readonly ValueConverter<Prazo, string> ParaTexto = new(
        prazo => Serializar(prazo),
        valor => Desserializar(valor));

    public static readonly ValueConverter<Prazo?, string?> ParaTextoOpcional = new(
        prazo => prazo == null ? null : Serializar(prazo),
        valor => valor == null ? null : Desserializar(valor));

    private static string Serializar(Prazo prazo) =>
        prazo.HorarioDeVencimento is { } horario
            ? $"{prazo.Tipo}|{horario.ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : prazo.Tipo.ToString();

    private static Prazo Desserializar(string valor)
    {
        var partes = valor.Split('|', 2);
        var tipo = Enum.Parse<TipoPrazo>(partes[0]);
        var horario = partes.Length > 1 ? TimeOnly.ParseExact(partes[1], "HH:mm", CultureInfo.InvariantCulture) : (TimeOnly?)null;
        return new Prazo(tipo, horario);
    }
}
