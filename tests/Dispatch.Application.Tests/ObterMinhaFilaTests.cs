using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterMinhaFilaTests
{
    [Fact]
    public async Task PoolDisponivel_SoTraProtocolosDentroDaAlcada()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var permitido = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var negado = new Protocolo(Guid.NewGuid(), "2", tipo.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([permitido, negado]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([regra]),
            new FakeTipoAtoRepository([tipo]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        var resultado = Assert.Single(fila.PoolDisponivel);
        Assert.Equal(permitido.Id, resultado.Id);
    }

    // ADR-0046: a ordem do pool agora é a da vez (OrdemDoPool) — entre prioridades iguais continua
    // valendo "quem vence antes", sem vencimento por último.
    [Fact]
    public async Task PoolDisponivel_OrdenaPorVencimentoAscendente()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");

        var vencePrimeiro = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        vencePrimeiro.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow);

        var venceDepois = new Protocolo(Guid.NewGuid(), "2", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        venceDepois.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow.AddHours(2));

        var semVencimento = new Protocolo(Guid.NewGuid(), "3", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([venceDepois, semVencimento, vencePrimeiro]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([tipo]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal([vencePrimeiro.Id, venceDepois.Id, semVencimento.Id], fila.PoolDisponivel.Select(p => p.Id));
    }

    // Pedido do dono: "Atribuídas a você" também por vencimento, mesmo critério que o pool já
    // usava (sem ORDER BY a ordem não é garantida).
    [Fact]
    public async Task Atribuidos_OrdenaPorVencimentoAscendente()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

        var vencePrimeiro = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        vencePrimeiro.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        vencePrimeiro.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow);

        var venceDepois = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        venceDepois.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        venceDepois.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow.AddHours(2));

        var semVencimento = new Protocolo(Guid.NewGuid(), "3", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        semVencimento.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([venceDepois, semVencimento, vencePrimeiro]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal([vencePrimeiro.Id, venceDepois.Id, semVencimento.Id], fila.Atribuidos.Select(p => p.Id));
    }

    [Fact]
    public async Task AtribuidosEEmConferencia_SoDoProprioConferente()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var outroConferenteId = Guid.NewGuid();

        var atribuido = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        atribuido.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);

        var emConferencia = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        emConferencia.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        emConferencia.IniciarConferencia(DateTimeOffset.UtcNow);

        var deOutroConferente = new Protocolo(Guid.NewGuid(), "3", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        deOutroConferente.AtribuirA(outroConferenteId, DateTimeOffset.UtcNow);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([atribuido, emConferencia, deOutroConferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal([atribuido.Id], fila.Atribuidos.Select(p => p.Id));
        Assert.Equal([emConferencia.Id], fila.EmConferencia.Select(p => p.Id));
    }

    // RF-24k: o mesmo Número voltando depois de uma linha Reprovada na mesma etapa é a 2ª.
    [Fact]
    public async Task NumeroDaConferencia_ContaReprovadosAnterioresDoMesmoNumero()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var ontem = DateTimeOffset.UtcNow.AddDays(-1);

        var reprovadaAntes = new Protocolo(Guid.NewGuid(), "263546", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, ontem);
        reprovadaAntes.AtribuirA(conferente.Id, ontem);
        reprovadaAntes.IniciarConferencia(ontem);
        reprovadaAntes.Reprovar(ontem);
        var voltou = new Protocolo(Guid.NewGuid(), "263546", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var primeiraVez = new Protocolo(Guid.NewGuid(), "777777", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([reprovadaAntes, voltou, primeiraVez]), new FakeEscreventeRepository([]),
            new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal(2, fila.NumeroDaConferencia[voltou.Id]);
        Assert.Equal(1, fila.NumeroDaConferencia[primeiraVez.Id]);
    }

    // ADR-0046: a leitura usa a mesma ordem da vez que o PegarProtocolo — Alta vence quem vence antes.
    [Fact]
    public async Task PoolDisponivel_PrioridadeAltaNoTopo_EProximoIdEhOPrimeiro()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var agora = DateTimeOffset.UtcNow;
        var normalVencendo = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, agora);
        normalVencendo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), agora);
        var altaSemVencimento = new Protocolo(Guid.NewGuid(), "2", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, agora, Prioridade.Alta);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([normalVencendo, altaSemVencimento]), new FakeEscreventeRepository([]),
            new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]), new FakeConfiguracaoRepository());

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal([altaSemVencimento.Id, normalVencendo.Id], fila.PoolDisponivel.Select(p => p.Id));
        Assert.Equal(new RegraDoPool(OrdemObrigatoria: true, LimiteNaMao: 5, NaMao: 0, ProximoId: altaSemVencimento.Id), fila.RegraDoPool);
    }

    // naMao = atribuídos + em conferência (pausado incluído); na mão cheia, proximoId some.
    [Fact]
    public async Task RegraDoPool_ContaAtribuidosEEmConferencia_ESemProximoNaMaoCheia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var agora = DateTimeOffset.UtcNow;
        var atribuido = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, agora);
        atribuido.AtribuirA(conferente.Id, agora);
        var pausado = new Protocolo(Guid.NewGuid(), "2", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, agora);
        pausado.AtribuirA(conferente.Id, agora);
        pausado.IniciarConferencia(agora);
        pausado.Pausar(agora);
        var noPool = new Protocolo(Guid.NewGuid(), "3", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, agora);
        var configuracao = new FakeConfiguracaoRepository();
        (await configuracao.ObterAsync(default)).DefinirRegraDoPool(limiteDeAtosNaMao: 2, poolEmOrdemObrigatoria: true);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([atribuido, pausado, noPool]), new FakeEscreventeRepository([]),
            new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]), configuracao);

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal(new RegraDoPool(OrdemObrigatoria: true, LimiteNaMao: 2, NaMao: 2, ProximoId: null), fila.RegraDoPool);
        Assert.Equal([noPool.Id], fila.PoolDisponivel.Select(p => p.Id));
    }
}
