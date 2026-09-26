namespace Dispatch.Application;

// Converte o arquivo que o sistema do cartório exporta nas linhas genéricas da importação (RF-05,
// RF-06; ADR-0045). Não grava nada: o resultado volta pra tela, que segue o fluxo de sempre
// (pré-visualizar → confirmar) com essas linhas, como se tivessem sido coladas.
//
// Recebe TODOS os conectores registrados (IEnumerable<IConversorDeRelatorio> — o container de DI
// entrega a lista inteira quando há mais de um registro da mesma interface) e usa o primeiro que
// reconhecer o arquivo. Hoje há um só; um segundo formato é só mais um AddScoped no composition root.
public sealed class ConverterRelatorio(IEnumerable<IConversorDeRelatorio> conversores)
{
    public async Task<ResultadoConversaoRelatorio> ExecutarAsync(Stream arquivo, CancellationToken cancellationToken = default)
    {
        // Copia uma vez pra memória: cada conector lê o arquivo do começo (Position = 0), e o stream
        // do upload não volta atrás. O endpoint já limitou o tamanho, então caber na memória é garantido.
        using var copia = new MemoryStream();
        await arquivo.CopyToAsync(copia, cancellationToken);

        foreach (var conversor in conversores)
        {
            copia.Position = 0;
            var resultado = conversor.Converter(copia);
            if (resultado is not ResultadoConversaoRelatorio.FormatoNaoReconhecido)
            {
                return resultado;
            }
        }

        return new ResultadoConversaoRelatorio.FormatoNaoReconhecido();
    }
}
