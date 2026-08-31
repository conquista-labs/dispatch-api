using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class GerarSugestoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static List<Protocolo> CincoProtocolosTipoDesconhecido(Conferente dono)
    {
        var lista = new List<Protocolo>();
        for (var i = 0; i < 5; i++)
        {
            var protocolo = new Protocolo(
                Guid.NewGuid(), $"{i}", tipoAtoId: null, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow,
                tipoAtoNomeOriginal: "ARROLAMENTO");
            protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
            lista.Add(protocolo);
        }

        return lista;
    }

    [Fact]
    public async Task PrimeiraRodada_CriaSugestaoNova()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
        var protocolos = CincoProtocolosTipoDesconhecido(dono);

        var sugestaoRepo = new FakeSugestaoRepository([]);
        var casoDeUso = new GerarSugestoes(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository([dono]), new FakeEscreventeRepository([]),
            sugestaoRepo, new FakeUnitOfWork(), new FakeRelogio(Agora));

        var novas = await casoDeUso.ExecutarAsync();

        Assert.Equal(1, novas);
        Assert.Equal(1, sugestaoRepo.Quantidade);
        var pendente = Assert.Single(await sugestaoRepo.ObterPendentesAsync(CancellationToken.None));
        // Todos os 5 protocolos são do mesmo dono (Pleno) — força da moda = 100%.
        Assert.Equal(1.0, pendente.IndiceConfianca, precision: 10);
    }

    [Fact]
    public async Task RodadaSeguinteComMesmaChaveAindaPendente_AtualizaEmVezDeDuplicar()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
        var protocolos = CincoProtocolosTipoDesconhecido(dono);
        var repoProtocolos = new FakeProtocoloRepository(protocolos);
        var sugestaoRepo = new FakeSugestaoRepository([]);
        var relogio = new FakeRelogio(Agora);
        var casoDeUso = new GerarSugestoes(
            repoProtocolos, new FakeConferenteRepository([dono]), new FakeEscreventeRepository([]),
            sugestaoRepo, new FakeUnitOfWork(), relogio);

        await casoDeUso.ExecutarAsync();

        // Mais uma ocorrência apareceu antes da próxima rodada.
        var protocoloNovo = new Protocolo(
            Guid.NewGuid(), "novo", tipoAtoId: null, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow,
            tipoAtoNomeOriginal: "ARROLAMENTO");
        protocoloNovo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        repoProtocolos.Adicionar(protocoloNovo);

        var novasSegundaRodada = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, novasSegundaRodada);
        Assert.Equal(1, sugestaoRepo.Quantidade);
        var pendente = Assert.Single(await sugestaoRepo.ObterPendentesAsync(CancellationToken.None));
        Assert.Equal(6, pendente.Ocorrencias);
        // AtualizarEvidenciaAsync recalcula o índice junto — continua 100% (mesmo dono Pleno).
        Assert.Equal(1.0, pendente.IndiceConfianca, precision: 10);
    }

    [Fact]
    public async Task ChaveDescartadaDentroDaJanelaDeMemoria_NaoReaparece()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
        var protocolos = CincoProtocolosTipoDesconhecido(dono);
        var sugestaoExistente = new Sugestao(
            Guid.NewGuid(), "tipo-desconhecido:ARROLAMENTO",
            new PayloadSugestao.TipoDesconhecido("ARROLAMENTO", Nivel.Pleno), "evidência antiga", 5, 0.8, Agora.AddDays(-1));
        sugestaoExistente.Descartar(Agora.AddDays(-1), Agora.AddDays(10));

        var sugestaoRepo = new FakeSugestaoRepository([sugestaoExistente]);
        var casoDeUso = new GerarSugestoes(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository([dono]), new FakeEscreventeRepository([]),
            sugestaoRepo, new FakeUnitOfWork(), new FakeRelogio(Agora));

        var novas = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, novas);
        Assert.Empty(await sugestaoRepo.ObterPendentesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ChaveDescartadaComJanelaExpirada_ReaparecComoSugestaoNova()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
        var protocolos = CincoProtocolosTipoDesconhecido(dono);
        var sugestaoExistente = new Sugestao(
            Guid.NewGuid(), "tipo-desconhecido:ARROLAMENTO",
            new PayloadSugestao.TipoDesconhecido("ARROLAMENTO", Nivel.Pleno), "evidência antiga", 5, 0.8, Agora.AddDays(-40));
        sugestaoExistente.Descartar(Agora.AddDays(-40), Agora.AddDays(-10));

        var sugestaoRepo = new FakeSugestaoRepository([sugestaoExistente]);
        var casoDeUso = new GerarSugestoes(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository([dono]), new FakeEscreventeRepository([]),
            sugestaoRepo, new FakeUnitOfWork(), new FakeRelogio(Agora));

        var novas = await casoDeUso.ExecutarAsync();

        Assert.Equal(1, novas);
        Assert.Equal(2, sugestaoRepo.Quantidade);
    }
}
