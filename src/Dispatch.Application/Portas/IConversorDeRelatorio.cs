using Dispatch.Domain;

namespace Dispatch.Application;

// Porta do "conector de relatório" (ADR-0045). O Dispatch é agnóstico de cartório: a importação
// (ImportarLote) só conhece LinhaImportacao — protocolo, tipo de ato, escrevente, instante do
// andamento. Cada cartório exporta o relatório do SEU sistema num formato próprio (hoje, um .xls de
// layout de impressão); traduzir esse arquivo nas linhas genéricas é trabalho de um adaptador.
//
// Por que porta aqui e implementação na Infrastructure: ler .xls exige biblioteca de terceiros
// (ExcelDataReader) e conhecer o layout de um sistema específico — os dois são detalhe de fora, que a
// Application não deve conhecer (ADR-0001). A Application define O QUE precisa ("me dê as linhas e a
// etapa deste arquivo, ou diga por que não dá"); a Infrastructure decide COMO. Um cartório novo, ou o
// mesmo cartório exportando .xlsx, vira outra classe implementando esta interface, sem tocar no caso
// de uso nem no endpoint.
//
// Síncrono de propósito: o caso de uso já entrega o arquivo inteiro em memória (MemoryStream,
// limitado a poucos MB pelo endpoint), e as bibliotecas de planilha leem de forma síncrona.
public interface IConversorDeRelatorio
{
    // Contrato com quem implementa: arquivo que não é do seu formato devolve FormatoNaoReconhecido
    // (nunca exceção) — é assim que ConverterRelatorio experimenta um conector depois do outro.
    ResultadoConversaoRelatorio Converter(Stream arquivo);
}

public abstract record ResultadoConversaoRelatorio
{
    private ResultadoConversaoRelatorio() { }

    // TotalDeclarado vem do próprio relatório ("Total Protocolos ..."); TotalLido é o que o conector
    // conseguiu montar. No sucesso os dois são iguais — os dois vão na resposta pra tela mostrar a
    // conferência ("26 de 26") sem ter de confiar num número só.
    public sealed record Convertido(
        string Conector, Etapa Etapa, IReadOnlyList<LinhaImportacao> Linhas, int TotalDeclarado, int TotalLido)
        : ResultadoConversaoRelatorio;

    // Não é arquivo deste formato (não abre como planilha, ou não tem o layout esperado).
    public sealed record FormatoNaoReconhecido : ResultadoConversaoRelatorio;

    // O relatório é do formato certo, mas de um andamento que não é pré nem pós-conferência.
    public sealed record AndamentoNaoEhConferencia(string Andamento) : ResultadoConversaoRelatorio;

    // Pré e pós no mesmo arquivo — o lote é sempre de uma etapa só (RF-05a).
    public sealed record EtapasMisturadas : ResultadoConversaoRelatorio;

    // A soma lida não bate com o total que o próprio relatório declara: o layout mudou e o conector
    // perdeu (ou inventou) linha. Melhor recusar do que importar pela metade.
    public sealed record TotaisNaoConferem(int TotalDeclarado, int TotalLido) : ResultadoConversaoRelatorio;

    public sealed record RelatorioVazio : ResultadoConversaoRelatorio;
}
