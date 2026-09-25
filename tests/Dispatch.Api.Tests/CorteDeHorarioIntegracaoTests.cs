using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

// Pedido do dono: "equipe X entra na pós-conferência depois das 16h, vence às 10h do dia
// seguinte" — genérico por Equipe+Etapa, não hardcoded (ver
// docs/decisions/0037-corte-de-horario-por-equipe-e-etapa.md). O risco
// real que só um teste de integração de verdade prova: Prazo ganhou um campo além de Tipo
// (HorarioDeVencimento) e PrazoConversoes precisou de um formato composto pra não perder esse
// campo no round-trip do banco — sem isso, reabrir um protocolo com corte quebraria depois de
// recarregado de uma query nova (o cenário exato que este teste reproduz).
[Collection(IntegracaoCollection.Nome)]
public sealed class CorteDeHorarioIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    [Fact]
    public async Task ProtocoloCriadoDepoisDoCorte_UsaCorteDeHorario_ESobreviveARoundTripEReabertura()
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var equipeId = await CriarEquipeComCorteAsync(distribuidora);
        var tipoAtoId = await CriarTipoAtoAsync(distribuidora);
        const string nomeEscrevente = "Escrevente Corte E2E";
        await CriarEscreventeAsync(distribuidora, nomeEscrevente, equipeId);

        // 26/08/2026 16h30 em Brasília — depois do corte das 16h.
        var andamento = new DateTimeOffset(2026, 8, 26, 16, 30, 0, TimeSpan.FromHours(-3));
        var protocoloId = await CriarProtocoloManualAsync(distribuidora, tipoAtoId, nomeEscrevente, andamento);

        // Confirma direto no banco (não só na resposta HTTP do momento da criação) — prova que
        // o round-trip (gravar → reler numa query nova) preserva Tipo E HorarioDeVencimento.
        await NoBancoAsync(async db =>
        {
            var protocolo = await db.Protocolos.AsNoTracking().SingleAsync(p => p.Id == protocoloId);
            Assert.Equal(TipoPrazo.CorteDeHorario, protocolo.Prazo!.Tipo);
            Assert.Equal(new TimeOnly(10, 0), protocolo.Prazo.HorarioDeVencimento);
            Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.FromHours(-3)), protocolo.VencimentoEm);
        });

        // Ciclo completo até Aprovado, pra poder reabrir (ReabrirConferencia reusa o Prazo já
        // persistido e recarregado — é exatamente o caminho que quebraria sem o fix em
        // PrazoConversoes).
        var conferenteId = await ObterConferenteIdAsync(distribuidora, "conferente-rf27@cartorio.com");
        var atribuiu = await distribuidora.PostAsJsonAsync($"/protocolos/{protocoloId}/atribuir", new { conferenteId });
        Assert.Equal(HttpStatusCode.NoContent, atribuiu.StatusCode);

        var conferente = await AutenticarComoAsync(Papel.Conferente);
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{protocoloId}/iniciar", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await conferente.PostAsJsonAsync($"/minha-fila/{protocoloId}/concluir", new { aprovado = true })).StatusCode);

        // `AutenticarComoAsync` re-semeia (POST /dev/seed-e2e) toda vez que é chamado — a
        // chamada acima (Conferente) reseta as 3 contas de novo, o que pode invalidar o token
        // da distribuidora obtido no início do teste (SessoesValidasApartirDe bumped depois de
        // IssuedAt do token antigo, RF-01k). Reautentica pra garantir um token emitido depois
        // do último seed — achado como flake real rodando a suíte inteira várias vezes.
        distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var reabriu = await distribuidora.PostAsync($"/protocolos/{protocoloId}/reabrir-conferencia", content: null);
        Assert.True(reabriu.IsSuccessStatusCode, $"status={reabriu.StatusCode} corpo={await reabriu.Content.ReadAsStringAsync()}");

        // Reaberto: vencimento recalcula a partir de "agora" (não do andamento original), ainda
        // usando o mesmo Prazo(CorteDeHorario, 10:00) — sem lançar.
        await NoBancoAsync(async db =>
        {
            var protocolo = await db.Protocolos.AsNoTracking().SingleAsync(p => p.Id == protocoloId);
            Assert.Equal(TipoPrazo.CorteDeHorario, protocolo.Prazo!.Tipo);
            Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        });
    }

    private static async Task<Guid> CriarEquipeComCorteAsync(HttpClient distribuidora)
    {
        var resposta = await distribuidora.PostAsJsonAsync("/equipes", new
        {
            nome = "Quinto Andar",
            prazoPreConferencia = nameof(TipoPrazo.D1),
            prazoPosConferencia = nameof(TipoPrazo.D1),
            cortePosConferenciaHorarioCorte = "16:00",
            cortePosConferenciaHorarioVencimento = "10:00",
        });
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("equipeId").GetGuid();
    }

    private static async Task<Guid> CriarTipoAtoAsync(HttpClient distribuidora)
    {
        var resposta = await distribuidora.PostAsJsonAsync("/tipos-ato", new { nome = "Ato Corte E2E" });
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("tipoAtoId").GetGuid();
    }

    private static async Task CriarEscreventeAsync(HttpClient distribuidora, string nome, Guid equipeId)
    {
        var resposta = await distribuidora.PostAsJsonAsync("/escreventes", new { nome, equipeId });
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CriarProtocoloManualAsync(
        HttpClient distribuidora, Guid tipoAtoId, string escreventeNome, DateTimeOffset andamentoEm)
    {
        var resposta = await distribuidora.PostAsJsonAsync("/protocolos/manual", new
        {
            numero = "CORTE-E2E-1",
            tipoAtoId,
            escreventeNome,
            etapa = nameof(Etapa.PosConferencia),
            prioridade = nameof(Prioridade.Normal),
            observacao = (string?)null,
            andamentoEm,
        });
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("protocoloId").GetGuid();
    }

    private static async Task<Guid> ObterConferenteIdAsync(HttpClient distribuidora, string email)
    {
        var conferentes = await distribuidora.GetFromJsonAsync<JsonElement>("/conferentes");
        var conferente = conferentes.EnumerateArray().Single(c => c.GetProperty("email").GetString() == email);
        return conferente.GetProperty("id").GetGuid();
    }
}
