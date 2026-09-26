namespace Dispatch.Domain;

// "A vez" do pool — regra do dono de 2026-09-26, não está no documento de requisitos (RF-20 só diz
// que "Pegar este" move do pool pra fila pessoal). Ver docs/decisions/0046-pool-em-ordem-e-limite-na-mao.md.
// Uma ordem só, usada pela leitura (GET /minha-fila, GET /conferentes/{id}/fila) e pela ação
// (POST /minha-fila/{id}/pegar): se as duas ordenassem cada uma do seu jeito, o botão que o front
// mostra e o que o servidor aceita discordariam.
public static class OrdemDoPool
{
    // 1. Prioridade decrescente (Alta, Normal, Baixa) — posto explícito em vez do valor numérico do
    //    enum, pra que reordenar os membros de Prioridade nunca mude a vez sem ninguém perceber.
    // 2. Quem vence antes; sem vencimento vai pro fim da sua prioridade.
    // 3. Quem entrou antes (AndamentoEm, o instante do relatório).
    // 4. Numero (ordinal) e Id — só pra que empate total nunca dependa da ordem que o banco devolveu.
    public static IReadOnlyList<Protocolo> Ordenar(IEnumerable<Protocolo> pool) =>
        pool
            .OrderBy(p => PostoDaPrioridade(p.Prioridade))
            .ThenBy(p => p.VencimentoEm is null)
            .ThenBy(p => p.VencimentoEm)
            .ThenBy(p => p.AndamentoEm)
            .ThenBy(p => p.Numero, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .ToList();

    private static int PostoDaPrioridade(Prioridade prioridade) => prioridade switch
    {
        Prioridade.Alta => 0,
        Prioridade.Normal => 1,
        Prioridade.Baixa => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "prioridade sem posto na ordem do pool")
    };
}

// O estado da regra pra UM conferente: a chave de ordem obrigatória e o limite (Configuração), quantos
// atos ele tem na mão (atribuídos + em conferência, pausado incluído) e o próximo da vez. É também a
// resposta `regraDoPool` das leituras de fila — o front desenha o botão a partir daqui, sem recalcular.
public sealed record RegraDoPool(bool OrdemObrigatoria, int LimiteNaMao, int NaMao, Guid? ProximoId)
{
    // ProximoId = o primeiro da ordem enquanto a mão não está cheia. Com a chave desligada continua
    // indicando o primeiro (o front pode ignorar): o campo quer dizer "o próximo da vez", e não muda
    // de sentido conforme a chave.
    public static RegraDoPool Calcular(bool ordemObrigatoria, int limiteNaMao, int naMao, IReadOnlyList<Protocolo> poolOrdenado) =>
        new(ordemObrigatoria, limiteNaMao, naMao, naMao < limiteNaMao ? poolOrdenado.FirstOrDefault()?.Id : null);

    // O limite vence a vez: com a mão cheia, nem o primeiro pode ser pego — e a mensagem certa pro
    // conferente é "termine algum", não "pegue outro".
    public DecisaoDoPegar Avaliar(Guid protocoloId)
    {
        if (NaMao >= LimiteNaMao)
        {
            return DecisaoDoPegar.LimiteNaMao;
        }

        if (OrdemObrigatoria && ProximoId != protocoloId)
        {
            return DecisaoDoPegar.ForaDaVez;
        }

        return DecisaoDoPegar.Permitido;
    }
}

public enum DecisaoDoPegar
{
    Permitido,
    LimiteNaMao,
    ForaDaVez
}
