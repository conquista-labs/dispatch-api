namespace Dispatch.Domain.Tests;

// RF-24k: protocolo que volta depois de não aprovado é a 2ª conferência (3ª, 4ª...). "Voltar" =
// o mesmo Número reaparecer na mesma etapa depois de uma linha Reprovada (cada rodada é uma
// linha própria de Protocolo — ADR-0006, Numero nunca é único).
public class NumeroDaConferenciaTests
{
    private const string Numero = "263546";
    private static readonly DateTimeOffset Base = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static RegistroDoNumero Registro(
        StatusProtocolo status,
        int horasDepoisDaBase,
        Etapa etapa = Etapa.PosConferencia,
        string numero = Numero) =>
        new(Guid.NewGuid(), numero, etapa, Base.AddHours(horasDepoisDaBase), status);

    [Fact]
    public void HistoricoVazio_EhAPrimeira()
    {
        var atual = Registro(StatusProtocolo.Pool, 10);

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(atual, [atual]));
    }

    [Fact]
    public void UmReprovadoAntes_EhASegunda()
    {
        var atual = Registro(StatusProtocolo.Pool, 10);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 1), atual };

        Assert.Equal(2, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void DoisReprovadosAntes_EhATerceira()
    {
        var atual = Registro(StatusProtocolo.Atribuido, 10);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 1), Registro(StatusProtocolo.Reprovado, 5), atual };

        Assert.Equal(3, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void ReprovadoNaOutraEtapa_NaoConta()
    {
        var atual = Registro(StatusProtocolo.Pool, 10, Etapa.PosConferencia);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 1, Etapa.PreConferencia), atual };

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void ReprovadoDeOutroNumero_NaoConta()
    {
        // O histórico em lote (uma query pra listagem inteira) mistura números.
        var atual = Registro(StatusProtocolo.Pool, 10);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 1, numero: "999999"), atual };

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    // Só Reprovado conta como "voltou". Aprovado (inclusive corrigido de Reprovado pra Aprovado),
    // linha ainda aberta, Descartado e Excluído não são uma conferência reprovada.
    [Theory]
    [InlineData(StatusProtocolo.Pool)]
    [InlineData(StatusProtocolo.Atribuido)]
    [InlineData(StatusProtocolo.Conferindo)]
    [InlineData(StatusProtocolo.Aprovado)]
    [InlineData(StatusProtocolo.Excecao)]
    [InlineData(StatusProtocolo.Descartado)]
    [InlineData(StatusProtocolo.Excluido)]
    public void LinhaAnteriorQueNaoEhReprovada_NaoConta(StatusProtocolo status)
    {
        var atual = Registro(StatusProtocolo.Pool, 10);
        var historico = new[] { Registro(status, 1), atual };

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void ReprovadoComAndamentoPosterior_NaoConta()
    {
        // A linha antiga continua sendo a 1ª mesmo depois que a nova (posterior) também foi reprovada.
        var antiga = Registro(StatusProtocolo.Reprovado, 1);
        var historico = new[] { antiga, Registro(StatusProtocolo.Reprovado, 10) };

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(antiga, historico));
    }

    [Fact]
    public void ReprovadoComMesmoAndamento_NaoConta()
    {
        var atual = Registro(StatusProtocolo.Pool, 10);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 10), atual };

        Assert.Equal(1, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void APropriaLinhaReprovada_NaoContaASiMesma()
    {
        var atual = Registro(StatusProtocolo.Reprovado, 10);
        var historico = new[] { Registro(StatusProtocolo.Reprovado, 1), atual };

        Assert.Equal(2, ResolvedorDeContinuidade.NumeroDaConferencia(atual, historico));
    }

    [Fact]
    public void De_CopiaOsCamposDoProtocolo()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), Numero, Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, Base);

        var registro = RegistroDoNumero.De(protocolo);

        Assert.Equal(
            new RegistroDoNumero(protocolo.Id, Numero, Etapa.PreConferencia, Base, StatusProtocolo.Pool),
            registro);
    }
}
