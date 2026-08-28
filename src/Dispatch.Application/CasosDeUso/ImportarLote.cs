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
        IReadOnlyCollection<LinhaImportacao> linhas, Etapa etapa, DateTimeOffset linhaDeCorte,
        TimeSpan faixaAtencao, TimeSpan faixaUrgente, CancellationToken cancellationToken = default) =>
        ProcessarAsync(linhas, etapa, linhaDeCorte, persistir: false, faixaAtencao, faixaUrgente, cancellationToken);

    public Task<ResumoImportacao> ConfirmarAsync(
        IReadOnlyCollection<LinhaImportacao> linhas, Etapa etapa, DateTimeOffset linhaDeCorte, CancellationToken cancellationToken = default) =>
        // Faixas do semáforo não importam aqui: persistir=true nunca monta LinhaPreviaImportacao
        // (ver o `if (!persistir)` abaixo), então esses valores nunca chegam a ser lidos.
        ProcessarAsync(linhas, etapa, linhaDeCorte, persistir: true, TimeSpan.Zero, TimeSpan.Zero, cancellationToken);

    private async Task<ResumoImportacao> ProcessarAsync(
        IReadOnlyCollection<LinhaImportacao> linhas,
        Etapa etapa,
        DateTimeOffset linhaDeCorte,
        bool persistir,
        TimeSpan faixaAtencao,
        TimeSpan faixaUrgente,
        CancellationToken cancellationToken)
    {
        var agora = relogio.Agora;

        // RF-07: "duplicata" aqui não é número de protocolo repetido (um protocolo reprovado
        // volta ao relatório com andamento novo, legitimamente) — é qualquer linha com
        // andamento igual ou anterior à linha de corte, ou seja, já processada num lote antes.
        var relevantes = linhas.Where(l => l.DataHoraAndamento > linhaDeCorte).ToList();

        // Criado antes do laço só quando vai persistir de verdade — na prévia não existe lote
        // nenhum (RF-11), os protocolos calculados ali são só pra mostrar, nunca gravados.
        LoteImportacao? lote = persistir
            ? new LoteImportacao(Guid.NewGuid(), etapa, linhaDeCorte, agora, relevantes.Count)
            : null;

        var escreventesConhecidos = (await escreventes.ObterTodosAsync(cancellationToken)).ToList();
        var equipesTodas = await equipes.ObterTodasAsync(cancellationToken);
        var conferentesNaEscala = await conferentes.ObterNaEscalaAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var catalogoTipos = (await tiposAto.ObterTodosAsync(cancellationToken)).ToList();

        var novosEscreventes = new List<Escrevente>();
        var novosTipos = new List<TipoAto>();
        var atribuicoes = new Dictionary<Guid, int>();
        var enviadosParaPool = 0;
        var excecoes = 0;
        var tiposDesconhecidos = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var escreventesSemEquipe = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var linhasPreview = persistir ? null : new List<LinhaPreviaImportacao>();

        foreach (var linha in linhas)
        {
            // RF-08 pede a regra por linha, mas uma linha antes da corte nunca é distribuída —
            // não tem prazo, equipe ou avaliação de alçada de verdade pra mostrar, só o fato de
            // que já foi ignorada.
            if (linha.DataHoraAndamento <= linhaDeCorte)
            {
                linhasPreview?.Add(new LinhaPreviaImportacao(
                    linha.Protocolo, linha.TipoAto, TipoConhecido: false, linha.Escrevente,
                    Equipe: null, Prazo: null, VencimentoEm: null, Semaforo: null,
                    JaExiste: true, ComAlcada: 0));
                continue;
            }

            var escrevente = escreventesConhecidos.FirstOrDefault(
                e => string.Equals(e.Nome, linha.Escrevente, StringComparison.OrdinalIgnoreCase));
            if (escrevente is null)
            {
                // RF-09: escrevente desconhecido nasce sem equipe — vira alocação manual
                // depois, na Central de regras. Nome vem do relatório em caixa alta — normaliza
                // antes de gravar (ninguém trata nome de gente assim na prática).
                escrevente = new Escrevente(Guid.NewGuid(), NormalizadorDeTexto.ParaNomeProprio(linha.Escrevente), equipeId: null);
                escreventesConhecidos.Add(escrevente);
                novosEscreventes.Add(escrevente);
            }

            var tipoAto = catalogoTipos.FirstOrDefault(
                t => string.Equals(t.Nome, linha.TipoAto, StringComparison.OrdinalIgnoreCase));
            var tipoJaExistia = tipoAto is not null;
            if (tipoAto is null)
            {
                // Tipo de ato novo entra direto no catálogo (nome normalizado) — não fica
                // esperando revisão humana como uma sugestão de aprendizado (RF-39/RF-40): sem
                // isso, todo protocolo desse tipo cai em exceção "tipo desconhecido" até alguém
                // aplicar uma sugestão que só existe depois de ≥5 ocorrências, travando um
                // cartório novo logo na primeira importação. A alçada (quem pode conferir esse
                // tipo) continua exigindo regra explícita quando negada — só o cadastro do tipo
                // em si deixou de travar. `tiposDesconhecidos` continua sinalizando (RF-09),
                // agora como "isso é novo, acabou de entrar no catálogo".
                tipoAto = new TipoAto(Guid.NewGuid(), NormalizadorDeTexto.ParaNomeProprio(linha.TipoAto));
                catalogoTipos.Add(tipoAto);
                novosTipos.Add(tipoAto);
                tiposDesconhecidos.Add(tipoAto.Nome);
            }

            var protocolo = new Protocolo(
                Guid.NewGuid(), linha.Protocolo, tipoAto.Id, escrevente.Id, etapa, linha.DataHoraAndamento,
                loteImportacaoId: lote?.Id, tipoAtoNomeOriginal: linha.TipoAto);

            var resultado = AplicadorDeDistribuicao.Executar(
                protocolo, escrevente, equipesTodas, conferentesNaEscala, regrasAtivas, catalogoTipos, agora,
                out var resolucaoPrazo);

            if (resolucaoPrazo.SemEquipeSinalizado)
            {
                escreventesSemEquipe.Add(escrevente.Nome);
            }

            var comAlcada = resultado switch
            {
                ResultadoDistribuicao.Atribuido a => a.Elegiveis.Count,
                ResultadoDistribuicao.EnviadoParaPool p => p.Elegiveis.Count,
                ResultadoDistribuicao.Excecao e => e.Avaliacoes.Count(av => av.Elegivel),
                _ => 0
            };

            linhasPreview?.Add(new LinhaPreviaImportacao(
                linha.Protocolo, linha.TipoAto, TipoConhecido: tipoJaExistia, linha.Escrevente,
                resolucaoPrazo.Equipe?.Nome, resolucaoPrazo.Prazo.Tipo, protocolo.VencimentoEm,
                protocolo.VencimentoEm is { } vencimento ? Semaforo.Calcular(vencimento, agora, faixaAtencao, faixaUrgente) : null,
                JaExiste: false, comAlcada));

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

            foreach (var tipo in novosTipos)
            {
                tiposAto.Adicionar(tipo);
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
            escreventesSemEquipe.ToList(),
            linhasPreview);
    }
}
