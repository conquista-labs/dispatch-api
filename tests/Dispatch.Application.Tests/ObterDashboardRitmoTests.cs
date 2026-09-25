using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// RF-46a/b (fatia 6 do Dashboard v2): ritmo por pessoa (só os atos que ela concluiu, com o tempo dos
// ciclos dela), ritmo da operação, média da casa, "Seu tempo por tipo de ato", e o tempo de
// referência (RF-46c) vindo de UMA busca de histórico por chamada.
public class ObterDashboardRitmoTests
{
    // 31/08/2026 12:00 UTC. "Mes" = 01/08 00:00 de Brasília até agora; trecho anterior = julho até o dia 31 12:00.
    private static readonly DateTimeOffset Agora = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NoPeriodo = Agora.AddDays(-2);

    private static Usuario NovoUsuario(string nome) => new(Guid.NewGuid(), nome, $"{nome.ToLowerInvariant()}@cartorio.com", "hash", Papel.Conferente);

    private static Conferente NovoConferente(Usuario usuario) => new(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo Concluido(Guid donoId, Guid? tipoAtoId, double minutos, DateTimeOffset? concluidoEm = null)
    {
        var fim = concluidoEm ?? NoPeriodo;
        var protocolo = new Protocolo(Guid.NewGuid(), Guid.NewGuid().ToString("N"), tipoAtoId, Guid.NewGuid(), Etapa.PosConferencia, fim.AddDays(-1));
        protocolo.AtribuirA(donoId, fim.AddHours(-2));
        protocolo.IniciarConferencia(fim - TimeSpan.FromMinutes(minutos));
        protocolo.Aprovar(fim);
        return protocolo;
    }

    private static TipoAto TipoInformado(string nome, int minutos)
    {
        var tipo = new TipoAto(Guid.NewGuid(), nome);
        tipo.DefinirTempoDeReferencia(minutos);
        return tipo;
    }

    private static (ObterDashboard CasoDeUso, FakeProtocoloRepository Protocolos) NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<TipoAto> tipos, IReadOnlyCollection<Usuario> usuarios)
    {
        var repositorio = new FakeProtocoloRepository(protocolos);
        // Configuração padrão das fakes: TempoMedioPorAtoMinutos = 18.
        var casoDeUso = new ObterDashboard(
            repositorio, new FakeConferenteRepository(conferentes), new FakeTipoAtoRepository(tipos),
            new FakeEscreventeRepository([]), new FakeEquipeRepository([]), new FakeUsuarioRepository(usuarios),
            new FakeConfiguracaoRepository(), new FakeRelogio(Agora));
        return (casoDeUso, repositorio);
    }

    // Ana: 2 atos do tipo A (informado 20) com 10 e 20 min → 30/40 = 0,75. Bruno: 1 ato do tipo B (sem
    // histórico, peso 1,00 → estimativa 18) com 36 min → 2,00, e 1 ato SEM tipo de 100 min (fica fora).
    // Julho (trecho anterior): Ana, 1 ato do tipo A com 40 min → 2,00.
    private sealed record Cenario(
        ObterDashboard CasoDeUso, FakeProtocoloRepository Protocolos, Conferente Ana, Conferente Bruno, TipoAto TipoA, TipoAto TipoB);

    private static Cenario AnaEBruno()
    {
        var usuarioAna = NovoUsuario("Ana");
        var usuarioBruno = NovoUsuario("Bruno");
        var ana = NovoConferente(usuarioAna);
        var bruno = NovoConferente(usuarioBruno);
        var tipoA = TipoInformado("Escritura", 20);
        var tipoB = new TipoAto(Guid.NewGuid(), "Procuração");
        var protocolos = new[]
        {
            Concluido(ana.Id, tipoA.Id, 10),
            Concluido(ana.Id, tipoA.Id, 20),
            Concluido(bruno.Id, tipoB.Id, 36),
            Concluido(bruno.Id, null, 100),
            Concluido(ana.Id, tipoA.Id, 40, concluidoEm: new DateTimeOffset(2026, 7, 10, 15, 0, 0, TimeSpan.Zero)),
        };
        var (casoDeUso, repositorio) = NovoCasoDeUso(protocolos, [ana, bruno], [tipoA, tipoB], [usuarioAna, usuarioBruno]);
        return new Cenario(casoDeUso, repositorio, ana, bruno, tipoA, tipoB);
    }

