using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterDashboardTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static Usuario NovoUsuario(string nome) => new(Guid.NewGuid(), nome, $"{nome.ToLowerInvariant()}@cartorio.com", "hash", Papel.Conferente);

    private static Conferente NovoConferente(Guid usuarioId, Nivel nivel = Nivel.Pleno) =>
        new(Guid.NewGuid(), usuarioId, nivel, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo NovoProtocoloConcluido(
        Guid donoId, Guid? tipoAtoId, DateTimeOffset concluidoEm, bool aprovado = true, DateTimeOffset? vencimentoEm = null,
        TimeSpan? duracao = null, Guid? escreventeId = null, Etapa etapa = Etapa.PosConferencia)
    {
        var inicio = concluidoEm - (duracao ?? TimeSpan.FromMinutes(10));
        var protocolo = new Protocolo(Guid.NewGuid(), "123", tipoAtoId, escreventeId ?? Guid.NewGuid(), etapa, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(donoId, DateTimeOffset.UtcNow);
        if (vencimentoEm is { } vencimento)
        {
            // TipoPrazo.UmaHora de propósito — é o único que não sofre o ajuste de "próximo dia
            // útil" (Prazo.cs), então o vencimento sai exatamente na hora esperada pelo teste.
            protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), vencimento.AddHours(-1));
        }

        protocolo.IniciarConferencia(inicio);
        if (aprovado)
        {
            protocolo.Aprovar(concluidoEm);
        }
        else
        {
            protocolo.Reprovar(concluidoEm);
        }

        return protocolo;
    }

    private static ObterDashboard NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<TipoAto> tiposAto, IReadOnlyCollection<Usuario> usuarios,
        IReadOnlyCollection<Escrevente>? escreventes = null, IReadOnlyCollection<Equipe>? equipes = null) =>
        new(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository(conferentes),
            new FakeTipoAtoRepository(tiposAto), new FakeEscreventeRepository(escreventes ?? []),
            new FakeEquipeRepository(equipes ?? []), new FakeUsuarioRepository(usuarios), new FakeRelogio(Agora));

    [Fact]
    public async Task UmSoConferenteComVolume_PontuaOMaximoEmVolumeEComplexidade()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 3);
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        var desempenho = Assert.Single(resultado.Desempenho);
        Assert.Equal("Ana", desempenho.Nome);
        Assert.Equal(1, desempenho.Volume);
        Assert.Equal(1.0, desempenho.PercentualAprovado);
        Assert.Equal(3, desempenho.ComplexidadeMedia);
        // Sozinho no grupo: é o próprio máximo em volume e complexidade → pontuação cheia nas
        // duas parcelas (40 + 10), mais prazo (sem vencimento definido = considerado no prazo,
        // 30) e qualidade (aprovado, 20) = 100.
        Assert.Equal(100, desempenho.Score);
        Assert.Equal(FaixaBonificacao.Integral, desempenho.Faixa);
    }

    [Fact]
    public async Task DoisConferentes_VolumeNormalizadoPeloMaximoDoGrupo()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);

        // Ana: 4 protocolos. Bruno: 2 protocolos (metade do volume de Ana).
        var protocolos = Enumerable.Range(0, 4).Select(_ => NovoProtocoloConcluido(conferenteA.Id, tipo.Id, Agora.AddDays(-1)))
            .Concat(Enumerable.Range(0, 2).Select(_ => NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1))))
            .ToList();
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [tipo], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        var ana = resultado.Desempenho.Single(d => d.Nome == "Ana");
        var bruno = resultado.Desempenho.Single(d => d.Nome == "Bruno");
        Assert.Equal(40, ana.Parcelas!.Volume);
        Assert.Equal(20, bruno.Parcelas!.Volume);
    }

    [Fact]
    public async Task ScoreAbaixoDoLimiarParcial_FaixaFora()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);
        // Reprovado (sem pontos de qualidade) e fora do prazo (sem pontos de prazo) — sobra só
        // volume (40, sozinho no grupo) + complexidade (10, sozinho no grupo) = 50.
        var protocolo = NovoProtocoloConcluido(
            conferente.Id, tipo.Id, Agora.AddDays(-1), aprovado: false, vencimentoEm: Agora.AddDays(-2));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        var desempenho = Assert.Single(resultado.Desempenho);
        Assert.Equal(50, desempenho.Score);
        Assert.Equal(FaixaBonificacao.Fora, desempenho.Faixa);
    }

    [Fact]
    public async Task VisaoRestrita_SoMostraOProprioDesempenhoESemFaixa()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, tipo.Id, Agora.AddDays(-1)),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1)),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [tipo], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: conferenteA.Id);

        var meu = Assert.Single(resultado.Desempenho);
        Assert.Equal("Ana", meu.Nome);
        Assert.Null(meu.Faixa);
        Assert.NotNull(meu.Parcelas);
        Assert.NotNull(resultado.MediaDaCasa);
        Assert.Null(resultado.MediaDaCasa!.Nome);
        Assert.Null(resultado.MediaDaCasa.Faixa);
        Assert.Empty(resultado.PorTipoAto);
    }

    [Fact]
    public async Task SemNenhumConcluidoNoPeriodo_NaoQuebraENaoDaScoreNaN()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], []);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: null);

        Assert.Equal(0, resultado.Kpis.AtosConferidos);
        Assert.Empty(resultado.Desempenho);
    }

    [Fact]
    public async Task CumprimentoPrazoEquipe_AgrupaPorEquipeEEtapa_PiorPercentualPrimeiro()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipeBoa = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.UmaHora));
        var equipeRuim = new Equipe(Guid.NewGuid(), "Balcão", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.UmaHora));
        var escreventeBom = new Escrevente(Guid.NewGuid(), "Bruno", equipeBoa.Id);
        var escreventeRuim = new Escrevente(Guid.NewGuid(), "Carla", equipeRuim.Id);
        var escreventeOrfao = new Escrevente(Guid.NewGuid(), "Duda", equipeId: null);

        var protocolos = new List<Protocolo>
        {
            // equipeBoa · Pós: 1 no prazo, 1 fora do prazo → 50%.
            NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), vencimentoEm: Agora.AddDays(-1).AddHours(1), escreventeId: escreventeBom.Id, etapa: Etapa.PosConferencia),
            NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), vencimentoEm: Agora.AddDays(-2), escreventeId: escreventeBom.Id, etapa: Etapa.PosConferencia),
            // equipeRuim · Pós: os 2 fora do prazo → 0%.
            NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), vencimentoEm: Agora.AddDays(-2), escreventeId: escreventeRuim.Id, etapa: Etapa.PosConferencia),
            NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), vencimentoEm: Agora.AddDays(-2), escreventeId: escreventeRuim.Id, etapa: Etapa.PosConferencia),
            // sem equipe · Pré: 1 no prazo → 100%.
            NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), vencimentoEm: Agora.AddDays(-1).AddHours(1), escreventeId: escreventeOrfao.Id, etapa: Etapa.PreConferencia),
        };
        var casoDeUso = NovoCasoDeUso(
            protocolos, [conferente], [tipo], [usuario],
            [escreventeBom, escreventeRuim, escreventeOrfao], [equipeBoa, equipeRuim]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(3, resultado.CumprimentoPrazoEquipe.Count);
        // Pior primeiro: Balcão (0%), depois 5º andar (50%), depois sem equipe (100%).
        Assert.Equal(["Balcão", "5º andar", "sem equipe"], resultado.CumprimentoPrazoEquipe.Select(c => c.EquipeNome));
        var balcao = resultado.CumprimentoPrazoEquipe[0];
        Assert.Equal(equipeRuim.Id, balcao.EquipeId);
        Assert.Equal(Etapa.PosConferencia, balcao.Etapa);
        Assert.Equal(2, balcao.Total);
        Assert.Equal(0.0, balcao.PercentualNoPrazo);
        var semEquipe = resultado.CumprimentoPrazoEquipe[2];
        Assert.Null(semEquipe.EquipeId);
        Assert.Equal(Etapa.PreConferencia, semEquipe.Etapa);
        Assert.Equal(1.0, semEquipe.PercentualNoPrazo);
    }

    [Fact]
    public async Task ProtocoloForaDoPeriodo_NaoEntraNoCalculo()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        // Semana = últimos 7 dias — concluído há 10 dias fica de fora.
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-10));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: null);

        Assert.Equal(0, resultado.Kpis.AtosConferidos);
        Assert.Empty(resultado.Desempenho);
    }
}
