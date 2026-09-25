using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dispatch.Application;
using Dispatch.Domain;
using ExcelDataReader;

namespace Dispatch.Infrastructure.Conectores;

// Adaptador do "Relatório de Andamentos dos Protocolos" que o sistema do cartório exporta em .xls
// (Excel 97-2003, formato binário BIFF8) — implementa a porta IConversorDeRelatorio (ADR-0045).
//
// O arquivo é um layout de IMPRESSÃO jogado numa planilha: ~38 colunas, células mescladas, cabeçalho
// do cartório repetido a cada página (inclusive no meio do bloco de um escrevente). Por isso a leitura
// não usa número de linha fixo: percorre as linhas de cima pra baixo como uma máquina de estados,
// ancorada no CONTEÚDO de colunas conhecidas:
//
//   col A  "Pré - Conferência" / "Pós - Conferência"      → etapa do lote
//   col A  nome + col V "Substituto:"                      → abre o bloco de um escrevente
//   col C  número (só dígitos) + col H "L: ..."            → abre um protocolo
//   col AB "dd/mm/aaaa" + col AE "hh:mm:ss" (logo abaixo)  → andamento do protocolo aberto
//   col J  tipo de ato (na linha seguinte à da data)       → fecha o protocolo
//   col R  "Total Protocolos Escrevente: N"                → fecha o bloco e confere a contagem
//   col A  "Total Protocolos Andamento: <etapa> - N"       → total geral, conferido contra a soma
//
// A conferência dos totais é a rede de segurança: se o sistema do cartório mudar o layout e alguma
// linha deixar de casar, a soma lida não bate com o total que o próprio relatório declara e o arquivo
// é recusado inteiro (TotaisNaoConferem) — nunca uma importação pela metade.
//
// Coluna do apresentante (Q) é ignorada: a importação não usa, e é dado pessoal.
public sealed class ConversorRelatorioDeAndamentosXls : IConversorDeRelatorio
{
    public const string NomeDoConector = "Relatório de Andamentos dos Protocolos";

    private const int ColunaA = 0;
    private const int ColunaNumero = 2;        // C
    private const int ColunaLivroFolhas = 7;   // H
    private const int ColunaTipoAto = 9;       // J
    private const int ColunaTotalEscrevente = 17; // R
    private const int ColunaSubstituto = 21;   // V
    private const int ColunaData = 27;         // AB
    private const int ColunaHora = 30;         // AE

