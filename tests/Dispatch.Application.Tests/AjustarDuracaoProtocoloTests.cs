using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AjustarDuracaoProtocoloTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inicio = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);

    private static Protocolo NovoProtocoloConcluido()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), Inicio);
        protocolo.IniciarConferencia(Inicio);
        protocolo.Aprovar(Inicio.AddMinutes(10));
        return protocolo;
    }

    [Fact]
    public async Task ProtocoloConcluido_Ajusta()
    {
        var protocolo = NovoProtocoloConcluido();
        var ajustadoPorId = Guid.NewGuid();
        var casoDeUso = new AjustarDuracaoProtocolo(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, TimeSpan.FromMinutes(30), ajustadoPorId, "esqueceu de pausar");

        Assert.Equal(ResultadoAjustarDuracaoProtocolo.Sucesso, resultado);
        Assert.Equal(TimeSpan.FromMinutes(30), protocolo.Duracao);
        var ajuste = Assert.Single(protocolo.AjustesDeDuracao);
        Assert.Equal(ajustadoPorId, ajuste.AjustadoPorId);
        Assert.Equal(Agora, ajuste.AjustadoEm);
        Assert.Equal(TimeSpan.FromMinutes(10), ajuste.DuracaoAnterior);
        Assert.Equal("esqueceu de pausar", ajuste.Motivo);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var casoDeUso = new AjustarDuracaoProtocolo(new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), TimeSpan.FromMinutes(30), Guid.NewGuid(), null);

        Assert.Equal(ResultadoAjustarDuracaoProtocolo.NaoEncontrado, resultado);
    }

    [Theory]
    [InlineData(StatusProtocolo.Pool)]
    [InlineData(StatusProtocolo.Atribuido)]
    [InlineData(StatusProtocolo.Conferindo)]
    [InlineData(StatusProtocolo.Excecao)]
    public async Task ProtocoloAindaNaoConcluido_RetornaStatusInvalido(StatusProtocolo status)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        switch (status)
        {
            case StatusProtocolo.Atribuido:
                protocolo.AtribuirA(Guid.NewGuid(), Inicio);
                break;
            case StatusProtocolo.Conferindo:
                protocolo.AtribuirA(Guid.NewGuid(), Inicio);
                protocolo.IniciarConferencia(Inicio);
                break;
            case StatusProtocolo.Excecao:
                protocolo.MarcarExcecao("sem alçada");
                break;
        }

        var casoDeUso = new AjustarDuracaoProtocolo(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, TimeSpan.FromMinutes(30), Guid.NewGuid(), null);

        Assert.Equal(ResultadoAjustarDuracaoProtocolo.StatusInvalido, resultado);
    }

    [Fact]
    public async Task DuracaoNegativa_RetornaDuracaoInvalida()
    {
        var protocolo = NovoProtocoloConcluido();
        var casoDeUso = new AjustarDuracaoProtocolo(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, TimeSpan.FromMinutes(-1), Guid.NewGuid(), null);

        Assert.Equal(ResultadoAjustarDuracaoProtocolo.DuracaoInvalida, resultado);
        Assert.Empty(protocolo.AjustesDeDuracao);
    }
}
