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
        TimeSpan? duracao = null, Guid? escreventeId = null, Etapa etapa = Etapa.PosConferencia,
        string? numero = null, DateTimeOffset? andamentoEm = null)
    {
        var inicio = concluidoEm - (duracao ?? TimeSpan.FromMinutes(10));
        // Número único por padrão: "aprovado na 1ª" (RF-24k) olha as outras linhas do mesmo Número,
        // então dois protocolos só compartilham número quando o teste quer uma 2ª rodada.
        var protocolo = new Protocolo(
            Guid.NewGuid(), numero ?? Guid.NewGuid().ToString("N"), tipoAtoId, escreventeId ?? Guid.NewGuid(), etapa,
            andamentoEm ?? DateTimeOffset.UtcNow);
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
        IReadOnlyCollection<Escrevente>? escreventes = null, IReadOnlyCollection<Equipe>? equipes = null,
        DateTimeOffset? agora = null, Configuracao? configuracao = null) =>
        new(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository(conferentes),
            new FakeTipoAtoRepository(tiposAto), new FakeEscreventeRepository(escreventes ?? []),
            new FakeEquipeRepository(equipes ?? []), new FakeUsuarioRepository(usuarios),
            new FakeConfiguracaoRepository(configuracao), new FakeRelogio(agora ?? Agora));

    private static Configuracao ConfiguracaoCom(MetasDoDashboard metas, PesosDoScore pesos)
    {
        var configuracao = new Configuracao(
            Guid.NewGuid(), TimeSpan.FromHours(4), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15),
            30, 18, 5, 8, 0.6, 3, 6, 0.5);
        configuracao.DefinirMetasEPesos(metas, pesos);
        return configuracao;
    }

    // Ana: 2 atos simples (peso 0,50), no prazo e aprovados. Bruno: 1 ato difícil (peso 1,50), estourado e
    // reprovado. Normalizando pelo melhor do grupo: Ana tem volume 1, prazo 1, qualidade 1,
    // complexidade 1/3; Bruno volume 1/2, prazo 0, qualidade 0, complexidade 1.
    private static (ObterDashboard CasoDeUso, Conferente Ana, Conferente Bruno) CenarioAnaEBruno(Configuracao? configuracao = null)
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var ana = NovoConferente(usuarioA.Id);
        var bruno = NovoConferente(usuarioB.Id);
        var simples = new TipoAto(Guid.NewGuid(), "Procuração", pesoComplexidade: 0.50m);
        var dificil = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1.50m);
        var concluidoEm = Agora.AddDays(-1);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(ana.Id, simples.Id, concluidoEm),
            NovoProtocoloConcluido(ana.Id, simples.Id, concluidoEm),
            NovoProtocoloConcluido(bruno.Id, dificil.Id, concluidoEm, aprovado: false, vencimentoEm: concluidoEm.AddHours(-1)),
        };
        return (NovoCasoDeUso(protocolos, [ana, bruno], [simples, dificil], [usuarioA, usuarioB], configuracao: configuracao), ana, bruno);
    }

    [Fact]
    public async Task PesosPadrao_ScoreDe40_30_20_10()
    {
        var (casoDeUso, ana, bruno) = CenarioAnaEBruno();

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        // Ana: 40 + 30 + 20 + 10/3 = 93,3 → 93. Bruno: 20 + 0 + 0 + 10 = 30.
        Assert.Equal(93, resultado.Desempenho.Single(d => d.ConferenteId == ana.Id).Score);
        Assert.Equal(30, resultado.Desempenho.Single(d => d.ConferenteId == bruno.Id).Score);
        Assert.Equal(PesosDoScore.Padrao, resultado.Pesos);
    }

    [Fact]
    public async Task PesosDaConfiguracao_MudamScoreFaixaEParcelas()
    {
        var configuracao = ConfiguracaoCom(MetasDoDashboard.Padrao, new PesosDoScore(10, 20, 30, 40));
        var (casoDeUso, ana, bruno) = CenarioAnaEBruno(configuracao);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        // Ana: 10 + 20 + 30 + 40/3 = 73,3 → 73 (sai da faixa integral). Bruno: 5 + 0 + 0 + 40 = 45.
        var linhaAna = resultado.Desempenho.Single(d => d.ConferenteId == ana.Id);
        Assert.Equal(73, linhaAna.Score);
        Assert.Equal(FaixaBonificacao.Parcial, linhaAna.Faixa);
        Assert.Equal(10, linhaAna.Parcelas!.Volume, precision: 6);
        Assert.Equal(20, linhaAna.Parcelas.Prazo, precision: 6);
        Assert.Equal(30, linhaAna.Parcelas.Qualidade, precision: 6);
        Assert.Equal(40.0 / 3, linhaAna.Parcelas.Complexidade, precision: 6);

        var linhaBruno = resultado.Desempenho.Single(d => d.ConferenteId == bruno.Id);
        Assert.Equal(45, linhaBruno.Score);
        Assert.Equal(5, linhaBruno.Parcelas!.Volume, precision: 6);
        Assert.Equal(40, linhaBruno.Parcelas.Complexidade, precision: 6);
        Assert.Equal(new PesosDoScore(10, 20, 30, 40), resultado.Pesos);
    }

    [Fact]
    public async Task PesoZero_DesligaAParcela()
    {
        var configuracao = ConfiguracaoCom(MetasDoDashboard.Padrao, new PesosDoScore(50, 30, 20, 0));
        var (casoDeUso, _, bruno) = CenarioAnaEBruno(configuracao);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        var linhaBruno = resultado.Desempenho.Single(d => d.ConferenteId == bruno.Id);
        Assert.Equal(0, linhaBruno.Parcelas!.Complexidade);
        Assert.Equal(25, linhaBruno.Score);
    }

    [Fact]
    public async Task GestaoComAdministrador_RecebeMetasEPesosDaConfiguracao()
    {
        var configuracao = ConfiguracaoCom(new MetasDoDashboard(0.80, 0.70), new PesosDoScore(25, 25, 25, 25));
        var (casoDeUso, _, _) = CenarioAnaEBruno(configuracao);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        Assert.Equal(new MetasDoDashboard(0.80, 0.70), resultado.Metas);
        Assert.Equal(new PesosDoScore(25, 25, 25, 25), resultado.Pesos);
    }

    [Fact]
    public async Task GestaoSemAdministrador_RecebeMetasMasNaoPesos()
    {
        // Decisão 4 do dono: a gestão vê a barra de meta. Os pesos só servem pra ler score, que a
        // distribuidora não vê (ADR-0039).
        var configuracao = ConfiguracaoCom(new MetasDoDashboard(0.80, 0.70), PesosDoScore.Padrao);
        var (casoDeUso, _, _) = CenarioAnaEBruno(configuracao);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(new MetasDoDashboard(0.80, 0.70), resultado.Metas);
        Assert.Null(resultado.Pesos);
    }

    [Fact]
    public async Task VisaoRestrita_RecebePesosMasNaoMetas_EScoreComOsPesosDaConfiguracao()
    {
        var configuracao = ConfiguracaoCom(new MetasDoDashboard(0.80, 0.70), new PesosDoScore(10, 20, 30, 40));
        var (casoDeUso, ana, _) = CenarioAnaEBruno(configuracao);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: ana.Id);

        Assert.Null(resultado.Metas);
        Assert.Equal(new PesosDoScore(10, 20, 30, 40), resultado.Pesos);
        var minhaLinha = Assert.Single(resultado.Desempenho);
        Assert.Equal(73, minhaLinha.Score);
        Assert.Equal(10, minhaLinha.Parcelas!.Volume, precision: 6);
    }

    [Fact]
    public async Task UmSoConferenteComVolume_PontuaOMaximoEmVolumeEComplexidade()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 2.50m);
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        var desempenho = Assert.Single(resultado.Desempenho);
        Assert.Equal("Ana", desempenho.Nome);
        Assert.Equal(1, desempenho.Volume);
        Assert.Equal(1.0, desempenho.PercentualAprovado);
        Assert.Equal(2.5, desempenho.ComplexidadeMedia);
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

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

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

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

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
        // Achado em uso real: os KPIs do topo (RF-42) contavam TODO MUNDO (2), não só o próprio
        // conferente (1) — a linha de desempenho logo abaixo já mostrava o número certo, os dois
        // pareciam dados desencontrados na mesma tela.
        Assert.Equal(1, resultado.Kpis.AtosConferidos);
    }

    [Fact]
    public async Task VisaoRestrita_KpisRefletemSoOProprioConferente_NaoOTotalDaOperacao()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);
        // Ana: 1 aprovado. Bruno: 3 reprovados — se o total vazasse pra visão restrita de Ana,
        // ela veria 4 atos conferidos e 25% de aprovação, em vez dos próprios 1 e 100%.
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, tipo.Id, Agora.AddDays(-1), aprovado: true),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1), aprovado: false),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1), aprovado: false),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1), aprovado: false),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [tipo], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: conferenteA.Id);

        Assert.Equal(1, resultado.Kpis.AtosConferidos);
        Assert.Equal(1.0, resultado.Kpis.PercentualAprovado);
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
        // Semana = desde a segunda (calendário de Brasília) — concluído há 10 dias fica de fora.
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-10));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: null);

        Assert.Equal(0, resultado.Kpis.AtosConferidos);
        Assert.Empty(resultado.Desempenho);
    }

    // Achado em uso real (produção, protocolo 263605): reabertura + reatribuição (RF-24c, só
    // acontece quando o dono original saiu da escala, RF-27) não pode fazer o tempo do 1º ciclo
    // (de quem já saiu) entrar na conta do tempo médio de quem terminou. Cada um leva só o que
    // de fato conferiu — mas o protocolo em si (volume/score) continua contando pro dono ATUAL.
    [Fact]
    public async Task ProtocoloReabertoEReatribuido_TempoMedioNaoHerdaDoConferenteAnterior()
    {
        var usuarioAna = NovoUsuario("Ana");
        var usuarioBruno = NovoUsuario("Bruno");
        var conferenteAna = NovoConferente(usuarioAna.Id);
        var conferenteBruno = NovoConferente(usuarioBruno.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");

        // Ana faz o 1º ciclo (20 min) e reprova; o ato é reaberto e, como ela saiu da escala,
        // reatribuído pro Bruno, que faz o 2º ciclo (5 min) e aprova.
        var protocolo = new Protocolo(Guid.NewGuid(), "263605", tipo.Id, Guid.NewGuid(), Etapa.PreConferencia, Agora.AddDays(-1));
        var inicio1 = Agora.AddDays(-1);
        protocolo.AtribuirA(conferenteAna.Id, inicio1);
        protocolo.IniciarConferencia(inicio1);
        protocolo.Reprovar(inicio1.AddMinutes(20));

        protocolo.ReabrirConferencia(inicio1.AddHours(1));
        protocolo.AtribuirA(conferenteBruno.Id, inicio1.AddHours(1));
        var inicio2 = inicio1.AddHours(2);
        protocolo.IniciarConferencia(inicio2);
        protocolo.Aprovar(inicio2.AddMinutes(5));

        var casoDeUso = NovoCasoDeUso([protocolo], [conferenteAna, conferenteBruno], [tipo], [usuarioAna, usuarioBruno]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(2, resultado.Desempenho.Count);
        var bruno = resultado.Desempenho.Single(d => d.Nome == "Bruno");
        var ana = resultado.Desempenho.Single(d => d.Nome == "Ana");

        // Bruno é o dono atual — o protocolo inteiro conta pro volume/score dele, do jeito que
        // já era antes desta mudança.
        Assert.Equal(1, bruno.Volume);
        Assert.Equal(TimeSpan.FromMinutes(5), bruno.TempoMedio);

        // Ana não é dona de nada no período (o protocolo passou pra frente), mas o tempo que ela
        // realmente gastou não pode desaparecer do relatório de produtividade.
        Assert.Equal(0, ana.Volume);
        Assert.Equal(TimeSpan.FromMinutes(20), ana.TempoMedio);

        // Duracao do protocolo em si continua somando os dois ciclos (25 min) — isso não mudou,
        // só quem "leva o crédito" de cada pedaço no Dashboard.
        Assert.Equal(TimeSpan.FromMinutes(25), protocolo.Duracao);
    }

    // Pedido do dono: ajuste manual de duração (distribuidora corrigindo um valor errado) tem
    // que refletir no tempo médio do conferente, não só no card/histórico do protocolo — senão
    // a bonificação continuaria calculada em cima do número que a própria distribuidora julgou
    // errado.
    [Fact]
    public async Task ProtocoloComDuracaoAjustadaManualmente_TempoMedioReflete()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1), duracao: TimeSpan.FromMinutes(10));

        protocolo.AjustarDuracao(TimeSpan.FromMinutes(45), Guid.NewGuid(), Agora, "esqueceu de pausar durante o almoço");

        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        var desempenho = Assert.Single(resultado.Desempenho);
        Assert.Equal(TimeSpan.FromMinutes(45), desempenho.TempoMedio);
    }

    // ADR-0039 / RF-43a — o teste que pega o vazamento: Bruno tem score maior que Ana, então a
    // ordem por score (Bruno, Ana) e a ordem por nome (Ana, Bruno) diferem. Sem a flag de
    // administrador, a distribuidora recebe por nome e sem nível/score/faixa/parcelas.
    [Fact]
    public async Task SemFlagDeAdministrador_OrdenaPorNomeESemAvaliacao()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id, Nivel.Junior);
        var conferenteB = NovoConferente(usuarioB.Id, Nivel.Senior);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, tipo.Id, Agora.AddDays(-1), aprovado: false),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-1)),
            NovoProtocoloConcluido(conferenteB.Id, tipo.Id, Agora.AddDays(-2)),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [tipo], [usuarioA, usuarioB]);

        var comoAdmin = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);
        Assert.Equal(["Bruno", "Ana"], comoAdmin.Desempenho.Select(d => d.Nome));

        var comoDistribuidora = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);
        Assert.Equal(["Ana", "Bruno"], comoDistribuidora.Desempenho.Select(d => d.Nome));
        Assert.All(comoDistribuidora.Desempenho, d =>
        {
            Assert.Null(d.Nivel);
            Assert.Null(d.Score);
            Assert.Null(d.Faixa);
            Assert.Null(d.Parcelas);
        });
        // O que não é avaliação de pessoal continua: volume, prazo, aprovação.
        Assert.Equal([1, 2], comoDistribuidora.Desempenho.Select(d => d.Volume));
    }

    // RF-45 + ADR-0039: o conferente vê o próprio score e as parcelas, mas não o próprio nível.
    [Fact]
    public async Task VisaoRestrita_MantemOProprioScoreMasNaoONivel()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id, Nivel.Senior);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1);
        var casoDeUso = NovoCasoDeUso(
            [NovoProtocoloConcluido(conferente.Id, tipo.Id, Agora.AddDays(-1))], [conferente], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: conferente.Id);

        var meu = Assert.Single(resultado.Desempenho);
        Assert.Null(meu.Nivel);
        Assert.NotNull(meu.Score);
        Assert.NotNull(meu.Parcelas);
    }

    // ---- Dashboard v2: período de calendário, trecho anterior, série, aprovado na 1ª ----

    // Agora = seg 31/08 09h em Brasília. A semana de calendário começou hoje 00:00 local: o domingo
    // de ontem, que a janela móvel de 7 dias contaria, fica de fora.
    [Fact]
    public async Task Semana_EhDeCalendario_DomingoDeOntemNaoEntraNaSemanaQueComecouHoje()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var ontem = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-1));
        var hoje = NovoProtocoloConcluido(conferente.Id, null, Agora.AddHours(-2)); // 07h local
        var casoDeUso = NovoCasoDeUso([ontem, hoje], [conferente], [], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: null);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 3, 0, 0, TimeSpan.Zero), resultado.PeriodoInicio);
        Assert.Equal(Agora, resultado.PeriodoFim);
        Assert.Equal(1, resultado.Kpis.AtosConferidos);
    }

    // Dia 03/09: "Este mês" começa em 01/09 — o 20/08 (dentro da janela móvel de 30 dias) não entra
    // em nada do que o Dashboard calcula, nem no desempenho.
    [Fact]
    public async Task Mes_EhDeCalendario_ConcluidoNoMesPassadoNaoEntraEmNada()
    {
        var agora = new DateTimeOffset(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = NovoProtocoloConcluido(conferente.Id, tipo.Id, new DateTimeOffset(2026, 8, 20, 15, 0, 0, TimeSpan.Zero));
        var casoDeUso = NovoCasoDeUso([protocolo], [conferente], [tipo], [usuario], agora: agora);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero), resultado.PeriodoInicio);
        Assert.Equal(0, resultado.Kpis.AtosConferidos);
        Assert.Empty(resultado.Desempenho);
        Assert.Empty(resultado.PorTipoAto);
        // O trecho anterior é 01/08 00:00 até 03/08 12h local — o 20/08 também fica fora dele.
        Assert.Equal(0, resultado.KpisAnterior.AtosConferidos);
    }

    // RF-42b: seg 31/08 09h local → trecho anterior = seg 24/08 00:00 até 24/08 09h local. O que foi
    // concluído no dia 24 depois das 09h não entra (não é "o mesmo trecho").
    [Fact]
    public async Task KpisAnterior_Semana_ContaSoOMesmoTrechoDaSemanaPassada()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferente.Id, null, new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero), aprovado: false),
            NovoProtocoloConcluido(conferente.Id, null, new DateTimeOffset(2026, 8, 24, 13, 0, 0, TimeSpan.Zero)),
            NovoProtocoloConcluido(conferente.Id, null, new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero)),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferente], [], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: null);

        Assert.Equal(1, resultado.Kpis.AtosConferidos);
        Assert.Equal(1, resultado.KpisAnterior.AtosConferidos);
        Assert.Equal(0.0, resultado.KpisAnterior.PercentualAprovado);
        Assert.Equal(0.0, resultado.KpisAnterior.PercentualAprovadoNaPrimeira);
    }

    [Fact]
    public async Task KpisAnterior_SemNadaNoTrecho_VemZeradoComAprovadoNaPrimeiraNulo()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], []);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(new KpisDashboard(0, 0, 0, null, null), resultado.KpisAnterior);
        Assert.Null(resultado.Kpis.PercentualAprovadoNaPrimeira);
    }

    // RF-45 + RF-42b: o conferente recebe a variação dele, não a da operação.
    [Fact]
    public async Task VisaoRestrita_KpisAnteriorSaoSoOsDoProprioConferente()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var noMesPassado = new DateTimeOffset(2026, 7, 10, 15, 0, 0, TimeSpan.Zero);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, null, noMesPassado, duracao: TimeSpan.FromMinutes(12)),
            NovoProtocoloConcluido(conferenteB.Id, null, noMesPassado, aprovado: false),
            NovoProtocoloConcluido(conferenteB.Id, null, noMesPassado, aprovado: false),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: conferenteA.Id);

        Assert.Equal(1, resultado.KpisAnterior.AtosConferidos);
        Assert.Equal(1.0, resultado.KpisAnterior.PercentualAprovado);
        Assert.Equal(1.0, resultado.KpisAnterior.PercentualAprovadoNaPrimeira);
        Assert.Equal(TimeSpan.FromMinutes(12), resultado.KpisAnterior.TempoMedio);
    }

    // Decisão 3 do dono: das linhas de 1ª rodada (RF-24k) concluídas no período, quantas estão
    // aprovadas. A 2ª rodada do "A" não entra no denominador; o "B" reprovado e corrigido pra
    // aprovado (RF-24a) conta como aprovado na 1ª. 1ª rodada: A1 (reprovado), B1 e C1 (aprovados)
    // → 2/3. "% aprovado" (todas as linhas, resultado atual) continua 3/4 e o score não muda.
    [Fact]
    public async Task AprovadoNaPrimeira_SoContaA1aRodada_ECorrecaoParaAprovadoConta()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var andamento = Agora.AddDays(-5);
        var a1 = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-4), aprovado: false, numero: "A", andamentoEm: andamento);
        var a2 = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-2), numero: "A", andamentoEm: andamento.AddDays(2));
        var b1 = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-3), aprovado: false, numero: "B", andamentoEm: andamento);
        b1.CorrigirResultado(Agora.AddDays(-3).AddMinutes(5));
        var c1 = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-3), numero: "C", andamentoEm: andamento);
        var casoDeUso = NovoCasoDeUso([a1, a2, b1, c1], [conferente], [], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null, incluirAvaliacaoDePessoal: true);

        Assert.Equal(2.0 / 3, resultado.Kpis.PercentualAprovadoNaPrimeira!.Value, precision: 10);
        Assert.Equal(0.75, resultado.Kpis.PercentualAprovado);
        var linha = Assert.Single(resultado.Desempenho);
        Assert.Equal(2.0 / 3, linha.PercentualAprovadoNaPrimeira!.Value, precision: 10);
        Assert.Equal(0.75, linha.PercentualAprovado);
        // Score continua pela aprovação atual: 40 volume + 30 prazo + 20·0,75 + 0 complexidade (sem tipo).
        Assert.Equal(85, linha.Score);
    }

    // Só a 2ª rodada caiu no período (a 1ª foi concluída no mês passado) → nenhuma 1ª conferência:
    // nulo, não 0%.
    [Fact]
    public async Task AprovadoNaPrimeira_SemNenhuma1aConferenciaNoPeriodo_EhNulo()
    {
        var usuario = NovoUsuario("Ana");
        var conferente = NovoConferente(usuario.Id);
        var andamento = new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);
        var primeira = NovoProtocoloConcluido(conferente.Id, null, andamento.AddDays(1), aprovado: false, numero: "X", andamentoEm: andamento);
        var segunda = NovoProtocoloConcluido(conferente.Id, null, Agora.AddDays(-2), numero: "X", andamentoEm: andamento.AddDays(3));
        var casoDeUso = NovoCasoDeUso([primeira, segunda], [conferente], [], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(1, resultado.Kpis.AtosConferidos);
        Assert.Null(resultado.Kpis.PercentualAprovadoNaPrimeira);
        Assert.Null(Assert.Single(resultado.Desempenho).PercentualAprovadoNaPrimeira);
    }

    // RF-45: a média da casa leva o "aprovado na 1ª" — média simples entre quem teve 1ª conferência.
    [Fact]
    public async Task VisaoRestrita_MediaDaCasaTrazAprovadoNaPrimeira()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, null, Agora.AddDays(-1)),
            NovoProtocoloConcluido(conferenteB.Id, null, Agora.AddDays(-1), aprovado: false),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: conferenteA.Id);

        Assert.Equal(1.0, Assert.Single(resultado.Desempenho).PercentualAprovadoNaPrimeira);
        Assert.Equal(0.5, resultado.MediaDaCasa!.PercentualAprovadoNaPrimeira);
        Assert.Equal(1.0, resultado.Kpis.PercentualAprovadoNaPrimeira);
    }

    // RF-42c: estourado = concluído depois do vencimento (o mesmo "no prazo" dos KPIs).
    [Fact]
    public async Task Serie_Gestao_ContaTodosESeparaOsEstourados()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var sexta28 = new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, null, sexta28, vencimentoEm: sexta28.AddHours(1)),
            NovoProtocoloConcluido(conferenteB.Id, null, sexta28, vencimentoEm: sexta28.AddHours(-1)),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(GranularidadeSerie.Dia, resultado.Serie.Granularidade);
        // Agosto/2026: 21 dias úteis, todos já passados (hoje é 31/08, segunda).
        Assert.Equal(21, resultado.Serie.Pontos.Count);
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 8, 28), 2, 1, Futuro: false), resultado.Serie.Pontos.Single(p => p.Inicio.Day == 28));
        Assert.Equal(0.5, resultado.Kpis.PercentualNoPrazo);
    }

    [Fact]
    public async Task Serie_VisaoRestrita_SoDoProprioConferente()
    {
        var usuarioA = NovoUsuario("Ana");
        var usuarioB = NovoUsuario("Bruno");
        var conferenteA = NovoConferente(usuarioA.Id);
        var conferenteB = NovoConferente(usuarioB.Id);
        var protocolos = new[]
        {
            NovoProtocoloConcluido(conferenteA.Id, null, Agora.AddHours(-1)),
            NovoProtocoloConcluido(conferenteB.Id, null, Agora.AddHours(-1)),
            NovoProtocoloConcluido(conferenteB.Id, null, Agora.AddHours(-1)),
        };
        var casoDeUso = NovoCasoDeUso(protocolos, [conferenteA, conferenteB], [], [usuarioA, usuarioB]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Semana, conferenteRestritoId: conferenteA.Id);

        Assert.Equal(1, resultado.Serie.Pontos.Sum(p => p.Conferidos));
        Assert.Equal(new DateOnly(2026, 8, 31), resultado.Serie.Pontos[0].Inicio);
        Assert.All(resultado.Serie.Pontos.Skip(1), p => Assert.True(p.Futuro));
    }

    [Fact]
    public async Task Serie_Trimestre_VemPorSemana()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], []);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Trimestre, conferenteRestritoId: null);

        Assert.Equal(GranularidadeSerie.Semana, resultado.Serie.Granularidade);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 3, 0, 0, TimeSpan.Zero), resultado.PeriodoInicio);
        Assert.Equal(14, resultado.Serie.Pontos.Count);
    }
}
