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
                protocolo.AtribuirA(donoId ?? Guid.NewGuid(), DateTimeOffset.UtcNow);
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
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([pool, atribuido, excecao]), new FakeRelogio(DateTimeOffset.UtcNow));

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
        var casoDeUso = new ObterVisaoDistribuicao(
            new FakeProtocoloRepository([protocolo1, protocolo2, deOutraPessoa]), new FakeRelogio(DateTimeOffset.UtcNow));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal(2, visao.PorConferente.Count);
        var grupo = visao.PorConferente.Single(g => g.ConferenteId == conferenteId);
        Assert.Equal(2, grupo.Protocolos.Count);
    }

    [Fact]
    public async Task PoolOrdenaPorVencimentoAscendente()
    {
        // Quem vence primeiro fica no topo — protocolo sem prazo definido (VencimentoEm nulo)
        // vai pro fim, não pro início.
        var vencePrimeiro = NovoProtocolo(StatusProtocolo.Pool);
        vencePrimeiro.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow);

        var venceDepois = NovoProtocolo(StatusProtocolo.Pool);
        venceDepois.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), DateTimeOffset.UtcNow.AddHours(2));

        var semVencimento = NovoProtocolo(StatusProtocolo.Pool);

        var casoDeUso = new ObterVisaoDistribuicao(
            new FakeProtocoloRepository([venceDepois, semVencimento, vencePrimeiro]), new FakeRelogio(DateTimeOffset.UtcNow));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal([vencePrimeiro, venceDepois, semVencimento], visao.Pool);
    }

    [Fact]
    public async Task FiltraPorLoteQuandoInformado()
    {
        var loteId = Guid.NewGuid();
        var doLote = NovoProtocolo(StatusProtocolo.Pool, loteImportacaoId: loteId);
        var deOutroLote = NovoProtocolo(StatusProtocolo.Pool, loteImportacaoId: Guid.NewGuid());
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([doLote, deOutroLote]), new FakeRelogio(DateTimeOffset.UtcNow));

        var visao = await casoDeUso.ExecutarAsync(loteId);

        Assert.Equal([doLote], visao.Pool);
    }

    [Fact]
    public async Task ContaSoConcluidosDeHojePorConferente()
    {
        var conferenteId = Guid.NewGuid();
        var agora = new DateTimeOffset(2026, 3, 10, 15, 0, 0, TimeSpan.Zero);
        var inicioDoDia = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

        var protocolo1 = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        protocolo1.Aprovar(inicioDoDia.AddHours(9));
        var protocolo2 = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        protocolo2.Reprovar(inicioDoDia.AddHours(10));
        // Concluído ontem — não deve contar em "feitos hoje".
        var protocoloOntem = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        protocoloOntem.Aprovar(inicioDoDia.AddHours(-1));

        var casoDeUso = new ObterVisaoDistribuicao(
            new FakeProtocoloRepository([protocolo1, protocolo2, protocoloOntem]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        var grupo = Assert.Single(visao.ConcluidosHojePorConferente);
        Assert.Equal(conferenteId, grupo.ConferenteId);
        Assert.Equal(2, grupo.Total);
    }

    // "Feitos hoje" conta pelo dia de Brasília: às 22h30 de Brasília (01h30 UTC do dia
    // seguinte) o concluído às 18h de Brasília ainda é de hoje, e o das 23h da véspera não é.
    [Fact]
    public async Task ConcluidosHoje_Entre21hE24hDeBrasilia_ContaPeloDiaLocal()
    {
        var conferenteId = Guid.NewGuid();
        var brasilia = TimeSpan.FromHours(-3);
        var agora = new DateTimeOffset(2026, 3, 10, 22, 30, 0, brasilia).ToUniversalTime(); // 11/03 01h30 UTC

        var as18h = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        as18h.Aprovar(new DateTimeOffset(2026, 3, 10, 18, 0, 0, brasilia).ToUniversalTime());
        var as22h = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        as22h.Reprovar(new DateTimeOffset(2026, 3, 10, 22, 0, 0, brasilia).ToUniversalTime());
        var vespera23h = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        vespera23h.Aprovar(new DateTimeOffset(2026, 3, 9, 23, 0, 0, brasilia).ToUniversalTime());

        var casoDeUso = new ObterVisaoDistribuicao(
            new FakeProtocoloRepository([as18h, as22h, vespera23h]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        var grupo = Assert.Single(visao.ConcluidosHojePorConferente);
        Assert.Equal(conferenteId, grupo.ConferenteId);
        Assert.Equal(2, grupo.Total);
    }

    [Fact]
    public async Task ConferenteSemConcluidoHojeNaoAparece()
    {
        var conferenteId = Guid.NewGuid();
        var atribuido = NovoProtocolo(StatusProtocolo.Atribuido, conferenteId);
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([atribuido]), new FakeRelogio(DateTimeOffset.UtcNow));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Empty(visao.ConcluidosHojePorConferente);
    }

    [Fact]
    public async Task ConcluidoDentroDaJanelaAparece()
    {
        var agora = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var protocolo = NovoProtocolo(StatusProtocolo.Atribuido);
        protocolo.Aprovar(agora.AddDays(-5));

        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([protocolo]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal([protocolo], visao.Concluidos);
    }

    [Fact]
    public async Task ConcluidoForaDaJanelaNaoAparece()
    {
        var agora = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var protocolo = NovoProtocolo(StatusProtocolo.Atribuido);
        protocolo.Aprovar(agora.AddDays(-40));

        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([protocolo]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Empty(visao.Concluidos);
        Assert.Empty(visao.ConcluidosHojePorConferente);
    }

    [Fact]
    public async Task ProtocoloEmAndamentoApareceMesmoAntigo()
    {
        // Pool/Atribuído/Conferindo/Exceção nunca sofrem corte por data — só ConcluidoEm importa,
        // e nenhum desses status tem ConcluidoEm preenchido.
        var agora = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var excecaoAntiga = NovoProtocolo(StatusProtocolo.Excecao);

        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([excecaoAntiga]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal([excecaoAntiga], visao.Excecoes);
    }

    [Fact]
    public async Task LoteEspecificoIgnoraJanelaPadrao()
    {
        var agora = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var loteId = Guid.NewGuid();
        var protocolo = NovoProtocolo(StatusProtocolo.Atribuido, loteImportacaoId: loteId);
        protocolo.Aprovar(agora.AddDays(-90));

        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([protocolo]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: loteId);

        Assert.Equal([protocolo], visao.Concluidos);
    }

    [Fact]
    public async Task DescartadoNuncaAparece()
    {
        var descartado = NovoProtocolo(StatusProtocolo.Excecao);
        descartado.Descartar();

        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([descartado]), new FakeRelogio(DateTimeOffset.UtcNow));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Empty(visao.Pool);
        Assert.Empty(visao.Atribuidos);
        Assert.Empty(visao.EmConferencia);
        Assert.Empty(visao.Concluidos);
        Assert.Empty(visao.Excecoes);
    }

    // RF-24k: nº da conferência pra toda a visão, inclusive o próprio Reprovado (que é a 1ª).
    [Fact]
    public async Task NumeroDaConferencia_ContaReprovadosAnterioresDoMesmoNumero()
    {
        var agora = DateTimeOffset.UtcNow;
        var ontem = agora.AddDays(-1);
        var reprovadaAntes = new Protocolo(Guid.NewGuid(), "263546", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, ontem);
        reprovadaAntes.AtribuirA(Guid.NewGuid(), ontem);
        reprovadaAntes.IniciarConferencia(ontem);
        reprovadaAntes.Reprovar(ontem);
        var voltou = new Protocolo(Guid.NewGuid(), "263546", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, agora);
        var casoDeUso = new ObterVisaoDistribuicao(new FakeProtocoloRepository([reprovadaAntes, voltou]), new FakeRelogio(agora));

        var visao = await casoDeUso.ExecutarAsync(loteImportacaoId: null);

        Assert.Equal(2, visao.NumeroDaConferencia[voltou.Id]);
        Assert.Equal(1, visao.NumeroDaConferencia[reprovadaAntes.Id]);
    }
}
