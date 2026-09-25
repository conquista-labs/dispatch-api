using System.Text;
using Dispatch.Application;
using Dispatch.Domain;
using Dispatch.Infrastructure.Conectores;

namespace Dispatch.Api.Tests.Conectores;

// Teste de unidade do adaptador (sem banco, sem HTTP) — mora neste projeto porque é o que já
// referencia a Infrastructure, e os fixtures .xls são os mesmos que o teste do endpoint envia.
// Os arquivos são sintéticos, com nomes fictícios, gerados por Fixtures/gerar_fixtures.py
// reproduzindo o layout do relatório real (que tem dado pessoal e não entra no repositório).
public sealed class ConversorRelatorioDeAndamentosXlsTests
{
    private readonly ConversorRelatorioDeAndamentosXls _conversor = new();

    [Fact]
    public void PreConferencia_LeTodasAsLinhasComEtapaEFuso()
    {
        var resultado = Converter("relatorio-pre-conferencia.xls");

        var convertido = Assert.IsType<ResultadoConversaoRelatorio.Convertido>(resultado);
        Assert.Equal("Relatório de Andamentos dos Protocolos", convertido.Conector);
        Assert.Equal(Etapa.PreConferencia, convertido.Etapa);
        Assert.Equal(5, convertido.TotalDeclarado);
        Assert.Equal(5, convertido.TotalLido);
        Assert.Equal(
            ["900101", "900102", "900201", "900202", "900203"],
            convertido.Linhas.Select(l => l.Protocolo));

        // Primeira linha inteira: número, tipo, escrevente do bloco e horário de Brasília (-03:00).
        Assert.Equal(
            new LinhaImportacao("900101", "VENDA E COMPRA", "ANA PAULA FICTICIA",
                new DateTimeOffset(2026, 9, 22, 17, 34, 5, TimeSpan.FromHours(-3))),
            convertido.Linhas[0]);
        Assert.Equal("INVENTÁRIO", convertido.Linhas[1].TipoAto);
    }

    // O cabeçalho de página se repete no meio do bloco do 2º escrevente (a quebra de página do
    // relatório real): os protocolos depois da quebra continuam sendo dele.
    [Fact]
    public void QuebraDePaginaNoMeioDoBloco_MantemOEscrevente()
    {
        var convertido = Assert.IsType<ResultadoConversaoRelatorio.Convertido>(Converter("relatorio-pre-conferencia.xls"));

        Assert.All(convertido.Linhas.Skip(2), l => Assert.Equal("BRUNO TESTE EXEMPLO", l.Escrevente));
    }

    // Número de protocolo gravado como célula numérica (alguém abriu e salvou no Excel) vira o
    // mesmo texto, sem ".0".
    [Fact]
    public void NumeroComoCelulaNumerica_ViraTextoSemDecimal()
    {
        var convertido = Assert.IsType<ResultadoConversaoRelatorio.Convertido>(Converter("relatorio-pre-conferencia.xls"));

        Assert.Equal("900201", convertido.Linhas[2].Protocolo);
    }

    // 23h30 em Brasília já é o dia seguinte em UTC — o instante tem de ser o certo, não o "dia" do texto.
    [Fact]
    public void HorarioDeParedeDeBrasilia_ViraOInstanteCerto()
    {
        var convertido = Assert.IsType<ResultadoConversaoRelatorio.Convertido>(Converter("relatorio-pre-conferencia.xls"));

        Assert.Equal(new DateTimeOffset(2026, 9, 26, 2, 30, 0, TimeSpan.Zero), convertido.Linhas[4].DataHoraAndamento.ToUniversalTime());
    }

    [Fact]
    public void PosConferencia_DetectaAEtapa()
    {
        var convertido = Assert.IsType<ResultadoConversaoRelatorio.Convertido>(Converter("relatorio-pos-conferencia.xls"));

        Assert.Equal(Etapa.PosConferencia, convertido.Etapa);
        Assert.Equal("PROCURAÇÃO", Assert.Single(convertido.Linhas).TipoAto);
    }

    [Fact]
    public void BlocoDeclaraMaisDoQueTraz_RecusaPorTotais()
    {
        var resultado = Converter("relatorio-totais-nao-conferem.xls");

        var totais = Assert.IsType<ResultadoConversaoRelatorio.TotaisNaoConferem>(resultado);
        Assert.Equal(3, totais.TotalDeclarado);
        Assert.Equal(2, totais.TotalLido);
    }

    [Fact]
    public void PreEPosNoMesmoArquivo_RecusaPorEtapasMisturadas()
    {
        Assert.IsType<ResultadoConversaoRelatorio.EtapasMisturadas>(Converter("relatorio-etapas-misturadas.xls"));
    }

    [Fact]
    public void AndamentoSemProtocolo_EhRelatorioVazio()
    {
        Assert.IsType<ResultadoConversaoRelatorio.RelatorioVazio>(Converter("relatorio-vazio.xls"));
    }

    // Um .xls de verdade, mas sem o título do relatório — é de outro formato, não deste conector.
    [Fact]
    public void PlanilhaQueNaoEhORelatorio_FormatoNaoReconhecido()
    {
        Assert.IsType<ResultadoConversaoRelatorio.FormatoNaoReconhecido>(Converter("planilha-qualquer.xls"));
    }

    // CSV renomeado, .xlsx (zip), arquivo vazio: a biblioteca não abre — vira "não é o formato",
    // nunca exceção (que chegaria como 500).
    [Theory]
    [InlineData("protocolo,tipoAto,escrevente,dataHoraAndamento\n900001,VENDA,ANA,2026-09-22T10:00:00-03:00\n")]
    [InlineData("PK\u0003\u0004 isto finge ser um xlsx")]
    [InlineData("")]
    public void ArquivoQueNaoEhXls_FormatoNaoReconhecido(string conteudo)
    {
        using var arquivo = new MemoryStream(Encoding.UTF8.GetBytes(conteudo));

        Assert.IsType<ResultadoConversaoRelatorio.FormatoNaoReconhecido>(_conversor.Converter(arquivo));
    }

    private ResultadoConversaoRelatorio Converter(string fixture)
    {
        using var arquivo = File.OpenRead(CaminhoDoFixture(fixture));
        return _conversor.Converter(arquivo);
    }

    internal static string CaminhoDoFixture(string nome) =>
        Path.Combine(AppContext.BaseDirectory, "Conectores", "Fixtures", nome);
}
