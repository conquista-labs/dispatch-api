using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// RF-42a — "Hoje, agora" (gestão) e "Seu dia" (conferente). Os relógios fixos cobrem as duas
// pontas do dia de Brasília: 23h locais (já é o dia seguinte em UTC — onde "hoje pelo dia UTC"
// erraria) e 10h locais (manhã, mesmo dia nos dois fusos).
public class ObterPainelDeHojeTests
{
    // 24/09/2026 23:00 em Brasília = 25/09/2026 02:00 UTC.
    private static readonly DateTimeOffset VinteETresHorasLocal = new(2026, 9, 24, 23, 0, 0, FusoHorario.Brasilia);
    // 24/09/2026 10:00 em Brasília = 24/09/2026 13:00 UTC.
    private static readonly DateTimeOffset DezHorasLocal = new(2026, 9, 24, 10, 0, 0, FusoHorario.Brasilia);

    private static readonly Guid EquipeA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid EquipeB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Escrevente EscreventeDaA = new(Guid.NewGuid(), "Escrevente A", EquipeA);
    private static readonly Escrevente OutroEscreventeDaA = new(Guid.NewGuid(), "Escrevente A2", EquipeA);
    private static readonly Escrevente EscreventeDaB = new(Guid.NewGuid(), "Escrevente B", EquipeB);
    private static readonly Escrevente EscreventeSemEquipe = new(Guid.NewGuid(), "Escrevente sem equipe", equipeId: null);

    private static ObterPainelDeHoje CasoDeUso(DateTimeOffset agora, params Protocolo[] protocolos) =>
        new(new FakeProtocoloRepository(protocolos),
            new FakeEscreventeRepository([EscreventeDaA, OutroEscreventeDaA, EscreventeDaB, EscreventeSemEquipe]),
            new FakeRelogio(agora.ToUniversalTime()));