    [Fact]
    public async Task Gestao_RitmoPorPessoa_DaOperacao_EDoTrechoAnterior_ComUmaSoBuscaDeHistorico()
    {
        var cenario = AnaEBruno();

        // Distribuidora sem Administrador: o ritmo aparece mesmo assim (não é avaliação de pessoal).
        var resultado = await cenario.CasoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        var linhaAna = resultado.Desempenho.Single(d => d.ConferenteId == cenario.Ana.Id);
        Assert.Null(linhaAna.Score);
        Assert.Equal(0.75, linhaAna.Ritmo!.Value, 10);
        Assert.Equal(TimeSpan.FromMinutes(20), linhaAna.TempoMedioReferencia);

        var linhaBruno = resultado.Desempenho.Single(d => d.ConferenteId == cenario.Bruno.Id);
        Assert.Equal(2.0, linhaBruno.Ritmo!.Value, 10);
        Assert.Equal(TimeSpan.FromMinutes(18), linhaBruno.TempoMedioReferencia);

        // Operação: (10 + 20 + 36) ÷ (20 + 20 + 18); o ato sem tipo não entra.
        Assert.Equal(66.0 / 58, resultado.Kpis.Ritmo!.Value, 10);
        Assert.Equal(2.0, resultado.KpisAnterior.Ritmo!.Value, 10);
        Assert.Null(resultado.MeuTempoPorTipo);
        Assert.Null(resultado.MediaDaCasa);
        Assert.Equal(1, cenario.Protocolos.ChamadasDeDuracoes);
    }

    [Fact]
    public async Task VisaoRestrita_KpiEhODela_MediaDaCasaEhMediaSimples_EMeuTempoPorTipo()
    {
        var cenario = AnaEBruno();

        var resultado = await cenario.CasoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: cenario.Ana.Id);

        Assert.Equal(0.75, resultado.Kpis.Ritmo!.Value, 10);
        Assert.Equal(2.0, resultado.KpisAnterior.Ritmo!.Value, 10);
        Assert.Equal(0.75, Assert.Single(resultado.Desempenho).Ritmo!.Value, 10);
        Assert.Equal((0.75 + 2.0) / 2, resultado.MediaDaCasa!.Ritmo!.Value, 10);
        Assert.Equal(TimeSpan.FromMinutes(19), resultado.MediaDaCasa.TempoMedioReferencia);

