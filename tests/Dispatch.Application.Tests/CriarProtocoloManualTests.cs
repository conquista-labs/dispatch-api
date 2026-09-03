using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CriarProtocoloManualTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static (CriarProtocoloManual CasoDeUso, FakeProtocoloRepository Protocolos, FakeEscreventeRepository Escreventes) NovoCasoDeUso(
        IReadOnlyCollection<Conferente> conferentes, IReadOnlyCollection<Equipe> equipes, IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> tiposAto, IReadOnlyCollection<Escrevente> escreventes, IReadOnlyCollection<Protocolo> protocolosIniciais)
    {
        var protocolos = new FakeProtocoloRepository(protocolosIniciais);
        var escreventesFake = new FakeEscreventeRepository(escreventes);
        var distribuirProtocolo = new DistribuirProtocolo(
            new FakeConferenteRepository(conferentes), new FakeEquipeRepository(equipes), new FakeRegraAlcadaRepository(regras),
            new FakeTipoAtoRepository(tiposAto), protocolos, new FakeUnitOfWork(), new FakeRelogio(Agora));
        var casoDeUso = new CriarProtocoloManual(protocolos, escreventesFake, new FakeTipoAtoRepository(tiposAto), distribuirProtocolo, new FakeRelogio(Agora));
        return (casoDeUso, protocolos, escreventesFake);
    }

    [Fact]
    public async Task NumeroJaExistente_BloqueiaSemCriarNadaNovo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var existente = new Protocolo(Guid.NewGuid(), "999010", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, Agora);
        var (casoDeUso, protocolos, _) = NovoCasoDeUso([], [], [], [tipo], [], [existente]);

        var resultado = await casoDeUso.ExecutarAsync("999010", tipo.Id, "Alguém", Etapa.PosConferencia, Prioridade.Normal, observacao: null);

        Assert.IsType<ResultadoCriarProtocoloManual.NumeroJaExiste>(resultado);
        Assert.Equal(1, protocolos.Quantidade);
    }

    // Continuidade de conferência (mesmo fluxo da importação — ver ResolvedorDeContinuidade):
    // Número com histórico só Reprovado na mesma etapa libera o cadastro e atribui direto ao
    // mesmo dono, sem passar pelo motor normal.
    [Fact]
    public async Task NumeroComHistoricoReprovadoNaMesmaEtapa_LiberaEAtribuiDiretoAoDonoAnterior()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var donoAnterior = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var reprovadoAnterior = new Protocolo(Guid.NewGuid(), "999020", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, Agora.AddDays(-1));
        reprovadoAnterior.AtribuirA(donoAnterior.Id, Agora.AddDays(-1));
        reprovadoAnterior.Reprovar(Agora.AddDays(-1));
        var (casoDeUso, protocolos, _) = NovoCasoDeUso([donoAnterior], [], [], [tipo], [], [reprovadoAnterior]);

        var resultado = await casoDeUso.ExecutarAsync("999020", tipo.Id, "Alguém", Etapa.PosConferencia, Prioridade.Normal, observacao: null);

        var sucesso = Assert.IsType<ResultadoCriarProtocoloManual.Sucesso>(resultado);
        var atribuido = Assert.IsType<ResultadoDistribuicao.Atribuido>(sucesso.Distribuicao);
        Assert.Equal(donoAnterior.Id, atribuido.Conferente.Id);
        Assert.Equal(2, protocolos.Quantidade);
    }

    [Fact]
    public async Task NumeroComHistoricoAindaAtribuido_ContinuaBloqueando()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var existente = new Protocolo(Guid.NewGuid(), "999021", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, Agora);
        existente.AtribuirA(Guid.NewGuid(), Agora);
        var (casoDeUso, protocolos, _) = NovoCasoDeUso([], [], [], [tipo], [], [existente]);

        var resultado = await casoDeUso.ExecutarAsync("999021", tipo.Id, "Alguém", Etapa.PosConferencia, Prioridade.Normal, observacao: null);

        Assert.IsType<ResultadoCriarProtocoloManual.NumeroJaExiste>(resultado);
        Assert.Equal(1, protocolos.Quantidade);
    }

    [Fact]
    public async Task NumeroLivre_CriaEPersisteEResolveEscreventeNovo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var (casoDeUso, protocolos, escreventes) = NovoCasoDeUso([], [], [], [tipo], [], []);

        var resultado = await casoDeUso.ExecutarAsync("999011", tipo.Id, "Fulano de Tal", Etapa.PosConferencia, Prioridade.Normal, observacao: null);

        var sucesso = Assert.IsType<ResultadoCriarProtocoloManual.Sucesso>(resultado);
        Assert.Equal(1, protocolos.Quantidade);
        Assert.Equal(1, escreventes.Quantidade);
        Assert.Equal(sucesso.ProtocoloId, protocolos.Todos.Single().Id);
        Assert.NotNull(sucesso.VencimentoEm);
    }

    [Fact]
    public async Task TipoAtoIdDesconhecido_SalvaComoTipoNulo()
    {
        var (casoDeUso, protocolos, _) = NovoCasoDeUso([], [], [], [], [], []);

        await casoDeUso.ExecutarAsync("999012", Guid.NewGuid(), "Fulano", Etapa.PosConferencia, Prioridade.Normal, observacao: null);

        Assert.Null(protocolos.Todos.Single().TipoAtoId);
    }

    [Fact]
    public async Task ComObservacao_GravaJuntoNaCriacao()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var (casoDeUso, protocolos, _) = NovoCasoDeUso([], [], [], [tipo], [], []);

        await casoDeUso.ExecutarAsync("999013", tipo.Id, "Fulano", Etapa.PosConferencia, Prioridade.Normal, "Cliente pediu urgência");

        Assert.Equal("Cliente pediu urgência", protocolos.Todos.Single().Observacao);
    }
}
