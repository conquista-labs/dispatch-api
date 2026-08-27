namespace Dispatch.Domain.Tests;

public class GeradorDeSugestoesTests
{
    private static Protocolo ProtocoloComTipoDesconhecido(string nomeTipo, Guid donoId) =>
        CriarProtocolo(tipoAtoId: null, escreventeId: Guid.NewGuid(), donoId: donoId, tipoAtoNomeOriginal: nomeTipo);

    private static Protocolo CriarProtocolo(
        Guid? tipoAtoId, Guid escreventeId, Guid? donoId = null, string? tipoAtoNomeOriginal = null,
        Guid? loteImportacaoId = null, StatusProtocolo status = StatusProtocolo.Excecao,
        DateTimeOffset? andamentoEm = null, TipoPrazo? prazo = null, DateTimeOffset? concluidoEm = null,
        Etapa etapa = Etapa.PreConferencia)
    {
        var referencia = andamentoEm ?? DateTimeOffset.UtcNow;
        var protocolo = new Protocolo(
            Guid.NewGuid(), "1", tipoAtoId, escreventeId, etapa, referencia,
            loteImportacaoId: loteImportacaoId, tipoAtoNomeOriginal: tipoAtoNomeOriginal);

        if (donoId is { } dono)
        {
            protocolo.AtribuirA(dono);
        }

        if (prazo is { } tipoPrazo)
        {
            protocolo.DefinirPrazo(new Prazo(tipoPrazo), referencia);
        }

        switch (status)
        {
            case StatusProtocolo.Conferindo:
                protocolo.IniciarConferencia(referencia);
                break;
            case StatusProtocolo.Aprovado when concluidoEm is { } fimAprovado:
                protocolo.IniciarConferencia(referencia);
                protocolo.Aprovar(fimAprovado);
                break;
            case StatusProtocolo.Reprovado when concluidoEm is { } fimReprovado:
                protocolo.IniciarConferencia(referencia);
                protocolo.Reprovar(fimReprovado);
                break;
        }

        return protocolo;
    }

    public class TipoDesconhecidoTests
    {
        [Fact]
        public void MenosDeCincoOcorrencias_NaoGeraCandidato()
        {
            var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
            var protocolos = Enumerable.Range(0, 4).Select(_ => ProtocoloComTipoDesconhecido("ARROLAMENTO", conferente.Id)).ToList();

            var candidatos = GeradorDeSugestoes.TipoDesconhecido(protocolos, [conferente]);

            Assert.Empty(candidatos);
        }

        [Fact]
        public void CincoOuMaisOcorrenciasResolvidasNaMao_SugereModaDoNivel()
        {
            var plenoA = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
            var plenoB = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
            var junior = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, true, 0);

            var protocolos = new List<Protocolo>
            {
                ProtocoloComTipoDesconhecido("ARROLAMENTO", plenoA.Id),
                ProtocoloComTipoDesconhecido("ARROLAMENTO", plenoA.Id),
                ProtocoloComTipoDesconhecido("arrolamento", plenoB.Id),
                ProtocoloComTipoDesconhecido("ARROLAMENTO", plenoB.Id),
                ProtocoloComTipoDesconhecido("ARROLAMENTO", junior.Id)
            };

            var candidatos = GeradorDeSugestoes.TipoDesconhecido(protocolos, [plenoA, plenoB, junior]);

            var candidato = Assert.Single(candidatos);
            var payload = Assert.IsType<PayloadSugestao.TipoDesconhecido>(candidato.Payload);
            Assert.Equal(Nivel.Pleno, payload.NivelSugerido);
            Assert.Equal(5, candidato.Ocorrencias);
        }

