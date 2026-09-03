namespace Dispatch.Domain.Tests;

public class ResolvedorDeContinuidadeTests
{
    private static Protocolo NovoProtocoloComDono(Etapa etapa, DateTimeOffset andamentoEm, Guid donoId)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), etapa, andamentoEm);
        protocolo.AtribuirA(donoId, DateTimeOffset.UtcNow);
        return protocolo;
    }

    [Fact]
    public void DuasEntradasNaMesmaEtapaComDono_DevolveODonoDaMaisAntiga()
    {
        var donoAntigo = Guid.NewGuid();
        var donoRecente = Guid.NewGuid();
        var historico = new[]
        {
            NovoProtocoloComDono(Etapa.PosConferencia, new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero), donoAntigo),
            NovoProtocoloComDono(Etapa.PosConferencia, new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero), donoRecente),
        };

        var dono = ResolvedorDeContinuidade.Resolver(historico, Etapa.PosConferencia);

        Assert.Equal(donoAntigo, dono);
    }

    [Fact]
    public void EntradaDeOutraEtapa_Ignora()
    {
        var historico = new[]
        {
            NovoProtocoloComDono(Etapa.PreConferencia, DateTimeOffset.UtcNow, Guid.NewGuid()),
        };

        var dono = ResolvedorDeContinuidade.Resolver(historico, Etapa.PosConferencia);

        Assert.Null(dono);
    }

    [Fact]
    public void EntradaSemDono_Ignora()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

        var dono = ResolvedorDeContinuidade.Resolver([protocolo], Etapa.PosConferencia);

        Assert.Null(dono);
    }

    [Fact]
    public void HistoricoVazio_DevolveNulo()
    {
        var dono = ResolvedorDeContinuidade.Resolver([], Etapa.PosConferencia);

        Assert.Null(dono);
    }

    private static Protocolo NovoProtocoloComStatus(StatusProtocolo status)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        switch (status)
        {
            case StatusProtocolo.Pool:
                break;
            case StatusProtocolo.Atribuido:
                protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
                break;
            case StatusProtocolo.Conferindo:
                protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
                protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
                break;
            case StatusProtocolo.Aprovado:
                protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
                protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
                protocolo.Aprovar(DateTimeOffset.UtcNow);
                break;
            case StatusProtocolo.Reprovado:
                protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
                protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
                protocolo.Reprovar(DateTimeOffset.UtcNow);
                break;
            case StatusProtocolo.Excecao:
                protocolo.MarcarExcecao("ninguém com alçada");
                break;
            case StatusProtocolo.Descartado:
                protocolo.MarcarExcecao("ninguém com alçada");
                protocolo.Descartar();
                break;
            case StatusProtocolo.Excluido:
                protocolo.Excluir();
                break;
        }

        return protocolo;
    }

    [Theory]
    [InlineData(StatusProtocolo.Reprovado)]
    [InlineData(StatusProtocolo.Descartado)]
    [InlineData(StatusProtocolo.Excluido)]
    public void PodeRecriar_TodosOsRegistrosForaDeUso_DevolveTrue(StatusProtocolo status)
    {
        var podeRecriar = ResolvedorDeContinuidade.PodeRecriar([NovoProtocoloComStatus(status)]);

        Assert.True(podeRecriar);
    }

    [Theory]
    [InlineData(StatusProtocolo.Pool)]
    [InlineData(StatusProtocolo.Atribuido)]
    [InlineData(StatusProtocolo.Conferindo)]
    [InlineData(StatusProtocolo.Aprovado)]
    [InlineData(StatusProtocolo.Excecao)]
    public void PodeRecriar_AlgumRegistroAindaEmUso_DevolveFalse(StatusProtocolo status)
    {
        var podeRecriar = ResolvedorDeContinuidade.PodeRecriar([NovoProtocoloComStatus(status)]);

        Assert.False(podeRecriar);
    }

    [Fact]
    public void PodeRecriar_HistoricoVazio_DevolveTrue()
    {
        Assert.True(ResolvedorDeContinuidade.PodeRecriar([]));
    }

    [Fact]
    public void PodeRecriar_UmRegistroReprovadoEOutroAindaAtribuido_DevolveFalse()
    {
        var historico = new[] { NovoProtocoloComStatus(StatusProtocolo.Reprovado), NovoProtocoloComStatus(StatusProtocolo.Atribuido) };

        Assert.False(ResolvedorDeContinuidade.PodeRecriar(historico));
    }
}
