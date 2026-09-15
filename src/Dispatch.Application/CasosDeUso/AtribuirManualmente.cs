using Dispatch.Domain;

namespace Dispatch.Application;

// RF-17 (fila de exceções) + pedido do dono ("mandar um ato pra um conferente manualmente"):
// atribuir na mão, sem passar pelo motor — a distribuidora escolhe a pessoa, sem checagem de
// alçada (decisão humana deliberada, mesmo espírito de sempre já usado na resolução de
// exceção — RNF-02 só exige que a decisão seja auditável, não que passe pelo motor).
// Elegível em Pool (ainda sem dono), Excecao (motor não soube decidir) e Atribuido (redireciona
// direto pra outra pessoa, sem precisar devolver ao pool antes) — não em Conferindo/concluído/
// descartado/excluído, onde reatribuir na mão interromperia trabalho já em andamento ou não
// faria sentido.
public sealed class AtribuirManualmente(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoAtribuirManualmente> ExecutarAsync(
        Guid protocoloId, Guid conferenteId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoAtribuirManualmente.ProtocoloNaoEncontrado;
        }

        if (protocolo.Status is not (StatusProtocolo.Pool or StatusProtocolo.Excecao or StatusProtocolo.Atribuido))
        {
            return ResultadoAtribuirManualmente.ProtocoloNaoElegivel;
        }

        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return ResultadoAtribuirManualmente.ConferenteNaoEncontrado;
        }

        protocolo.AtribuirA(conferente.Id, relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoAtribuirManualmente.Sucesso;
    }
}

public enum ResultadoAtribuirManualmente
{
    Sucesso,
    ProtocoloNaoEncontrado,
    ProtocoloNaoElegivel,
    ConferenteNaoEncontrado
}
