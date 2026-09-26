using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class PegarProtocoloTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static Conferente NovoConferente(Nivel nivel = Nivel.Pleno) =>
        new(Guid.NewGuid(), Guid.NewGuid(), nivel, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo NovoProtocolo(
        TipoAto tipo, string numero = "1", Prioridade prioridade = Prioridade.Normal, int? venceEmHoras = null)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), numero, tipo.Id, Guid.NewGuid(), Etapa.PreConferencia, Agora, prioridade);
        if (venceEmHoras is { } horas)
        {
            protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), Agora.AddHours(horas - 1));
        }

        return protocolo;
    }

    private static FakeConfiguracaoRepository Configuracao(int limiteNaMao = 5, bool ordemObrigatoria = true)
    {
        var configuracao = new FakeConfiguracaoRepository();
        configuracao.ObterAsync(default).Result.DefinirRegraDoPool(limiteNaMao, ordemObrigatoria);
        return configuracao;
    }

    private static PegarProtocolo NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<TipoAto> tipos,
        IReadOnlyCollection<RegraAlcada>? regras = null, FakeConfiguracaoRepository? configuracao = null) =>
        new(new FakeProtocoloRepository(protocolos), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository(regras ?? []),
            new FakeTipoAtoRepository(tipos), configuracao ?? Configuracao(), new FakeUnitOfWork(), new FakeRelogio(Agora));

    [Fact]
    public async Task ProtocoloNoPoolDentroDaAlcada_Atribui()
    {
        var conferente = NovoConferente();
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = NovoProtocolo(tipo);

        var resultado = await NovoCasoDeUso([protocolo], [tipo]).ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var resultado = await NovoCasoDeUso([], []).ExecutarAsync(Guid.NewGuid(), NovoConferente());

        Assert.IsType<ResultadoPegarProtocolo.NaoEncontrado>(resultado);
    }

    [Fact]
    public async Task ProtocoloForaDoPool_RetornaNaoEstaNoPool()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = NovoProtocolo(tipo);
        protocolo.AtribuirA(Guid.NewGuid(), Agora);

        var resultado = await NovoCasoDeUso([protocolo], [tipo]).ExecutarAsync(protocolo.Id, NovoConferente());

        Assert.IsType<ResultadoPegarProtocolo.NaoEstaNoPool>(resultado);
    }

    [Fact]
    public async Task SemAlcadaPraEtapa_RetornaSemAlcada()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = NovoProtocolo(tipo);
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        var resultado = await NovoCasoDeUso([protocolo], [tipo], [regra]).ExecutarAsync(protocolo.Id, NovoConferente(Nivel.Junior));

        Assert.IsType<ResultadoPegarProtocolo.SemAlcada>(resultado);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
    }

    [Fact]
    public async Task TipoDesconhecido_RetornaSemAlcada()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipoAtoId: null, Guid.NewGuid(), Etapa.PreConferencia, Agora);

        var resultado = await NovoCasoDeUso([protocolo], []).ExecutarAsync(protocolo.Id, NovoConferente());

        Assert.IsType<ResultadoPegarProtocolo.SemAlcada>(resultado);
    }

    // ADR-0046 — daqui pra baixo, a regra do pool.

    [Fact]
    public async Task ForaDaVez_ComAChaveLigada_Recusa()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var primeiro = NovoProtocolo(tipo, "1", venceEmHoras: 1);
        var segundo = NovoProtocolo(tipo, "2", venceEmHoras: 5);

        var resultado = await NovoCasoDeUso([segundo, primeiro], [tipo]).ExecutarAsync(segundo.Id, NovoConferente());

        Assert.IsType<ResultadoPegarProtocolo.ForaDaVez>(resultado);
        Assert.Equal(StatusProtocolo.Pool, segundo.Status);
    }

    [Fact]
    public async Task PrioridadeAlta_EhAVez_MesmoVencendoDepois()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var normalVencendo = NovoProtocolo(tipo, "1", venceEmHoras: 1);
        var alta = NovoProtocolo(tipo, "2", Prioridade.Alta, venceEmHoras: 48);
        var casoDeUso = NovoCasoDeUso([normalVencendo, alta], [tipo]);
        var conferente = NovoConferente();

        Assert.IsType<ResultadoPegarProtocolo.ForaDaVez>(await casoDeUso.ExecutarAsync(normalVencendo.Id, conferente));
        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(await casoDeUso.ExecutarAsync(alta.Id, conferente));
    }

    // "A vez" é do pool DELE: o que está antes na fila mas fora da alçada dele não conta.
    [Fact]
    public async Task PrimeiroDoPoolForaDaAlcada_NaoTiraAVezDoConferente()
    {
        var permitido = new TipoAto(Guid.NewGuid(), "Escritura");
        var proibido = new TipoAto(Guid.NewGuid(), "Testamento");
        var primeiroGeralSemAlcada = NovoProtocolo(proibido, "1", Prioridade.Alta, venceEmHoras: 1);
        var primeiroDele = NovoProtocolo(permitido, "2", venceEmHoras: 5);
        var conferente = NovoConferente();
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferente.Id), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(proibido.Id));

        var resultado = await NovoCasoDeUso([primeiroGeralSemAlcada, primeiroDele], [permitido, proibido], [regra])
            .ExecutarAsync(primeiroDele.Id, conferente);

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(resultado);
    }

    [Fact]
    public async Task ForaDaVez_ComAChaveDesligada_Atribui()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var primeiro = NovoProtocolo(tipo, "1", venceEmHoras: 1);
        var segundo = NovoProtocolo(tipo, "2", venceEmHoras: 5);

        var resultado = await NovoCasoDeUso([primeiro, segundo], [tipo], configuracao: Configuracao(ordemObrigatoria: false))
            .ExecutarAsync(segundo.Id, NovoConferente());

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Atribuido, segundo.Status);
    }

    [Fact]
    public async Task MaoCheia_ContandoAtribuidosEEmConferencia_RecusaComOsNumeros()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = NovoConferente();
        var atribuido = NovoProtocolo(tipo, "10");
        atribuido.AtribuirA(conferente.Id, Agora);
        var emConferencia = NovoProtocolo(tipo, "11");
        emConferencia.AtribuirA(conferente.Id, Agora);
        emConferencia.IniciarConferencia(Agora);
        var noPool = NovoProtocolo(tipo, "1");

        var resultado = await NovoCasoDeUso([atribuido, emConferencia, noPool], [tipo], configuracao: Configuracao(limiteNaMao: 2))
            .ExecutarAsync(noPool.Id, conferente);

        Assert.Equal(new ResultadoPegarProtocolo.LimiteNaMao(NaMao: 2, Limite: 2), resultado);
        Assert.Equal(StatusProtocolo.Pool, noPool.Status);
    }

    // O limite vale com a chave desligada também — ele não depende da ordem.
    [Fact]
    public async Task MaoCheia_ComAChaveDesligada_TambemRecusa()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = NovoConferente();
        var atribuido = NovoProtocolo(tipo, "10");
        atribuido.AtribuirA(conferente.Id, Agora);
        var noPool = NovoProtocolo(tipo, "1");

        var resultado = await NovoCasoDeUso([atribuido, noPool], [tipo], configuracao: Configuracao(limiteNaMao: 1, ordemObrigatoria: false))
            .ExecutarAsync(noPool.Id, conferente);

        Assert.IsType<ResultadoPegarProtocolo.LimiteNaMao>(resultado);
    }

    [Fact]
    public async Task UmAbaixoDoLimite_AindaPega()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = NovoConferente();
        var atribuido = NovoProtocolo(tipo, "10");
        atribuido.AtribuirA(conferente.Id, Agora);
        var noPool = NovoProtocolo(tipo, "1");

        var resultado = await NovoCasoDeUso([atribuido, noPool], [tipo], configuracao: Configuracao(limiteNaMao: 2))
            .ExecutarAsync(noPool.Id, conferente);

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(resultado);
    }

    // Só conta o que é DELE e não terminou — concluídos e os de outro conferente não enchem a mão.
    [Fact]
    public async Task ConcluidosEDeOutroConferente_NaoContamNaMao()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = NovoConferente();
        var concluido = NovoProtocolo(tipo, "10");
        concluido.AtribuirA(conferente.Id, Agora);
        concluido.IniciarConferencia(Agora);
        concluido.Aprovar(Agora);
        var deOutro = NovoProtocolo(tipo, "11");
        deOutro.AtribuirA(Guid.NewGuid(), Agora);
        var noPool = NovoProtocolo(tipo, "1");

        var resultado = await NovoCasoDeUso([concluido, deOutro, noPool], [tipo], configuracao: Configuracao(limiteNaMao: 1))
            .ExecutarAsync(noPool.Id, conferente);

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(resultado);
    }

    // Corrida pelo mesmo "primeiro da vez": o segundo recebe o "não está no pool" de sempre, e o
    // próximo da vez dele passa a ser o seguinte.
    [Fact]
    public async Task DoisConferentesPeloMesmoPrimeiro_OSegundoRecebeNaoEstaNoPool()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var primeiro = NovoProtocolo(tipo, "1", venceEmHoras: 1);
        var segundo = NovoProtocolo(tipo, "2", venceEmHoras: 5);
        var casoDeUso = NovoCasoDeUso([primeiro, segundo], [tipo]);
        var ana = NovoConferente();
        var bruno = NovoConferente();

        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(await casoDeUso.ExecutarAsync(primeiro.Id, ana));
        Assert.IsType<ResultadoPegarProtocolo.NaoEstaNoPool>(await casoDeUso.ExecutarAsync(primeiro.Id, bruno));
        Assert.IsType<ResultadoPegarProtocolo.Sucesso>(await casoDeUso.ExecutarAsync(segundo.Id, bruno));
        Assert.Equal(ana.Id, primeiro.DonoId);
        Assert.Equal(bruno.Id, segundo.DonoId);
    }
}
