using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterVisaoDistribuicaoTests
{
    private static Protocolo NovoProtocolo(StatusProtocolo status, Guid? donoId = null, Guid? loteImportacaoId = null)
    {
        var protocolo = new Protocolo(
            Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow, loteImportacaoId: loteImportacaoId);

        switch (status)
        {
            case StatusProtocolo.Atribuido:
                protocolo.AtribuirA(donoId ?? Guid.NewGuid());
                break;
            case StatusProtocolo.Excecao:
                protocolo.MarcarExcecao("tipo desconhecido");
                break;
            case StatusProtocolo.Pool:
                break;
        }

        return protocolo;
    }

    [Fact]
    public async Task SeparaProtocolosPorStatus()
    {
        var pool = NovoProtocolo(StatusProtocolo.Pool);
        var atribuido = NovoProtocolo(StatusProtocolo.Atribuido);
        var excecao = NovoProtocolo(StatusProtocolo.Excecao);
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([pool, atribuido, excecao]));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal([pool], visao.Pool);
        Assert.Equal([atribuido], visao.Atribuidos);
        Assert.Equal([excecao], visao.Excecoes);
        Assert.Empty(visao.EmConferencia);
        Assert.Empty(visao.Concluidos);
    }

    [Fact]
    public async Task AgrupaAtribuidosPorConferente()
    {
        var conferenteId = Guid.NewGuid();
        var protocolo1 = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        var protocolo2 = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        var deOutraPessoa = NovoProtocolo(StatusProtocolo.Atribuido);
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([protocolo1, protocolo2, deOutraPessoa]));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal(2, visao.PorConferente.Count);
        var grupo = visao.PorConferente.Single(g => g.ConferenteId == conferenteId);
        Assert.Equal(2, grupo.Protocolos.Count);
    }

    [Fact]
    public async Task FiltraPorLoteQuandoInformado()
    {
        var loteId = Guid.NewGuid();
        var doLote = NovoProtocolo(StatusProtocolo.Pool, loteImportacaoId: loteId);
        var deOutroLote = NovoProtocolo(StatusProtocolo.Pool, loteImportacaoId: Guid.NewGuid());
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([doLote, deOutroLote]));

        var visao = await casoDeUso.ExecutarAsync(loteId);

        Assert.Equal([doLote], visao.Pool);
    }
}