    private static readonly Regex Andamento = new(
        @"^(?<etapa>pr[eé]|p[oó]s)\s*-\s*confer[eê]ncia$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TotalEscrevente = new(
        @"^Total Protocolos Escrevente:\s*(?<n>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TotalAndamento = new(
        @"^Total Protocolos Andamento:\s*(?<andamento>.+?)\s*-\s*(?<n>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SoDigitos = new(@"^\d+$", RegexOptions.CultureInvariant);

    // O formato .xls guarda parte do texto numa "code page" do Windows (ex.: 1252, Europa Ocidental).
    // O .NET moderno (Core/5+) vem só com UTF-8/UTF-16/ASCII/Latin-1 de fábrica; as code pages antigas
    // ficam num provedor à parte (CodePagesEncodingProvider, que já vem no runtime — não precisa de
    // pacote) e precisam ser REGISTRADAS antes de usar, senão o ExcelDataReader explode com
    // "No data is available for encoding 1252". Construtor estático = roda uma vez por processo, antes
    // do primeiro uso da classe — o registro fica junto de quem precisa dele, sem depender do Program.cs.
    static ConversorRelatorioDeAndamentosXls()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ResultadoConversaoRelatorio Converter(Stream arquivo)
    {
        var linhas = LerLinhas(arquivo);
        return linhas is null ? new ResultadoConversaoRelatorio.FormatoNaoReconhecido() : Interpretar(linhas);
    }

    // Só a leitura bruta fica dentro do try: o arquivo vem do usuário e qualquer coisa pode chegar
    // (CSV renomeado, .xlsx, .doc, planilha corrompida) — e a biblioteca sinaliza cada caso com uma
    // exceção diferente. Tudo isso é "não é o formato", um 400, nunca um 500. A interpretação fica fora
    // do try de propósito: um bug nela deve aparecer como erro, não ser mascarado como arquivo ruim.
    private static List<string[]>? LerLinhas(Stream arquivo)
    {
        try
        {
            // CreateBinaryReader = só o formato binário antigo (.xls). O .xlsx (OpenXML, um zip) teria o
            // CreateOpenXmlReader; fica de fora até existir um relatório nesse formato.
            using var leitor = ExcelReaderFactory.CreateBinaryReader(arquivo, new ExcelReaderConfiguration { LeaveOpen = true });
            var linhas = new List<string[]>();
            do
            {
                while (leitor.Read())
                {
                    var celulas = new string[leitor.FieldCount];
                    for (var i = 0; i < leitor.FieldCount; i++)
                    {
                        celulas[i] = ParaTexto(leitor.GetValue(i));
                    }
                    linhas.Add(celulas);
                }
            }
            while (leitor.NextResult()); // próxima aba (Plan2/Plan3 costumam vir vazias)

            return linhas;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Número de protocolo pode chegar como texto ("259350", o que o sistema exporta hoje) ou como
    // célula numérica (259350.0) se alguém abrir e salvar no Excel — os dois viram o mesmo texto.
    private static string ParaTexto(object? valor) => valor switch
    {
        null => "",
        string texto => texto.Trim(),
        double numero when numero == Math.Floor(numero) => numero.ToString("0", CultureInfo.InvariantCulture),
        IFormattable formatavel => formatavel.ToString(null, CultureInfo.InvariantCulture).Trim(),
        _ => valor.ToString()?.Trim() ?? "",
    };

    private static ResultadoConversaoRelatorio Interpretar(List<string[]> linhas)
    {
        var tituloEncontrado = false;
        var etapas = new HashSet<Etapa>();
        string? andamentoDesconhecido = null;
        var convertidas = new List<LinhaImportacao>();
        var totalLido = 0;

        string? escrevente = null;
        var lidosNoBloco = 0;
        var somaDosBlocos = 0;
        int? totalDosAndamentos = null;
        (int Declarado, int Lido)? primeiroBlocoQueNaoBate = null;

        ProtocoloAberto? aberto = null;

        foreach (var linha in linhas)
        {
            var colunaA = Celula(linha, ColunaA);

            if (linha.Any(c => c.Equals(NomeDoConector, StringComparison.OrdinalIgnoreCase)))
            {
                tituloEncontrado = true;
                continue;
            }

            if (Andamento.Match(colunaA) is { Success: true } andamento)
            {
                etapas.Add(ParaEtapa(andamento.Groups["etapa"].Value));
                continue;
            }

            if (TotalAndamento.Match(colunaA) is { Success: true } totalAndamento)
            {
                var nome = totalAndamento.Groups["andamento"].Value;
                if (Andamento.Match(nome) is { Success: true } etapaDoTotal)
                {
                    etapas.Add(ParaEtapa(etapaDoTotal.Groups["etapa"].Value));
                }
                else
                {
                    andamentoDesconhecido ??= nome;
                }
                totalDosAndamentos = (totalDosAndamentos ?? 0) + int.Parse(totalAndamento.Groups["n"].Value, CultureInfo.InvariantCulture);
                continue;
            }

            if (colunaA.Length > 0 && Celula(linha, ColunaSubstituto).StartsWith("Substituto", StringComparison.OrdinalIgnoreCase))
            {
                // Bloco novo. Protocolo que ficou aberto do bloco anterior não chegou até o tipo de
                // ato: não conta como lido, e a conferência de totais recusa o arquivo.
                aberto = null;
                escrevente = colunaA;
                lidosNoBloco = 0;
                continue;
            }

            if (TotalEscrevente.Match(Celula(linha, ColunaTotalEscrevente)) is { Success: true } totalEscrevente)
            {
                var declarado = int.Parse(totalEscrevente.Groups["n"].Value, CultureInfo.InvariantCulture);
                somaDosBlocos += declarado;
                if (declarado != lidosNoBloco)
                {
                    primeiroBlocoQueNaoBate ??= (declarado, lidosNoBloco);
                }
                aberto = null;
                escrevente = null;
                lidosNoBloco = 0;
                continue;
            }

            var numero = Celula(linha, ColunaNumero);
            if (SoDigitos.IsMatch(numero) && Celula(linha, ColunaLivroFolhas).StartsWith("L:", StringComparison.OrdinalIgnoreCase))
            {
                aberto = new ProtocoloAberto(numero, escrevente);
                continue;
            }

            if (aberto is null)
            {
                continue;
            }

            if (aberto.Andamento is null)
            {
                if (TentarLerAndamento(Celula(linha, ColunaData), Celula(linha, ColunaHora)) is { } instante)
                {
                    aberto = aberto with { Andamento = instante };
                }
                continue;
            }

            var tipoAto = Celula(linha, ColunaTipoAto);
            if (tipoAto.Length == 0)
            {
                continue;
            }

            totalLido++;
            if (aberto.Escrevente is not null)
            {
                lidosNoBloco++;
                convertidas.Add(new LinhaImportacao(aberto.Numero, tipoAto, aberto.Escrevente, aberto.Andamento.Value));
            }
            aberto = null;
        }

        if (!tituloEncontrado)
        {
            return new ResultadoConversaoRelatorio.FormatoNaoReconhecido();
        }

        if (andamentoDesconhecido is not null)
        {
            return new ResultadoConversaoRelatorio.AndamentoNaoEhConferencia(andamentoDesconhecido);
        }

        if (etapas.Count > 1)
        {
            return new ResultadoConversaoRelatorio.EtapasMisturadas();
        }

        var totalDeclarado = totalDosAndamentos ?? somaDosBlocos;
        if (totalLido == 0 && totalDeclarado == 0 && somaDosBlocos == 0)
        {
            return new ResultadoConversaoRelatorio.RelatorioVazio();
        }

        // Três conferências, da mais específica pra mais geral: cada bloco contra o seu "Total
        // Protocolos Escrevente"; tudo o que foi lido (inclusive protocolo fora de bloco) contra a soma
        // dos blocos; e a soma dos blocos contra o "Total Protocolos Andamento", quando ele existe.
        if (primeiroBlocoQueNaoBate is { } bloco)
        {
            return new ResultadoConversaoRelatorio.TotaisNaoConferem(bloco.Declarado, bloco.Lido);
        }
        if (totalLido != somaDosBlocos || totalDeclarado != somaDosBlocos)
        {
            return new ResultadoConversaoRelatorio.TotaisNaoConferem(totalDeclarado, totalLido);
        }

        if (etapas.Count == 0)
        {
            return new ResultadoConversaoRelatorio.FormatoNaoReconhecido();
        }

        return new ResultadoConversaoRelatorio.Convertido(NomeDoConector, etapas.Single(), convertidas, totalDeclarado, totalLido);
    }

    private static string Celula(string[] linha, int coluna) => coluna < linha.Length ? linha[coluna] : "";

    private static Etapa ParaEtapa(string prefixo) =>
        prefixo.StartsWith("pr", StringComparison.OrdinalIgnoreCase) ? Etapa.PreConferencia : Etapa.PosConferencia;

    // O relatório traz horário de parede do cartório (Brasília). Vira instante com o fuso que o Domain
    // já centraliza (FusoHorario) — o offset -03:00 viaja até a gravação, que converte pra UTC.
    private static DateTimeOffset? TentarLerAndamento(string data, string hora)
    {
        if (!DateOnly.TryParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            || !TimeOnly.TryParseExact(hora, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var horario))
        {
            return null;
        }

        return new DateTimeOffset(dia.ToDateTime(horario), FusoHorario.Brasilia);
    }

    private sealed record ProtocoloAberto(string Numero, string? Escrevente, DateTimeOffset? Andamento = null);
}
