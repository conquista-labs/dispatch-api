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
            // RF-25: "remover" é soft delete (Usuario.Desativar) — ativo=false significa "não é
            // mais conferente", não é um estado que alguma tela deva mostrar. Filtrar aqui, na
            // única leitura agregada, poupa cada consumidor (tela de Conferentes, seletor de
            // atribuição manual em Exceções, o que mais vier) de ter que lembrar disso sozinho.
            .Where(conferente => todosUsuarios.Single(u => u.Id == conferente.UsuarioId).Ativo)
            .Select(conferente =>
            {
                var usuario = todosUsuarios.Single(u => u.Id == conferente.UsuarioId);
                var capacidadeEstimada = Math.Max(1, (int)Math.Round(conferente.JornadaHoras * 60 / TempoMedioPorAtoMinutos));
                return new ConferenteComUsuario(
                    conferente.Id, usuario.Nome, usuario.Email, usuario.Ativo,
                    conferente.Nivel, conferente.JornadaHoras, conferente.NaEscala, conferente.CargaAtual, capacidadeEstimada);
            })
            // Sem isso a ordem vinha da leitura crua do Postgres, que não é garantida estável
            // entre uma chamada e outra sem ORDER BY — a lista "pulava" de posição a cada
            // refetch depois de qualquer ação (RF-25/26/27 invalidam a query inteira).
            // `ThenBy(Id)`: nome sozinho não é único (dois conferentes de teste têm o mesmo
            // nome só com e-mail diferente) — sem um desempate de verdade, quem empata no nome
            // continua sujeito à mesma ordem instável do Postgres entre as duas leituras.
            .OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .ToList();
    }
}

public sealed record ConferenteComUsuario(
    Guid Id, string Nome, string Email, bool Ativo, Nivel Nivel, double JornadaHoras, bool NaEscala, int CargaAtual, int CapacidadeEstimada);
