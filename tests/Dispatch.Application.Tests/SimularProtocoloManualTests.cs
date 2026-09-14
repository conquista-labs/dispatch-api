using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class SimularProtocoloManualTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static SimularProtocoloManual NovoCasoDeUso(
        IReadOnlyCollection<Conferente> conferentes, IReadOnlyCollection<Equipe> equipes, IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> tiposAto, IReadOnlyCollection<Escrevente> escreventes, IReadOnlyCollection<Protocolo> protocolos) =>
        new(
            new FakeConferenteRepository(conferentes), new FakeEquipeRepository(equipes), new FakeRegraAlcadaRepository(regras),
            new FakeTipoAtoRepository(tiposAto), new FakeEscreventeRepository(escreventes), new FakeProtocoloRepository(protocolos),
            new FakeRelogio(Agora));

    [Fact]
    public async Task NaoPersisteNada_NemProtocoloNemEscreventeNovo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolosFake = new FakeProtocoloRepository([]);
        var escreventesFake = new FakeEscreventeRepository([]);
        var casoDeUso = new SimularProtocoloManual(
            new FakeConferenteRepository([]), new FakeEquipeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([tipo]), escreventesFake, protocolosFake, new FakeRelogio(Agora));

        await casoDeUso.ExecutarAsync("999001", tipo.Id, "Escrevente Novo", Etapa.PosConferencia, Prioridade.Normal);

        Assert.Equal(0, protocolosFake.Quantidade);
        Assert.Equal(0, escreventesFake.Quantidade);
    }

    [Fact]
    public async Task NumeroJaExistente_NumeroDisponivelFalse()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var existente = new Protocolo(Guid.NewGuid(), "999002", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, Agora);
        var casoDeUso = NovoCasoDeUso([], [], [], [tipo], [], [existente]);

        var resultado = await casoDeUso.ExecutarAsync("999002", tipo.Id, "Alguém", Etapa.PosConferencia, Prioridade.Normal);

        Assert.False(resultado.NumeroDisponivel);
    }

    // Mesmo fluxo de continuidade do cadastro manual de verdade — a prévia não pode mentir
    // sobre o destino que ExecutarAsync vai produzir ao confirmar.
    [Fact]
    public async Task NumeroComHistoricoReprovadoNaMesmaEtapa_NumeroDisponivelTrueEDestinoEhOMesmoDono()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var donoAnterior = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var reprovadoAnterior = new Protocolo(Guid.NewGuid(), "999005", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, Agora.AddDays(-1));
        reprovadoAnterior.AtribuirA(donoAnterior.Id, Agora.AddDays(-1));
        reprovadoAnterior.Reprovar(Agora.AddDays(-1));
        var casoDeUso = NovoCasoDeUso([donoAnterior], [], [], [tipo], [], [reprovadoAnterior]);

        var resultado = await casoDeUso.ExecutarAsync("999005", tipo.Id, "Alguém", Etapa.PosConferencia, Prioridade.Normal);

        Assert.True(resultado.NumeroDisponivel);
        Assert.Equal("Atribuido", resultado.Destino);
        Assert.Equal(donoAnterior.Id, resultado.ConferenteId);
    }

    [Fact]
    public async Task EscreventeComEquipe_DevolveEquipeEPrazoDaEquipe()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Ana", equipe.Id);
        // Precisa de pelo menos 1 candidato na escala — o motor manda pra exceção "ninguém com
        // alçada" mesmo sem urgência se não existir NENHUM conferente (passo 4 do motor, seção
        // 4 do requisito, roda antes de perguntar se é urgente).
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([conferente], [equipe], [], [tipo], [escrevente], []);

        var resultado = await casoDeUso.ExecutarAsync("999003", tipo.Id, "Ana", Etapa.PosConferencia, Prioridade.Normal);

        Assert.True(resultado.NumeroDisponivel);
        Assert.Equal("5º andar", resultado.EquipeNome);
        Assert.False(resultado.SemEquipeSinalizado);
        Assert.Equal(TipoPrazo.D1, resultado.Prazo);
        Assert.Equal("EnviadoParaPool", resultado.Destino);
    }

    [Fact]
    public async Task EscreventeSemEquipe_SinalizaEUsaPrazoPadrao()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([], [], [], [tipo], [], []);

        var resultado = await casoDeUso.ExecutarAsync("999004", tipo.Id, "Escrevente Desconhecido", Etapa.PosConferencia, Prioridade.Normal);

        Assert.True(resultado.SemEquipeSinalizado);
        Assert.Null(resultado.EquipeNome);
        Assert.Equal(TipoPrazo.D1, resultado.Prazo);
    }

    // "Hora de entrada": o vencimento previsto na prévia tem que refletir o AndamentoEm
    // informado, não "agora" — senão o modal mostraria um vencimento diferente do que
    // CriarProtocoloManual grava de verdade ao confirmar (RF-18f/RF-38, referência = AndamentoEm).
    [Fact]
    public async Task ComAndamentoEmInformado_VencimentoContaAPartirDele()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([], [], [], [tipo], [], []);
        var andamentoEm = Agora.AddHours(-10);

        var resultado = await casoDeUso.ExecutarAsync(
            "999006", tipo.Id, "Escrevente Desconhecido", Etapa.PosConferencia, Prioridade.Normal, andamentoEm: andamentoEm);

        // Sem equipe = prazo padrão D+1 (24h corridas), contadas a partir do AndamentoEm.
        Assert.Equal(andamentoEm.AddDays(1), resultado.VencimentoEm);
    }
}
