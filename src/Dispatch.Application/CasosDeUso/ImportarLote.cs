using Dispatch.Domain;

namespace Dispatch.Application;

// RF-05 a RF-12. Duas operações públicas com a mesma lógica por dentro (RF-11: nada é
// persistido até a confirmação) — a diferença entre elas é só se o resultado é gravado ou
// descartado no final. Não existe estado de "lote pendente" guardado entre uma chamada e
// outra: confirmar reprocessa as mesmas linhas do zero, dessa vez persistindo.
public sealed class ImportarLote(
    IEscreventeRepository escreventes,
    IEquipeRepository equipes,
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IProtocoloRepository protocolos,
    ILoteImportacaoRepository lotes,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public Task<ResumoImportacao> PreVisualizarAsync(
        IReadOnlyCollection<LinhaImportacao> linhas, Etapa etapa, DateTimeOffset linhaDeCorte, CancellationToken cancellationToken = default) =>
        ProcessarAsync(linhas, etapa, linhaDeCorte, persistir: false, cancellationToken);

    public Task<ResumoImportacao> ConfirmarAsync(
        IReadOnlyCollection<LinhaImportacao> linhas, Etapa etapa, DateTimeOffset linhaDeCorte, CancellationToken cancellationToken = default) =>
        ProcessarAsync(linhas, etapa, linhaDeCorte, persistir: true, cancellationToken);

    private async Task<ResumoImportacao> ProcessarAsync(
        IReadOnlyCollection<LinhaImportacao> linhas,
        Etapa etapa,
        DateTimeOffset linhaDeCorte,
        bool persistir,
        CancellationToken cancellationToken)
    {
        // RF-07: "duplicata" aqui não é número de protocolo repetido (um protocolo reprovado
        // volta ao relatório com andamento novo, legitimamente) — é qualquer linha com
        // andamento igual ou anterior à linha de corte, ou seja, já processada num lote antes.
        var relevantes = linhas.Where(l => l.DataHoraAndamento > linhaDeCorte).ToList();

        // Criado antes do laço só quando vai persistir de verdade — na prévia não existe lote
        // nenhum (RF-11), os protocolos calculados ali são só pra mostrar, nunca gravados.
        LoteImportacao? lote = persistir
            ? new LoteImportacao(Guid.NewGuid(), etapa, linhaDeCorte, relogio.Agora, relevantes.Count)
            : null;

        var escreventesConhecidos = (await escreventes.ObterTodosAsync(cancellationToken)).ToList();
        var equipesTodas = await equipes.ObterTodasAsync(cancellationToken);
        var conferentesNaEscala = await conferentes.ObterNaEscalaAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var catalogoTipos = await tiposAto.ObterTodosAsync(cancellationToken);

        var novosEscreventes = new List<Escrevente>();
        var atribuicoes = new Dictionary<Guid, int>();
        var enviadosParaPool = 0;
        var excecoes = 0;
        var tiposDesconhecidos = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var escreventesSemEquipe = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var linha in relevantes)
        {
            var escrevente = escreventesConhecidos.FirstOrDefault(
                e => string.Equals(e.Nome, linha.Escrevente, StringComparison.OrdinalIgnoreCase));
            if (escrevente is null)
            {
                // RF-09: escrevente desconhecido nasce sem equipe — vira alocação manual
                // depois, na Central de regras.
                escrevente = new Escrevente(Guid.NewGuid(), linha.Escrevente, equipeId: null);
                escreventesConhecidos.Add(escrevente);
                novosEscreventes.Add(escrevente);
            }

            var tipoAto = catalogoTipos.FirstOrDefault(
                t => string.Equals(t.Nome, linha.TipoAto, StringComparison.OrdinalIgnoreCase));
            if (tipoAto is null)
            {
                tiposDesconhecidos.Add(linha.TipoAto);
            }

            var protocolo = new Protocolo(
                Guid.NewGuid(), linha.Protocolo, tipoAto?.Id, escrevente.Id, etapa, linha.DataHoraAndamento, loteImportacaoId: lote?.Id);

            var resultado = AplicadorDeDistribuicao.Executar(
                protocolo, escrevente, equipesTodas, conferentesNaEscala, regrasAtivas, catalogoTipos,
                out var resolucaoPrazo);

            if (resolucaoPrazo.SemEquipeSinalizado)
            {
                escreventesSemEquipe.Add(escrevente.Nome);
            }

            switch (resultado)
            {
                case ResultadoDistribuicao.Atribuido atribuido:
                    atribuicoes[atribuido.Conferente.Id] = atribuicoes.GetValueOrDefault(atribuido.Conferente.Id) + 1;
                    break;
                case ResultadoDistribuicao.EnviadoParaPool:
                    enviadosParaPool++;
                    break;
                case ResultadoDistribuicao.Excecao:
                    excecoes++;
                    break;
            }

            if (persistir)
            {
                protocolos.Adicionar(protocolo);
            }
        }

        if (persistir)
        {
            lotes.Adicionar(lote!);

            foreach (var escrevente in novosEscreventes)
            {
                escreventes.Adicionar(escrevente);
            }

            await unitOfWork.SalvarAsync(cancellationToken);
        }

        return new ResumoImportacao(
            lote?.Id,
            linhas.Count,
            linhas.Count - relevantes.Count,
            relevantes.Count,
            atribuicoes.Select(par => new AtribuicaoPorConferente(par.Key, par.Value)).ToList(),
            enviadosParaPool,
            excecoes,
            tiposDesconhecidos.ToList(),
            escreventesSemEquipe.ToList());
    }
}
