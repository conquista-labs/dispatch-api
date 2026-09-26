namespace Dispatch.Domain.Tests;

// Regra do dono de 2026-09-26 (ADR-0046): o conferente pega do pool só na vez — Alta primeiro, depois
// quem vence antes (sem vencimento por último), depois quem entrou antes (AndamentoEm), depois
// Numero/Id pra ser determinístico — e só enquanto tem menos atos "na mão" (atribuídos + em
// conferência) que o limite da Configuração.
public class RegraDoPoolTests
{
    private static readonly DateTimeOffset Base = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static Protocolo NovoProtocolo(
        string numero, Prioridade prioridade = Prioridade.Normal, DateTimeOffset? vencimento = null,
        DateTimeOffset? andamento = null, Guid? id = null)
    {
        var protocolo = new Protocolo(
            id ?? Guid.NewGuid(), numero, Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, andamento ?? Base, prioridade);
        if (vencimento is { } v)
        {
            // UmaHora não empurra pra dia útil: vencimento = andamento de referência + 1h, exato.
            protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), v.AddHours(-1));
        }

        return protocolo;
    }

    [Fact]
    public void Ordenar_PrioridadeAltaVemAntesDeQualquerVencimento()
    {
        var normalVencendoJa = NovoProtocolo("1", Prioridade.Normal, vencimento: Base.AddMinutes(5));
        var altaVencendoDepois = NovoProtocolo("2", Prioridade.Alta, vencimento: Base.AddDays(3));

        var ordem = OrdemDoPool.Ordenar([normalVencendoJa, altaVencendoDepois]);

        Assert.Equal([altaVencendoDepois, normalVencendoJa], ordem);
    }

    [Fact]
    public void Ordenar_PrioridadeDecrescente_AltaNormalBaixa()
    {
        var baixa = NovoProtocolo("1", Prioridade.Baixa, vencimento: Base.AddMinutes(1));
        var normal = NovoProtocolo("2", Prioridade.Normal, vencimento: Base.AddMinutes(2));
        var alta = NovoProtocolo("3", Prioridade.Alta, vencimento: Base.AddMinutes(3));

        Assert.Equal([alta, normal, baixa], OrdemDoPool.Ordenar([baixa, normal, alta]));
    }

    [Fact]
    public void Ordenar_MesmaPrioridade_QuemVenceAntesPrimeiro()
    {
        var venceDepois = NovoProtocolo("1", vencimento: Base.AddHours(5));
        var venceAntes = NovoProtocolo("2", vencimento: Base.AddHours(2));

        Assert.Equal([venceAntes, venceDepois], OrdemDoPool.Ordenar([venceDepois, venceAntes]));
    }

    [Fact]
    public void Ordenar_SemVencimentoVemPorUltimoDentroDaPrioridade()
    {
        var semVencimento = NovoProtocolo("1", andamento: Base.AddDays(-10));
        var comVencimento = NovoProtocolo("2", vencimento: Base.AddDays(30));
        var altaSemVencimento = NovoProtocolo("3", Prioridade.Alta);

        // Sem vencimento cai pro fim da SUA prioridade — não passa na frente de uma prioridade menor
        // nem fica atrás dela.
        Assert.Equal([altaSemVencimento, comVencimento, semVencimento],
            OrdemDoPool.Ordenar([semVencimento, comVencimento, altaSemVencimento]));
    }

    [Fact]
    public void Ordenar_MesmoVencimento_QuemEntrouAntesPrimeiro()
    {
        var vencimento = Base.AddHours(4);
        var entrouDepois = NovoProtocolo("1", vencimento: vencimento, andamento: Base.AddMinutes(30));
        var entrouAntes = NovoProtocolo("2", vencimento: vencimento, andamento: Base);

        Assert.Equal([entrouAntes, entrouDepois], OrdemDoPool.Ordenar([entrouDepois, entrouAntes]));
    }

    [Fact]
    public void Ordenar_EmpateTotal_DesempataPorNumeroDepoisPorId()
    {
        var vencimento = Base.AddHours(4);
        // "20" com o menor Id de todos: prova que Numero decide antes do Id.
        var numeroMaior = NovoProtocolo("20", vencimento: vencimento, id: Guid.Parse("00000000-0000-0000-0000-000000000000"));
        var mesmoNumeroIdMenor = NovoProtocolo("10", vencimento: vencimento, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var numeroMenor = NovoProtocolo("10", vencimento: vencimento, id: Guid.Parse("88888888-0000-0000-0000-000000000000"));
        var mesmoNumeroIdMaior = NovoProtocolo("10", vencimento: vencimento, id: Guid.Parse("ffffffff-0000-0000-0000-000000000000"));

        var ordem = OrdemDoPool.Ordenar([numeroMaior, mesmoNumeroIdMaior, numeroMenor, mesmoNumeroIdMenor]);

        Assert.Equal([mesmoNumeroIdMenor, numeroMenor, mesmoNumeroIdMaior, numeroMaior], ordem);
    }

    [Fact]
    public void Ordenar_EhDeterministico_QualquerOrdemDeEntradaDaOMesmoResultado()
    {
        var itens = new[]
        {
            NovoProtocolo("5", Prioridade.Baixa),
            NovoProtocolo("4", Prioridade.Alta, vencimento: Base.AddHours(1)),
            NovoProtocolo("3", vencimento: Base.AddHours(1)),
            NovoProtocolo("2", vencimento: Base.AddHours(1), andamento: Base.AddMinutes(-5)),
            NovoProtocolo("1"),
        };

        Assert.Equal(OrdemDoPool.Ordenar(itens), OrdemDoPool.Ordenar(itens.Reverse()));
    }

    [Fact]
    public void Calcular_ProximoEhOPrimeiroDaOrdem_QuandoCabeNaMao()
    {
        var primeiro = NovoProtocolo("1", Prioridade.Alta);
        var segundo = NovoProtocolo("2");

        var regra = RegraDoPool.Calcular(ordemObrigatoria: true, limiteNaMao: 5, naMao: 4, poolOrdenado: [primeiro, segundo]);

        Assert.Equal(new RegraDoPool(true, 5, 4, primeiro.Id), regra);
    }

    [Fact]
    public void Calcular_SemProximo_QuandoAMaoEstaCheia()
    {
        var primeiro = NovoProtocolo("1");

        var regra = RegraDoPool.Calcular(ordemObrigatoria: true, limiteNaMao: 5, naMao: 5, poolOrdenado: [primeiro]);

        Assert.Null(regra.ProximoId);
    }

    [Fact]
    public void Calcular_SemProximo_QuandoOPoolEstaVazio()
    {
        var regra = RegraDoPool.Calcular(ordemObrigatoria: true, limiteNaMao: 5, naMao: 0, poolOrdenado: []);

        Assert.Null(regra.ProximoId);
    }

    [Fact]
    public void Calcular_ChaveDesligada_AindaIndicaOPrimeiroDaOrdem()
    {
        var primeiro = NovoProtocolo("1");

        var regra = RegraDoPool.Calcular(ordemObrigatoria: false, limiteNaMao: 5, naMao: 0, poolOrdenado: [primeiro, NovoProtocolo("2")]);

        Assert.Equal(primeiro.Id, regra.ProximoId);
    }

    [Fact]
    public void Avaliar_OPrimeiroDaVez_EhPermitido()
    {
        var primeiro = NovoProtocolo("1");
        var regra = RegraDoPool.Calcular(true, 5, 0, [primeiro, NovoProtocolo("2")]);

        Assert.Equal(DecisaoDoPegar.Permitido, regra.Avaliar(primeiro.Id));
    }

    [Fact]
    public void Avaliar_ForaDaVez_ComAChaveLigada_EhRecusado()
    {
        var segundo = NovoProtocolo("2");
        var regra = RegraDoPool.Calcular(true, 5, 0, [NovoProtocolo("1"), segundo]);

        Assert.Equal(DecisaoDoPegar.ForaDaVez, regra.Avaliar(segundo.Id));
    }

    [Fact]
    public void Avaliar_ForaDaVez_ComAChaveDesligada_EhPermitido()
    {
        var segundo = NovoProtocolo("2");
        var regra = RegraDoPool.Calcular(false, 5, 0, [NovoProtocolo("1"), segundo]);

        Assert.Equal(DecisaoDoPegar.Permitido, regra.Avaliar(segundo.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Avaliar_MaoCheia_VenceAVez_MesmoComOPrimeiroEComAChaveDesligada(bool ordemObrigatoria)
    {
        var primeiro = NovoProtocolo("1");
        var regra = RegraDoPool.Calcular(ordemObrigatoria, 3, 3, [primeiro]);

        Assert.Equal(DecisaoDoPegar.LimiteNaMao, regra.Avaliar(primeiro.Id));
    }

    [Fact]
    public void Avaliar_UmAbaixoDoLimite_AindaPodePegar()
    {
        var primeiro = NovoProtocolo("1");
        var regra = RegraDoPool.Calcular(true, 3, 2, [primeiro]);

        Assert.Equal(DecisaoDoPegar.Permitido, regra.Avaliar(primeiro.Id));
    }

    [Fact]
    public void Configuracao_NasceComLimiteCincoEOrdemObrigatoria()
    {
        var configuracao = NovaConfiguracao();

        Assert.Equal(5, configuracao.LimiteDeAtosNaMao);
        Assert.True(configuracao.PoolEmOrdemObrigatoria);
    }

    [Fact]
    public void Configuracao_DefinirRegraDoPool_TrocaOsDois()
    {
        var configuracao = NovaConfiguracao();

        configuracao.DefinirRegraDoPool(limiteDeAtosNaMao: 1, poolEmOrdemObrigatoria: false);

        Assert.Equal(1, configuracao.LimiteDeAtosNaMao);
        Assert.False(configuracao.PoolEmOrdemObrigatoria);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Configuracao_LimiteNaMaoAbaixoDeUm_EhRecusado(int limite)
    {
        var configuracao = NovaConfiguracao();

        Assert.Throws<ArgumentException>(() => configuracao.DefinirRegraDoPool(limite, true));
        Assert.Equal(5, configuracao.LimiteDeAtosNaMao);
    }

    private static Configuracao NovaConfiguracao() => new(
        Guid.NewGuid(), TimeSpan.FromHours(4), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15),
        30, 18, 5, 8, 0.6, 3, 6, 0.5);
}
