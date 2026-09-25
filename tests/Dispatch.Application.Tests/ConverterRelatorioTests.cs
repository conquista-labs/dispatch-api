using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ConverterRelatorioTests
{
    private static readonly ResultadoConversaoRelatorio Convertido = new ResultadoConversaoRelatorio.Convertido(
        "Conector B", Etapa.PosConferencia, [], TotalDeclarado: 0, TotalLido: 0);

    // Com mais de um conector registrado, o primeiro que reconhece o arquivo responde — e cada um
    // lê o arquivo do começo, mesmo depois de outro ter consumido o stream.
    [Fact]
    public async Task UsaOPrimeiroConectorQueReconheceOArquivo_LendoDoComeco()
    {
        var naoReconhece = new ConversorFake(new ResultadoConversaoRelatorio.FormatoNaoReconhecido());
        var reconhece = new ConversorFake(Convertido);
        var nuncaChamado = new ConversorFake(new ResultadoConversaoRelatorio.RelatorioVazio());
        var casoDeUso = new ConverterRelatorio([naoReconhece, reconhece, nuncaChamado]);

        var resultado = await casoDeUso.ExecutarAsync(new MemoryStream([1, 2, 3]));

        Assert.Same(Convertido, resultado);
        Assert.Equal([1, 2, 3], naoReconhece.BytesLidos);
        Assert.Equal([1, 2, 3], reconhece.BytesLidos);
        Assert.Null(nuncaChamado.BytesLidos);
    }

    // Erro de um conector que reconheceu o formato (totais, etapas) é a resposta — não passa pro próximo.
    [Fact]
    public async Task ErroDeQuemReconheceu_EhAResposta()
    {
        var totais = new ResultadoConversaoRelatorio.TotaisNaoConferem(3, 2);
        var casoDeUso = new ConverterRelatorio([new ConversorFake(totais), new ConversorFake(Convertido)]);

        Assert.Same(totais, await casoDeUso.ExecutarAsync(new MemoryStream([1])));
    }

    [Fact]
    public async Task NenhumConectorReconhece_FormatoNaoReconhecido()
    {
        var casoDeUso = new ConverterRelatorio([new ConversorFake(new ResultadoConversaoRelatorio.FormatoNaoReconhecido())]);

        Assert.IsType<ResultadoConversaoRelatorio.FormatoNaoReconhecido>(await casoDeUso.ExecutarAsync(new MemoryStream([1])));
    }

    private sealed class ConversorFake(ResultadoConversaoRelatorio resultado) : IConversorDeRelatorio
    {
        public byte[]? BytesLidos { get; private set; }

        public ResultadoConversaoRelatorio Converter(Stream arquivo)
        {
            using var copia = new MemoryStream();
            arquivo.CopyTo(copia);
            BytesLidos = copia.ToArray();
            return resultado;
        }
    }
}
