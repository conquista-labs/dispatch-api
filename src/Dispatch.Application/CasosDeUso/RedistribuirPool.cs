using Dispatch.Domain;

namespace Dispatch.Application;

// RF-16: reaplica o motor a todo protocolo sem dono (Pool ou Exceção — os dois status sem
// DonoId). Não recalcula prazo (nada mudou sobre o relatório original); só reavalia elegibilidade
// contra o estado atual de conferentes/regras, que pode ter mudado desde a primeira distribuição
// (alguém saiu da escala, uma regra nova foi criada etc.).
public sealed class RedistribuirPool(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var semDono = await protocolos.ObterSemDonoAsync(cancellationToken);
        if (semDono.Count == 0)
        {
            return 0;
        }

        var conferentesNaEscala = await conferentes.ObterNaEscalaAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var catalogoTipos = await tiposAto.ObterTodosAsync(cancellationToken);
        var equipePorEscreventeId = (await escreventes.ObterTodosAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => e.EquipeId);

        var alterados = 0;
        foreach (var protocolo in semDono)
        {
            var statusAntes = protocolo.Status;
            var equipeDoEscreventeId = equipePorEscreventeId.GetValueOrDefault(protocolo.EscreventeId);
            var resultado = MotorDistribuicao.Distribuir(protocolo, conferentesNaEscala, regrasAtivas, catalogoTipos, equipeDoEscreventeId);

            switch (resultado)
            {
                case ResultadoDistribuicao.Atribuido atribuido:
                    protocolo.AtribuirA(atribuido.Conferente.Id, relogio.Agora, atribuido.Avaliacao.Decisao.RegraAplicada?.Id);
                    // Seção 11: carga acumulada dentro da própria rodada — este laço processa
                    // vários protocolos de uma vez, então o desempate por carga (dentro do
                    // motor) precisa enxergar as atribuições já feitas nesta mesma chamada.
                    atribuido.Conferente.IncrementarCargaAtual();
                    break;
                case ResultadoDistribuicao.EnviadoParaPool:
                    protocolo.EnviarParaPool();
                    break;
                case ResultadoDistribuicao.Excecao excecao:
                    protocolo.MarcarExcecao(excecao.Motivo);
                    break;
            }

            if (protocolo.Status != statusAntes)
            {
                alterados++;
            }
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return alterados;
    }
}
