using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CriarEquipeTests
{
    [Fact]
    public async Task Cria_ComOsPrazosInformados()
    {
        var equipes = new FakeEquipeRepository([]);
        var casoDeUso = new CriarEquipe(equipes, new FakeUnitOfWork());

        var id = await casoDeUso.ExecutarAsync("5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1));

        Assert.Equal(1, equipes.Quantidade);
        var equipe = await equipes.ObterPorIdAsync(id, CancellationToken.None);
        Assert.Equal("5º andar", equipe!.Nome);
        Assert.Equal(TipoPrazo.D0, equipe.PrazoPreConferencia.Tipo);
    }
}

public class EditarEquipeTests
{
    private static EditarEquipe NovoCasoDeUso(
        FakeEquipeRepository equipes, FakeEscreventeRepository? escreventes = null, FakeProtocoloRepository? protocolos = null) =>
        new(equipes, escreventes ?? new FakeEscreventeRepository([]), protocolos ?? new FakeProtocoloRepository([]), new FakeUnitOfWork());

    [Fact]
    public async Task EquipeExistente_RenomeiaEAtualizaPrazos()
    {
        var equipe = new Equipe(Guid.NewGuid(), "Antigo", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var casoDeUso = NovoCasoDeUso(new FakeEquipeRepository([equipe]));

        var resultado = await casoDeUso.ExecutarAsync(equipe.Id, "Novo nome", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D2));

        Assert.True(resultado);
        Assert.Equal("Novo nome", equipe.Nome);
        Assert.Equal(TipoPrazo.D0, equipe.PrazoPreConferencia.Tipo);
        Assert.Equal(TipoPrazo.D2, equipe.PrazoPosConferencia.Tipo);
    }

    [Fact]
    public async Task EquipeInexistente_RetornaFalse()
    {
        var casoDeUso = NovoCasoDeUso(new FakeEquipeRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "x", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D0));

        Assert.False(resultado);
    }

    [Fact]
    public async Task MudarPrazo_RecalculaVencimentoDosProtocolosAbertosDaEquipe()
    {
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipe.Id);
        var referencia = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

        var aberto = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), escrevente.Id, Etapa.PreConferencia, referencia);
        aberto.DefinirPrazo(new Prazo(TipoPrazo.D1), referencia);

        var concluido = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), escrevente.Id, Etapa.PreConferencia, referencia);
        concluido.DefinirPrazo(new Prazo(TipoPrazo.D1), referencia);
        concluido.AtribuirA(Guid.NewGuid());
        concluido.Descartar();
        var vencimentoOriginalDoConcluido = concluido.VencimentoEm;

        var casoDeUso = NovoCasoDeUso(
            new FakeEquipeRepository([equipe]),
            new FakeEscreventeRepository([escrevente]),
            new FakeProtocoloRepository([aberto, concluido]));

        await casoDeUso.ExecutarAsync(equipe.Id, equipe.Nome, new Prazo(TipoPrazo.UmaHora), equipe.PrazoPosConferencia);

        Assert.Equal(referencia.AddHours(1), aberto.VencimentoEm);
        // Descartado é terminal — não deveria ser tocado pelo recálculo.
        Assert.Equal(vencimentoOriginalDoConcluido, concluido.VencimentoEm);
    }
}

public class MoverEscreventeParaEquipeTests
{
    [Fact]
    public async Task EscreventeSemEquipe_MoveParaEquipeExistente()
    {
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);
        var casoDeUso = new MoverEscreventeParaEquipe(
            new FakeEscreventeRepository([escrevente]), new FakeEquipeRepository([equipe]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(escrevente.Id, equipe.Id);

        Assert.Equal(ResultadoMoverEscrevente.Sucesso, resultado);
        Assert.Equal(equipe.Id, escrevente.EquipeId);
    }

    [Fact]
    public async Task EquipeInexistente_Rejeita()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);
        var casoDeUso = new MoverEscreventeParaEquipe(
            new FakeEscreventeRepository([escrevente]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(escrevente.Id, Guid.NewGuid());

        Assert.Equal(ResultadoMoverEscrevente.EquipeNaoEncontrada, resultado);
    }

    [Fact]
    public async Task EscreventeInexistente_Rejeita()
    {
        var casoDeUso = new MoverEscreventeParaEquipe(
            new FakeEscreventeRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), null);

        Assert.Equal(ResultadoMoverEscrevente.EscreventeNaoEncontrado, resultado);
    }
}

public class ListarEscreventesSemEquipeTests
{
    [Fact]
    public async Task RetornaSoQuemNaoTemEquipe()
    {
        var comEquipe = new Escrevente(Guid.NewGuid(), "Com equipe", equipeId: Guid.NewGuid());
        var semEquipe = new Escrevente(Guid.NewGuid(), "Sem equipe", equipeId: null);
        var casoDeUso = new ListarEscreventesSemEquipe(new FakeEscreventeRepository([comEquipe, semEquipe]));

        var resultado = await casoDeUso.ExecutarAsync();

        Assert.Equal([semEquipe], resultado);
    }
}
