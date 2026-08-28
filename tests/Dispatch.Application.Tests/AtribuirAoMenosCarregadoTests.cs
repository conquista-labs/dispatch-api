using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AtribuirAoMenosCarregadoTests
{
    [Fact]
    public async Task ProtocoloNoPool_AtribuiAoElegivelDeMenorCarga()
    {
        var tipoId = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipoId, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var maisCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 5);
        var menosCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var casoDeUso = new AtribuirAoMenosCarregado(
            new FakeProtocoloRepository([protocolo]),
            new FakeConferenteRepository([maisCarregado, menosCarregado]),
            new FakeRegraAlcadaRepository([]),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.Equal(ResultadoAtribuirAoMenosCarregado.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(menosCarregado.Id, protocolo.DonoId);
    }

    [Fact]
    public async Task NinguemComAlcada_Rejeita()
    {
        var tipoId = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipoId, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.MarcarExcecao("ninguém com alçada");
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var regraNega = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipoId));
        var casoDeUso = new AtribuirAoMenosCarregado(
            new FakeProtocoloRepository([protocolo]),
            new FakeConferenteRepository([conferente]),
            new FakeRegraAlcadaRepository([regraNega]),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.Equal(ResultadoAtribuirAoMenosCarregado.NinguemComAlcada, resultado);
        Assert.Equal(StatusProtocolo.Excecao, protocolo.Status);
    }

    [Fact]
    public async Task ProtocoloJaAtribuido_Rejeita()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var casoDeUso = new AtribuirAoMenosCarregado(
            new FakeProtocoloRepository([protocolo]),
            new FakeConferenteRepository([]),
            new FakeRegraAlcadaRepository([]),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.Equal(ResultadoAtribuirAoMenosCarregado.ProtocoloNaoElegivel, resultado);
    }
}