        var porTipo = Assert.Single(resultado.MeuTempoPorTipo!);
        Assert.Equal(new MeuTempoPorTipo(cenario.TipoA.Id, "Escritura", 2, TimeSpan.FromMinutes(15), 20), porTipo);
    }

    [Fact]
    public async Task MeuTempoPorTipo_OrdenaPorVolume_EIgnoraAtoSemTipo()
    {
        var usuario = NovoUsuario("Ana");
        var ana = NovoConferente(usuario);
        var tipoRaro = TipoInformado("Ata notarial", 30);
        var tipoComum = TipoInformado("Procuração", 10);
        var protocolos = new[]
        {
            Concluido(ana.Id, tipoRaro.Id, 30),
            Concluido(ana.Id, tipoComum.Id, 8),
            Concluido(ana.Id, tipoComum.Id, 12),
            Concluido(ana.Id, null, 50),
        };
        var (casoDeUso, _) = NovoCasoDeUso(protocolos, [ana], [tipoRaro, tipoComum], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: ana.Id);

        Assert.Equal([tipoComum.Id, tipoRaro.Id], resultado.MeuTempoPorTipo!.Select(t => t.TipoAtoId));
        Assert.Equal(TimeSpan.FromMinutes(10), resultado.MeuTempoPorTipo![0].MeuTempoMedio);
        // (30 + 8 + 12) ÷ (30 + 10 + 10) = 1,00; os 50 min do ato sem tipo não pesam.
        Assert.Equal(1.0, resultado.Kpis.Ritmo!.Value, 10);
    }

    [Fact]
    public async Task AtoReabertoEReatribuido_ContaSoOCicloDeQuemConcluiu()
    {
        var usuarioAna = NovoUsuario("Ana");
        var usuarioBruno = NovoUsuario("Bruno");
        var ana = NovoConferente(usuarioAna);
        var bruno = NovoConferente(usuarioBruno);
        var tipo = TipoInformado("Inventário", 10);
        var protocolo = new Protocolo(Guid.NewGuid(), "263605", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, NoPeriodo.AddDays(-1));
        protocolo.AtribuirA(ana.Id, NoPeriodo.AddHours(-3));
        protocolo.IniciarConferencia(NoPeriodo.AddHours(-3));
        protocolo.Aprovar(NoPeriodo.AddHours(-3).AddMinutes(20));
        protocolo.ReabrirConferencia(NoPeriodo.AddHours(-1));
        protocolo.AtribuirA(bruno.Id, NoPeriodo.AddHours(-1));
        protocolo.IniciarConferencia(NoPeriodo.AddMinutes(-5));
        protocolo.Aprovar(NoPeriodo);
        var (casoDeUso, _) = NovoCasoDeUso([protocolo], [ana, bruno], [tipo], [usuarioAna, usuarioBruno]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        // Bruno concluiu: só os 5 min dele contra a referência 10. Ana fez 20 min mas não concluiu — tem
        // linha (tempo médio), sem ritmo. A operação soma a duração inteira: 25 ÷ 10.
        Assert.Equal(0.5, resultado.Desempenho.Single(d => d.ConferenteId == bruno.Id).Ritmo!.Value, 10);
        var linhaAna = resultado.Desempenho.Single(d => d.ConferenteId == ana.Id);
        Assert.Equal(0, linhaAna.Volume);
        Assert.Null(linhaAna.Ritmo);
        Assert.Null(linhaAna.TempoMedioReferencia);
        Assert.Equal(2.5, resultado.Kpis.Ritmo!.Value, 10);
    }

    [Fact]
    public async Task Referencia_VemDaMedianaDos12Meses_IgnorandoOQueEhMaisAntigo()
    {
        var usuario = NovoUsuario("Ana");
        var ana = NovoConferente(usuario);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 2.00m); // estimativa 36, descarte > 144
        var outro = NovoConferente(NovoUsuario("Carla"));
        var historico = Enumerable.Range(0, 30).Select(_ => Concluido(outro.Id, tipo.Id, 30, concluidoEm: Agora.AddMonths(-3)));
        // 13 meses atrás: fora da janela. Se entrasse, a mediana cairia pra 15.
        var antigos = Enumerable.Range(0, 30).Select(_ => Concluido(outro.Id, tipo.Id, 5, concluidoEm: Agora.AddMonths(-13)));
        var protocolos = historico.Concat(antigos).Append(Concluido(ana.Id, tipo.Id, 15)).ToList();
        var (casoDeUso, _) = NovoCasoDeUso(protocolos, [ana, outro], [tipo], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: ana.Id);

        Assert.Equal(30, Assert.Single(resultado.MeuTempoPorTipo!).ReferenciaMinutos);
        Assert.Equal(0.5, resultado.Kpis.Ritmo!.Value, 10);
    }

    [Fact]
    public async Task SemAtoElegivel_RitmoNulo()
    {
        var usuario = NovoUsuario("Ana");
        var ana = NovoConferente(usuario);
        var (casoDeUso, repositorio) = NovoCasoDeUso([Concluido(ana.Id, null, 12)], [ana], [], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: ana.Id);

        Assert.Null(resultado.Kpis.Ritmo);
        Assert.Null(Assert.Single(resultado.Desempenho).Ritmo);
        Assert.Null(resultado.MediaDaCasa!.Ritmo);
        Assert.Empty(resultado.MeuTempoPorTipo!);
        // Nenhum tipo em jogo: nem busca o histórico.
        Assert.Equal(0, repositorio.ChamadasDeDuracoes);
    }

    [Fact]
    public async Task ComplexidadeMedia_EhAMediaDosPesosDecimais()
    {
        var usuario = NovoUsuario("Ana");
        var ana = NovoConferente(usuario);
        var leve = new TipoAto(Guid.NewGuid(), "Procuração", pesoComplexidade: 0.75m);
        var pesado = new TipoAto(Guid.NewGuid(), "Inventário", pesoComplexidade: 1.80m);
        var (casoDeUso, _) = NovoCasoDeUso([Concluido(ana.Id, leve.Id, 10), Concluido(ana.Id, pesado.Id, 10)], [ana], [leve, pesado], [usuario]);

        var resultado = await casoDeUso.ExecutarAsync(PeriodoDashboard.Mes, conferenteRestritoId: null);

        Assert.Equal(1.275, Assert.Single(resultado.Desempenho).ComplexidadeMedia, 10);
    }
}
