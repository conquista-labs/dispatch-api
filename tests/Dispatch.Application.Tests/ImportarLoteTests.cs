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
        IReadOnlyCollection<Conferente>? conferentes = null,
        IReadOnlyCollection<Escrevente>? escreventesIniciais = null,
        IReadOnlyCollection<Equipe>? equipes = null)
    {
        escreventes = new FakeEscreventeRepository(escreventesIniciais ?? []);
        protocolos = new FakeProtocoloRepository([]);
        return new ImportarLote(
            escreventes,
            new FakeEquipeRepository(equipes ?? []),
            new FakeConferenteRepository(conferentes ?? []),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
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
        var casoDeUso = NovoCasoDeUso(out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(2, resumo.TotalNoArquivo);
        Assert.Equal(1, resumo.IgnoradasPelaLinhaDeCorte);
        Assert.Equal(1, resumo.Processadas);
    }

    [Fact]
    public async Task EscreventeDesconhecido_EhCriadoSemEquipeESinalizado()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano Novo", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Fulano Novo", resumo.EscreventesSemEquipe);
        // Prévia não persiste (RF-11) — o escrevente novo não pode ter ido pro repositório ainda.
        Assert.Equal(0, escreventes.Quantidade);
    }

    [Fact]
    public async Task TipoDeAtoDesconhecido_EhSinalizadoEViraExcecao()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Ato Que Não Existe", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Ato Que Não Existe", resumo.TiposDesconhecidos);
        Assert.Equal(1, resumo.Excecoes);
    }

    [Fact]
    public async Task PreVisualizar_NaoPersisteNada()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos);

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
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos, conferentes: [conferente]);

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
        var casoDeUso = NovoCasoDeUso(out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var atribuicao = Assert.Single(resumo.AtribuidosPorConferente);
        Assert.Equal(conferente.Id, atribuicao.ConferenteId);
        Assert.Equal(1, atribuicao.Quantidade);
    }

    [Fact]
    public async Task PreVisualizar_LinhaAntesDaLinhaDeCorte_VemComoJaExisteSemPrazoResolvido()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(-1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _);

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
        var casoDeUso = NovoCasoDeUso(out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

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
        var casoDeUso = NovoCasoDeUso(out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.False(linha.TipoConhecido);
        Assert.Equal(0, linha.ComAlcada);
    }

    [Fact]
    public async Task Confirmar_NaoTrazLinhasDePrevia()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Null(resumo.Linhas);
    }
}