        [Fact]
        public void AindaEmExcecaoSemDono_NaoConta()
        {
            var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
            var protocolos = Enumerable.Range(0, 5)
                .Select(_ => CriarProtocolo(tipoAtoId: null, Guid.NewGuid(), tipoAtoNomeOriginal: "ARROLAMENTO"))
                .ToList();

            var candidatos = GeradorDeSugestoes.TipoDesconhecido(protocolos, [conferente]);

            Assert.Empty(candidatos);
        }
    }

    public class PrazoIrrealTests
    {
        // Referência à meia-noite: FimDoDia(referencia, diasAFrente) vira "referencia + (diasAFrente+1) dias"
        // exatamente, sem sobra de horas — deixa o vencimento de D+1 previsível: referencia + 48h.
        private static readonly DateTimeOffset Referencia = new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CasosSuficientesComEstouroAlto_SugereFaixaMaisProximaDoPercentil80()
        {
            var equipeId = Guid.NewGuid();
            var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);

            // Prazo D1 a partir da meia-noite vence em +48h. 8 casos concluídos entre 58h e
            // 65h depois — todos estouram, percentil 80 (índice 6 de 8) cai em 64h, mais perto
            // de D2 (60h) do que de D1 (36h).
            var protocolos = Enumerable.Range(0, 8).Select(i => CriarProtocolo(
                    tipoAtoId: Guid.NewGuid(),
                    escrevente.Id,
                    donoId: Guid.NewGuid(),
                    status: StatusProtocolo.Aprovado,
                    andamentoEm: Referencia,
                    prazo: TipoPrazo.D1,
                    concluidoEm: Referencia.AddHours(58 + i)))
                .ToList();

            var candidatos = GeradorDeSugestoes.PrazoIrreal(protocolos, [escrevente]);

            var candidato = Assert.Single(candidatos);
            var payload = Assert.IsType<PayloadSugestao.PrazoIrreal>(candidato.Payload);
            Assert.Equal(equipeId, payload.EquipeId);
            Assert.Equal(Etapa.PreConferencia, payload.Etapa);
            Assert.Equal(TipoPrazo.D2, payload.PrazoSugerido);
        }

        [Fact]
        public void PoucosCasos_NaoGeraCandidatoMesmoComEstouroTotal()
        {
            var equipeId = Guid.NewGuid();
            var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);

            var protocolos = Enumerable.Range(0, 3).Select(_ => CriarProtocolo(
                    tipoAtoId: Guid.NewGuid(), escrevente.Id, donoId: Guid.NewGuid(), status: StatusProtocolo.Aprovado,
                    andamentoEm: Referencia, prazo: TipoPrazo.D1, concluidoEm: Referencia.AddDays(4)))
                .ToList();

            var candidatos = GeradorDeSugestoes.PrazoIrreal(protocolos, [escrevente]);

            Assert.Empty(candidatos);
        }

        [Fact]
        public void CasosSuficientesMasPoucoEstouro_NaoGeraCandidato()
        {
            var equipeId = Guid.NewGuid();
            var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);

            // 8 casos, só 1 estourando (12.5% < 60%). Prazo D1 vence em +48h.
            var protocolos = Enumerable.Range(0, 8).Select(i => CriarProtocolo(
                    tipoAtoId: Guid.NewGuid(), escrevente.Id, donoId: Guid.NewGuid(), status: StatusProtocolo.Aprovado,
                    andamentoEm: Referencia, prazo: TipoPrazo.D1,
                    concluidoEm: i == 0 ? Referencia.AddDays(3) : Referencia.AddHours(2)))
                .ToList();

            var candidatos = GeradorDeSugestoes.PrazoIrreal(protocolos, [escrevente]);

            Assert.Empty(candidatos);
        }
    }

    public class EscreventeOrfaoTests
    {
        [Fact]
        public void OrfaoComPoucosProtocolos_NaoGeraCandidato()
        {
            var loteId = Guid.NewGuid();
            var orfao = new Escrevente(Guid.NewGuid(), "Sem Equipe", equipeId: null);
            var protocolos = Enumerable.Range(0, 2)
                .Select(_ => CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId))
                .ToList();

            var candidatos = GeradorDeSugestoes.EscreventeOrfao([orfao], protocolos);

            Assert.Empty(candidatos);
        }

        [Fact]
        public void OrfaoComTresOuMaisProtocolos_SugereEquipeDominanteDoMesmoLote()
        {
            var loteId = Guid.NewGuid();
            var equipeDominante = Guid.NewGuid();
            var equipeMinoritaria = Guid.NewGuid();

            var orfao = new Escrevente(Guid.NewGuid(), "Sem Equipe", equipeId: null);
            var colegaA = new Escrevente(Guid.NewGuid(), "Colega A", equipeDominante);
            var colegaB = new Escrevente(Guid.NewGuid(), "Colega B", equipeDominante);
            var colegaC = new Escrevente(Guid.NewGuid(), "Colega C", equipeMinoritaria);

            var protocolos = new List<Protocolo>
            {
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), colegaA.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), colegaB.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), colegaC.Id, loteImportacaoId: loteId)
            };

            var candidatos = GeradorDeSugestoes.EscreventeOrfao([orfao, colegaA, colegaB, colegaC], protocolos);

            var candidato = Assert.Single(candidatos);
            var payload = Assert.IsType<PayloadSugestao.EscreventeOrfao>(candidato.Payload);
            Assert.Equal(orfao.Id, payload.EscreventeId);
            Assert.Equal(equipeDominante, payload.EquipeSugeridaId);
        }

        [Fact]
        public void SemNenhumColegaComEquipeNoMesmoLote_NaoGeraCandidato()
        {
            var loteId = Guid.NewGuid();
            var orfao = new Escrevente(Guid.NewGuid(), "Sem Equipe", equipeId: null);
            var outroOrfao = new Escrevente(Guid.NewGuid(), "Também Sem Equipe", equipeId: null);

            var protocolos = new List<Protocolo>
            {
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), orfao.Id, loteImportacaoId: loteId),
                CriarProtocolo(Guid.NewGuid(), outroOrfao.Id, loteImportacaoId: loteId)
            };

            var candidatos = GeradorDeSugestoes.EscreventeOrfao([orfao, outroOrfao], protocolos);

            Assert.Empty(candidatos);
        }
    }

    public class RiscoQualidadeTests
    {
        [Fact]
        public void CasosSuficientesComReprovacaoAlta_SugereRestringirONivel()
        {
            var junior = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, true, 0);
            var tipoAtoId = Guid.NewGuid();
            var referencia = DateTimeOffset.UtcNow;

            // 6 casos, 4 reprovados (66% > 50%).
            var protocolos = Enumerable.Range(0, 6).Select(i => CriarProtocolo(
                    tipoAtoId, Guid.NewGuid(), donoId: junior.Id,
                    status: i < 4 ? StatusProtocolo.Reprovado : StatusProtocolo.Aprovado,
                    andamentoEm: referencia, concluidoEm: referencia.AddHours(1)))
                .ToList();

            var candidatos = GeradorDeSugestoes.RiscoQualidade(protocolos, [junior]);

            var candidato = Assert.Single(candidatos);
            var payload = Assert.IsType<PayloadSugestao.RiscoQualidade>(candidato.Payload);
            Assert.Equal(tipoAtoId, payload.TipoAtoId);
            Assert.Equal(Nivel.Junior, payload.NivelRestrito);
        }

        [Fact]
        public void NivelSenior_NuncaGeraCandidato_NaoHaNivelAcimaPraRestringir()
        {
            var senior = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Senior, 8, true, 0);
            var tipoAtoId = Guid.NewGuid();
            var referencia = DateTimeOffset.UtcNow;

            var protocolos = Enumerable.Range(0, 6).Select(_ => CriarProtocolo(
                    tipoAtoId, Guid.NewGuid(), donoId: senior.Id, status: StatusProtocolo.Reprovado,
                    andamentoEm: referencia, concluidoEm: referencia.AddHours(1)))
                .ToList();

            var candidatos = GeradorDeSugestoes.RiscoQualidade(protocolos, [senior]);

            Assert.Empty(candidatos);
        }

        [Fact]
        public void ReprovacaoBaixa_NaoGeraCandidato()
        {
            var pleno = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, true, 0);
            var tipoAtoId = Guid.NewGuid();
            var referencia = DateTimeOffset.UtcNow;

            // 6 casos, só 1 reprovado (16% < 50%).
            var protocolos = Enumerable.Range(0, 6).Select(i => CriarProtocolo(
                    tipoAtoId, Guid.NewGuid(), donoId: pleno.Id,
                    status: i == 0 ? StatusProtocolo.Reprovado : StatusProtocolo.Aprovado,
                    andamentoEm: referencia, concluidoEm: referencia.AddHours(1)))
                .ToList();

            var candidatos = GeradorDeSugestoes.RiscoQualidade(protocolos, [pleno]);

            Assert.Empty(candidatos);
        }
    }
}
