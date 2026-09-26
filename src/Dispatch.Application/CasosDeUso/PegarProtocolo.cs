using Dispatch.Domain;

namespace Dispatch.Application;

// RF-20: conferente pegando um protocolo do pool pra si. Usa o MESMO pool que "Minha fila" mostra
// (PoolDoConferente: alçada + ordem da vez) — fora dele é sem alçada; dentro, a regra do pool do dono
// (ADR-0046) decide: mão cheia (atribuídos + em conferência ≥ LimiteDeAtosNaMao) recusa, e com
// PoolEmOrdemObrigatoria só o primeiro da vez passa. Só aqui: atribuição manual da distribuidora
// (AtribuirManualmente) e o motor não olham essa regra.
public sealed class PegarProtocolo(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IConfiguracaoRepository configuracao,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    private readonly PoolDoConferente _pool = new(protocolos, escreventes, regras, tiposAto, configuracao);

    public async Task<ResultadoPegarProtocolo> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return new ResultadoPegarProtocolo.NaoEncontrado();
        }

        // Também é o desfecho de quem perde a corrida pelo mesmo "primeiro da vez": quando o segundo
        // chega, o primeiro já o tirou do pool.
        if (protocolo.Status != StatusProtocolo.Pool)
        {
            return new ResultadoPegarProtocolo.NaoEstaNoPool();
        }

        var poolDisponivel = await _pool.ObterDisponivelAsync(conferente, cancellationToken);
        if (poolDisponivel.All(p => p.Id != protocoloId))
        {
            return new ResultadoPegarProtocolo.SemAlcada();
        }

        var atribuidos = await protocolos.ObterAtribuidosAAsync(conferente.Id, cancellationToken);
        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken);
        var regra = await _pool.CalcularRegraAsync(atribuidos.Count + emConferencia.Count, poolDisponivel, cancellationToken);
        var decisao = regra.Avaliar(protocoloId);
        switch (decisao)
        {
            case DecisaoDoPegar.LimiteNaMao:
                return new ResultadoPegarProtocolo.LimiteNaMao(regra.NaMao, regra.LimiteNaMao);
            case DecisaoDoPegar.ForaDaVez:
                return new ResultadoPegarProtocolo.ForaDaVez();
            case DecisaoDoPegar.Permitido:
                break;
            default:
                throw new InvalidOperationException($"Decisão não mapeada: {decisao}");
        }

        protocolo.AtribuirA(conferente.Id, relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoPegarProtocolo.Sucesso();
    }
}

// Hierarquia fechada (não mais enum) porque LimiteNaMao carrega os números que a mensagem mostra.
public abstract record ResultadoPegarProtocolo
{
    private ResultadoPegarProtocolo() { }

    public sealed record Sucesso : ResultadoPegarProtocolo;

    public sealed record NaoEncontrado : ResultadoPegarProtocolo;

    public sealed record NaoEstaNoPool : ResultadoPegarProtocolo;

    public sealed record SemAlcada : ResultadoPegarProtocolo;

    // ADR-0046: atribuídos + em conferência já chegou ao LimiteDeAtosNaMao.
    public sealed record LimiteNaMao(int NaMao, int Limite) : ResultadoPegarProtocolo;

    // ADR-0046: PoolEmOrdemObrigatoria ligada e este não é o primeiro da vez do pool dele.
    public sealed record ForaDaVez : ResultadoPegarProtocolo;
}
