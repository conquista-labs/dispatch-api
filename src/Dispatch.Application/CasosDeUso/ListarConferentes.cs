using Dispatch.Domain;

namespace Dispatch.Application;

// RF-25. Conferente (Domain) não guarda nome — isso é dado de Usuario — e nenhuma tela
// consegue mostrar "quem é" sem juntar os dois. Gap real: até aqui não existia leitura
// nenhuma que fizesse essa junção (só ObterAlcancePorConferente, que devolve alcance, não
// identidade).
public sealed class ListarConferentes(IConferenteRepository conferentes, IUsuarioRepository usuarios)
{
    // RF-28: "capacidade estimada" = jornada ÷ tempo médio por ato. O documento de requisitos
    // (seção 11, premissas) fixa esse tempo médio em 18min — mesmo tipo de constante hardcoded
    // que as faixas do semáforo (4h/60min), até existir tabela de config (seção 8).
    private const double TempoMedioPorAtoMinutos = 18;

    public async Task<IReadOnlyList<ConferenteComUsuario>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var usuarioIds = todosConferentes.Select(c => c.UsuarioId).ToList();
        var todosUsuarios = await usuarios.ObterVariosPorIdsAsync(usuarioIds, cancellationToken);

        return todosConferentes
            .Select(conferente =>
            {
                var usuario = todosUsuarios.Single(u => u.Id == conferente.UsuarioId);
                var capacidadeEstimada = Math.Max(1, (int)Math.Round(conferente.JornadaHoras * 60 / TempoMedioPorAtoMinutos));
                return new ConferenteComUsuario(
                    conferente.Id, usuario.Nome, usuario.Email, usuario.Ativo,
                    conferente.Nivel, conferente.JornadaHoras, conferente.NaEscala, conferente.CargaAtual, capacidadeEstimada);
            })
            .ToList();
    }
}

public sealed record ConferenteComUsuario(
    Guid Id, string Nome, string Email, bool Ativo, Nivel Nivel, double JornadaHoras, bool NaEscala, int CargaAtual, int CapacidadeEstimada);