    // Vencimento exato: prazo de 1 hora (o único que não empurra pra dia útil) a partir de
    // vencimento - 1h.
    private static Protocolo Aberto(
        StatusProtocolo status, DateTimeOffset? vencimento = null, Guid? donoId = null, Escrevente? escrevente = null)
    {
        var protocolo = new Protocolo(
            Guid.NewGuid(), "P", Guid.NewGuid(), (escrevente ?? EscreventeSemEquipe).Id, Etapa.PreConferencia, DezHorasLocal.AddDays(-1));
        if (vencimento is { } v)
        {
            protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), v.AddHours(-1));
        }

        switch (status)
        {
            case StatusProtocolo.Pool:
                break;
            case StatusProtocolo.Atribuido:
                protocolo.AtribuirA(donoId ?? Guid.NewGuid(), DezHorasLocal.AddDays(-1));
                break;
            case StatusProtocolo.Conferindo:
                protocolo.AtribuirA(donoId ?? Guid.NewGuid(), DezHorasLocal.AddDays(-1));
                protocolo.IniciarConferencia(DezHorasLocal.AddDays(-1));
                break;
            case StatusProtocolo.Excecao:
                protocolo.MarcarExcecao("tipo desconhecido");
                break;
            case StatusProtocolo.Descartado:
                protocolo.MarcarExcecao("tipo desconhecido");
                protocolo.Descartar();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "use Concluido para Aprovado/Reprovado");
        }

        return protocolo;
    }

    private static Protocolo Concluido(DateTimeOffset concluidoEm, Guid? donoId = null, bool aprovado = true, DateTimeOffset? vencimento = null)
    {
        var protocolo = Aberto(StatusProtocolo.Conferindo, vencimento, donoId);
        if (aprovado)
        {
            protocolo.Aprovar(concluidoEm.ToUniversalTime());
        }
        else
        {
            protocolo.Reprovar(concluidoEm.ToUniversalTime());
        }

        return protocolo;
    }

    private static DateTimeOffset Local(int dia, int hora, int minuto = 0) => new(2026, 9, dia, hora, minuto, 0, FusoHorario.Brasilia);

    [Fact]
    public async Task Gestao_As23hDeBrasilia_ConferidosHojeContaSoODiaLocal()
    {
        var painel = await CasoDeUso(
            VinteETresHorasLocal,
            Concluido(Local(24, 0, 10)),                  // começo do dia local — conta
            Concluido(Local(24, 20), aprovado: false),    // 23:00 UTC do dia 24 — "hoje pelo dia UTC" perderia este
            Concluido(Local(24, 22, 30)),                 // já é dia 25 em UTC — conta
            Concluido(Local(23, 23, 50))                  // véspera local — não conta
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(VisaoPainelHoje.Gestao, painel.Visao);
        Assert.Equal(3, painel.ConferidosHoje);
        Assert.Equal(VinteETresHorasLocal, painel.AtualizadoEm);
    }

    [Fact]
    public async Task Gestao_As10hDeBrasilia_ConferidosHojeComecaNaMeiaNoiteLocal()
    {
        var painel = await CasoDeUso(
            DezHorasLocal,
            Concluido(Local(24, 0, 0)),     // exatamente meia-noite local — conta (>=)
            Concluido(Local(24, 9, 59)),
            Concluido(Local(23, 23, 59)),   // 02:59 UTC do dia 24 — mesmo dia em UTC, mas véspera local
            Concluido(Local(23, 21, 0))
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(2, painel.ConferidosHoje);
    }

    [Fact]
    public async Task Gestao_ContaFilaEExcecoes_SoComAbertos()
    {
        var painel = await CasoDeUso(
            DezHorasLocal,
            Aberto(StatusProtocolo.Pool),
            Aberto(StatusProtocolo.Pool),
            Aberto(StatusProtocolo.Atribuido),
            Aberto(StatusProtocolo.Conferindo),
            Aberto(StatusProtocolo.Excecao),
            Aberto(StatusProtocolo.Descartado),
            Concluido(Local(24, 9))
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new NaFilaHoje(Pool: 2, ComConferente: 2), painel.NaFila);
        Assert.Equal(1, painel.Excecoes);
        Assert.Null(painel.NaMao);
    }

    [Fact]
    public async Task Gestao_EstouradoEVenceEmUmaHora_NasBordas()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddTicks(-1)),                     // estourado
            Aberto(StatusProtocolo.Excecao, agora.AddHours(-5)),                  // estourado (exceção também é aberto)
            Aberto(StatusProtocolo.Atribuido, agora),                             // vence em 1h (agora ≤ v)
            Aberto(StatusProtocolo.Conferindo, agora.AddHours(1).AddTicks(-1)),   // vence em 1h (v < agora+1h)
            Aberto(StatusProtocolo.Pool, agora.AddHours(1)),                      // fora das duas
            Aberto(StatusProtocolo.Pool),                                         // sem vencimento — fora
            Aberto(StatusProtocolo.Descartado, agora.AddHours(-1)),               // terminal — fora
            Concluido(Local(24, 9), vencimento: agora.AddHours(-3))               // concluído — fora
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new EmRiscoHoje(Estourados: 2, VencemEmUmaHora: 2), painel.EmRisco);
    }

    [Fact]
    public async Task Gestao_Gargalo_EquipeComMaisProtocolosEmRisco()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA),
            Aberto(StatusProtocolo.Atribuido, agora.AddMinutes(30), escrevente: OutroEscreventeDaA),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-2), escrevente: EscreventeDaB),
            Aberto(StatusProtocolo.Pool, agora.AddHours(3), escrevente: EscreventeDaB),   // não está em risco
            Aberto(StatusProtocolo.Pool, agora.AddHours(4), escrevente: EscreventeDaB)
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new GargaloHoje(EquipeA, 2), painel.Gargalo);
    }

    [Fact]
    public async Task Gestao_SemGargalo_QuandoNenhumaEquipePassaDeUm()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaB),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeSemEquipe)
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(3, painel.EmRisco.Estourados);
        Assert.Null(painel.Gargalo);
    }

    [Fact]
    public async Task Gestao_SemGargalo_ComUmSoProtocoloEmRisco()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(agora, Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA))
            .ExecutarAsync(conferenteRestritoId: null);

        Assert.Null(painel.Gargalo);
    }

    [Fact]
    public async Task Gestao_SemEquipeEhGrupoProprio_InclusiveEscreventeQueNaoExisteMais()
    {
        var agora = DezHorasLocal;
        var escreventeApagado = new Escrevente(Guid.NewGuid(), "fora do cadastro", EquipeA);
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeSemEquipe),
            Aberto(StatusProtocolo.Excecao, agora.AddMinutes(10), escrevente: escreventeApagado),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA)
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new GargaloHoje(EquipeId: null, 2), painel.Gargalo);
    }

    [Fact]
    public async Task Gestao_EmpateNoGargalo_VenceOMenorEquipeId()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaB),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaB),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaA)
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new GargaloHoje(EquipeA, 2), painel.Gargalo);
    }

    [Fact]
    public async Task Gestao_EmpateEntreEquipeESemEquipe_VenceAEquipe()
    {
        var agora = DezHorasLocal;
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeSemEquipe),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeSemEquipe),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaB),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1), escrevente: EscreventeDaB)
        ).ExecutarAsync(conferenteRestritoId: null);

        Assert.Equal(new GargaloHoje(EquipeB, 2), painel.Gargalo);
    }

    [Fact]
    public async Task Conferente_SoOsDaMaoDele_ECamposDeGestaoNulos()
    {
        var agora = VinteETresHorasLocal;
        var eu = Guid.NewGuid();
        var colega = Guid.NewGuid();
        var painel = await CasoDeUso(
            agora,
            Aberto(StatusProtocolo.Atribuido, agora.AddHours(-1), donoId: eu),              // meu, estourado
            Aberto(StatusProtocolo.Atribuido, agora.AddHours(5), donoId: eu),               // meu, folgado
            Aberto(StatusProtocolo.Conferindo, agora.AddMinutes(59), donoId: eu),           // meu, vence em 1h
            Aberto(StatusProtocolo.Atribuido, agora.AddHours(-1), donoId: colega),          // do colega — fora
            Aberto(StatusProtocolo.Conferindo, agora.AddMinutes(10), donoId: colega),
            Aberto(StatusProtocolo.Pool, agora.AddHours(-1)),                               // pool não é da mão de ninguém
            Aberto(StatusProtocolo.Excecao, agora.AddHours(-1)),
            Concluido(Local(24, 20), donoId: eu),                                           // meu, hoje local
            Concluido(Local(24, 8), donoId: eu, aprovado: false),                           // meu, hoje local
            Concluido(Local(23, 23, 30), donoId: eu),                                       // meu, véspera local
            Concluido(Local(24, 21), donoId: colega)                                        // do colega
        ).ExecutarAsync(conferenteRestritoId: eu);

        Assert.Equal(VisaoPainelHoje.Conferente, painel.Visao);
        Assert.Equal(2, painel.ConferidosHoje);
        Assert.Equal(new NaMaoHoje(Total: 3, EmConferencia: 1), painel.NaMao);
        Assert.Equal(new EmRiscoHoje(Estourados: 1, VencemEmUmaHora: 1), painel.EmRisco);
        Assert.Null(painel.NaFila);
        Assert.Null(painel.Excecoes);
        Assert.Null(painel.Gargalo);
    }

    [Fact]
    public async Task Conferente_SemNadaNaMao_DevolveZeros()
    {
        var painel = await CasoDeUso(DezHorasLocal, Aberto(StatusProtocolo.Pool, DezHorasLocal.AddHours(-1)))
            .ExecutarAsync(conferenteRestritoId: Guid.NewGuid());

        Assert.Equal(0, painel.ConferidosHoje);
        Assert.Equal(new NaMaoHoje(0, 0), painel.NaMao);
        Assert.Equal(new EmRiscoHoje(0, 0), painel.EmRisco);
    }
}
