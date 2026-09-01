using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ImportarLoteTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");
    private static readonly DateTimeOffset LinhaDeCorte = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);

    private static ImportarLote NovoCasoDeUso(
        out FakeEscreventeRepository escreventes,
        out FakeProtocoloRepository protocolos,
        out FakeTipoAtoRepository tiposAto,
        IReadOnlyCollection<Conferente>? conferentes = null,
        IReadOnlyCollection<Escrevente>? escreventesIniciais = null,
        IReadOnlyCollection<Equipe>? equipes = null)
    {
        escreventes = new FakeEscreventeRepository(escreventesIniciais ?? []);
        protocolos = new FakeProtocoloRepository([]);
        tiposAto = new FakeTipoAtoRepository([Inventario]);
        return new ImportarLote(
            escreventes,
            new FakeEquipeRepository(equipes ?? []),
            new FakeConferenteRepository(conferentes ?? []),
            new FakeRegraAlcadaRepository([]),
            tiposAto,
            protocolos,
            new FakeLoteImportacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(LinhaDeCorte));
    }

    [Fact]
    public async Task LinhaAntesDaLinhaDeCorte_EhIgnorada()
    {
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(-1)),
            new LinhaImportacao("262204", "Inventário", "Fulano", LinhaDeCorte.AddHours(1))
        };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(2, resumo.TotalNoArquivo);
        Assert.Equal(1, resumo.IgnoradasPelaLinhaDeCorte);
        Assert.Equal(1, resumo.Processadas);
    }

    [Fact]
    public async Task EscreventeDesconhecido_EhCriadoSemEquipeESinalizado()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano Novo", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Fulano Novo", resumo.EscreventesSemEquipe);
        // Prévia não persiste (RF-11) — o escrevente novo não pode ter ido pro repositório ainda.
        Assert.Equal(0, escreventes.Quantidade);
    }

    // Tipo de ato novo entra direto no catálogo (nome normalizado) em vez de travar esperando
    // uma sugestão de aprendizado (RF-39/RF-40) — sem conferente na escala, ainda vira exceção,
    // mas por "ninguém com alçada", não mais "tipo desconhecido" (o tipo já existe).
    [Fact]
    public async Task TipoDeAtoNovo_EhSinalizadoECadastradoNoCatalogo()
    {
        var linhas = new[] { new LinhaImportacao("262203", "ATO QUE NÃO EXISTE", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out var tiposAto);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Ato Que Não Existe", resumo.TiposDesconhecidos);
        Assert.Equal(1, resumo.Excecoes);
        // Prévia não persiste (RF-11) — o tipo novo não pode ter ido pro repositório ainda.
        Assert.Equal(1, tiposAto.Quantidade);
    }

    // Seção 11 do documento de requisitos: "ao distribuir um lote o motor considera a carga
    // acumulada dentro da própria rodada, e não apenas a carga já gravada". Dois protocolos
    // urgentes (prazo de 1h) do mesmo tipo/etapa, dois conferentes empatados em carga — sem
    // o incremento em memória, os dois iriam pro mesmo conferente (sempre o de menor carga
    // gravada); com o incremento, o segundo protocolo já enxerga a carga do primeiro.
    [Fact]
    public async Task DoisProtocolosUrgentesNoMesmoLote_NaoRepetemOMesmoConferente()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.UmaHora), new Prazo(TipoPrazo.UmaHora));
        var escreventeA = new Escrevente(Guid.NewGuid(), "Escrevente A", equipeId);
        var escreventeB = new Escrevente(Guid.NewGuid(), "Escrevente B", equipeId);
        var conferenteX = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var conferenteY = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Escrevente A", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "Inventário", "Escrevente B", LinhaDeCorte.AddHours(1))
        };
        var casoDeUso = NovoCasoDeUso(
            out _, out var protocolos, out _,
            [conferenteX, conferenteY], [escreventeA, escreventeB], [equipe]);

        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        var donos = protocolos.Todos.Select(p => p.DonoId).ToList();
        Assert.Equal(2, donos.Distinct().Count());
    }

    // Sem regra de alçada nenhuma, "ausência de regra = permitido" (RF-31) — um tipo novo com
    // pelo menos um conferente na escala já flui pro pool na hora, sem exceção nenhuma. É
    // exatamente o cenário de um cartório novo, sem nenhuma alçada configurada ainda.
    [Fact]
    public async Task TipoDeAtoNovo_ComConferenteNaEscala_VaiParaOPoolSemExcecao()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[] { new LinhaImportacao("262203", "VENDA E COMPRA", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, [conferente]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(0, resumo.Excecoes);
        Assert.Equal(1, resumo.EnviadosParaPool);
        Assert.Contains("Venda e Compra", resumo.TiposDesconhecidos);
    }

    // RF confirmado nesta sessão: relatório vem em CAIXA ALTA, mas o cadastro precisa sair
    // normalizado — tanto no tipo de ato (aqui) quanto no escrevente (teste acima).
    [Fact]
    public async Task Confirmar_CadastraTipoDeAtoNovoNormalizado()
    {
        var linhas = new[] { new LinhaImportacao("262203", "VENDA E COMPRA", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto);

        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        // Catálogo já nasce com "Inventário" (fixture) — "Venda e Compra" é o segundo.
        Assert.Equal(2, tiposAto.Quantidade);
        var protocolo = Assert.Single(protocolos.Todos);
        Assert.NotNull(protocolo.TipoAtoId);
    }

    [Fact]
    public async Task PreVisualizar_NaoPersisteNada()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(0, protocolos.Quantidade);
        Assert.Equal(0, escreventes.Quantidade);
        Assert.Null(resumo.LoteImportacaoId);
    }

    [Fact]
    public async Task Confirmar_PersisteProtocolosENovosEscreventes()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "Inventário", "Fulano", LinhaDeCorte.AddHours(2))
        };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos, out _, conferentes: [conferente]);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(2, protocolos.Quantidade);
        Assert.Equal(1, escreventes.Quantidade);
        Assert.Equal(2, resumo.EnviadosParaPool);
        Assert.NotNull(resumo.LoteImportacaoId);
        var todosDoLote = await protocolos.ObterParaDistribuicaoAsync(resumo.LoteImportacaoId, CancellationToken.None);
        Assert.Equal(2, todosDoLote.Count);
    }

    [Fact]
    public async Task ProtocoloUrgentePorPrazo_ContaNoResumoPorConferente()
    {
        // Prazo D0 (via equipe) é urgente mesmo sem prioridade alta — não tem coluna de
        // prioridade no relatório importado, só o prazo derivado da equipe decide isso aqui.
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D0));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var atribuicao = Assert.Single(resumo.AtribuidosPorConferente);
        Assert.Equal(conferente.Id, atribuicao.ConferenteId);
        Assert.Equal(1, atribuicao.Quantidade);
    }

    [Fact]
    public async Task PreVisualizar_LinhaAntesDaLinhaDeCorte_VemComoJaExisteSemPrazoResolvido()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(-1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.True(linha.JaExiste);
        Assert.Null(linha.Equipe);
        Assert.Null(linha.Prazo);
        Assert.Null(linha.Semaforo);
        Assert.Equal(0, linha.ComAlcada);
    }

    [Fact]
    public async Task PreVisualizar_LinhaComEquipe_TrazEquipeEPrazoDaRegra()
    {
        // Mesmo exemplo do documento de requisitos: "5º andar · pós-conferência".
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D0));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PosConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.False(linha.JaExiste);
        Assert.True(linha.TipoConhecido);
        Assert.Equal("5º andar", linha.Equipe);
        Assert.Equal(TipoPrazo.D0, linha.Prazo);
        Assert.NotNull(linha.VencimentoEm);
        Assert.NotNull(linha.Semaforo);
        Assert.Equal(1, linha.ComAlcada);
    }

    [Fact]
    public async Task PreVisualizar_TipoDesconhecido_TrazTipoConhecidoFalsoEComAlcadaZero()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Ato Que Não Existe", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.False(linha.TipoConhecido);
        Assert.Equal(0, linha.ComAlcada);
    }

    [Fact]
    public async Task Confirmar_NaoTrazLinhasDePrevia()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Null(resumo.Linhas);
    }
}
