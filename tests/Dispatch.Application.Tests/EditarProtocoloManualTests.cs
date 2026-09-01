using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class EditarProtocoloManualTests
{
    private static readonly DateTimeOffset AndamentoOriginal = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static EditarProtocoloManual NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Escrevente> escreventes, IReadOnlyCollection<Equipe> equipes,
        IReadOnlyCollection<Conferente> conferentes, IReadOnlyCollection<RegraAlcada> regras, IReadOnlyCollection<TipoAto> tiposAto) =>
        new(
            new FakeProtocoloRepository(protocolos), new FakeEscreventeRepository(escreventes), new FakeEquipeRepository(equipes),
            new FakeConferenteRepository(conferentes), new FakeRegraAlcadaRepository(regras), new FakeTipoAtoRepository(tiposAto),
            new FakeUnitOfWork());

    [Fact]
    public async Task ProtocoloInexistente_DevolveNaoEncontrado()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], [], [], []);

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), Guid.NewGuid(), "Alguém", Etapa.PosConferencia, Prioridade.Normal, null);

        Assert.IsType<ResultadoEditarProtocoloManual.NaoEncontrado>(resultado);
    }

    [Fact]
    public async Task SoTrocarPrioridadeOuObservacao_NaoMexeNoVencimento()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var escrevente = new Escrevente(Guid.NewGuid(), "Ana", equipeId: null);
        var protocolo = new Protocolo(Guid.NewGuid(), "999020", tipo.Id, escrevente.Id, Etapa.PosConferencia, AndamentoOriginal);
        protocolo.DefinirPrazo(new Prazo(TipoPrazo.D1), AndamentoOriginal);
        var vencimentoOriginal = protocolo.VencimentoEm;
        var casoDeUso = NovoCasoDeUso([protocolo], [escrevente], [], [], [], [tipo]);

        var resultado = await casoDeUso.ExecutarAsync(
            protocolo.Id, tipo.Id, "Ana", Etapa.PosConferencia, Prioridade.Alta, "nova observação");

        Assert.IsType<ResultadoEditarProtocoloManual.Sucesso>(resultado);
        Assert.Equal(vencimentoOriginal, protocolo.VencimentoEm);
        Assert.Equal(Prioridade.Alta, protocolo.Prioridade);
        Assert.Equal("nova observação", protocolo.Observacao);
    }

    [Fact]
    public async Task TrocarEtapa_RecalculaVencimentoAPartirDoAndamentoOriginal()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.UmaHora), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Ana", equipe.Id);
        var protocolo = new Protocolo(Guid.NewGuid(), "999021", tipo.Id, escrevente.Id, Etapa.PosConferencia, AndamentoOriginal);
        protocolo.DefinirPrazo(new Prazo(TipoPrazo.D1), AndamentoOriginal);
        var casoDeUso = NovoCasoDeUso([protocolo], [escrevente], [equipe], [], [], [tipo]);

        // Troca pra Pré-conferência, que nessa equipe é "1 hora" — vencimento tem que sair de
        // AndamentoOriginal + 1h, não de "agora" (a Fake não recebe relógio nenhum de propósito,
        // se o caso de uso usasse "agora" o teste quebraria por falta de IRelogio).
        await casoDeUso.ExecutarAsync(protocolo.Id, tipo.Id, "Ana", Etapa.PreConferencia, Prioridade.Normal, null);

        Assert.Equal(AndamentoOriginal.AddHours(1), protocolo.VencimentoEm);
        Assert.Equal(Etapa.PreConferencia, protocolo.Etapa);
    }

    [Fact]
    public async Task DonoPerdeAlcadaAoTrocarTipo_VoltaProPool()
    {
        var tipoAntigo = new TipoAto(Guid.NewGuid(), "Inventário");
        var tipoNovo = new TipoAto(Guid.NewGuid(), "Testamento");
        var escrevente = new Escrevente(Guid.NewGuid(), "Ana", equipeId: null);
        var usuarioId = Guid.NewGuid();
        var dono = new Conferente(Guid.NewGuid(), usuarioId, Nivel.Junior, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "999022", tipoAntigo.Id, escrevente.Id, Etapa.PosConferencia, AndamentoOriginal);
        protocolo.DefinirPrazo(new Prazo(TipoPrazo.D1), AndamentoOriginal);
        protocolo.AtribuirA(dono.Id, AndamentoOriginal);
        // Nega Júnior pro tipo novo — depois da troca, o dono não tem mais alçada.
        var regra = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipoNovo.Id), OrigemRegra.Manual, ativa: true);
        var casoDeUso = NovoCasoDeUso([protocolo], [escrevente], [], [dono], [regra], [tipoAntigo, tipoNovo]);

        await casoDeUso.ExecutarAsync(protocolo.Id, tipoNovo.Id, "Ana", Etapa.PosConferencia, Prioridade.Normal, null);

        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }

    [Fact]
    public async Task DonoMantemAlcada_ContinuaAtribuido()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var escreventeAntigo = new Escrevente(Guid.NewGuid(), "Ana", equipeId: null);
        var escreventeNovo = new Escrevente(Guid.NewGuid(), "Bruno", equipeId: null);
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Senior, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "999023", tipo.Id, escreventeAntigo.Id, Etapa.PosConferencia, AndamentoOriginal);
        protocolo.DefinirPrazo(new Prazo(TipoPrazo.D1), AndamentoOriginal);
        protocolo.AtribuirA(dono.Id, AndamentoOriginal);
        var casoDeUso = NovoCasoDeUso([protocolo], [escreventeAntigo, escreventeNovo], [], [dono], [], [tipo]);

        // Só troca o escrevente (mesmo tipo/etapa) — sem regra nenhuma negando, padrão aberto
        // continua permitindo, dono não deveria perder o protocolo.
        await casoDeUso.ExecutarAsync(protocolo.Id, tipo.Id, "Bruno", Etapa.PosConferencia, Prioridade.Normal, null);

        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(dono.Id, protocolo.DonoId);
        Assert.Equal(escreventeNovo.Id, protocolo.EscreventeId);
    }
}
