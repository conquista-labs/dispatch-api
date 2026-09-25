using Dispatch.Domain;

namespace Dispatch.Application;

// RF-25. Conferente (Domain) não guarda nome — isso é dado de Usuario — e nenhuma tela
// consegue mostrar "quem é" sem juntar os dois. Gap real: até aqui não existia leitura
// nenhuma que fizesse essa junção (só ObterAlcancePorConferente, que devolve alcance, não
// identidade).
// RF-28: "capacidade estimada" = jornada ÷ tempo médio por ato. O tempo médio (documento de
// requisitos, seção 11, premissas) vem da tabela `config` (seção 8).
//
// Perfil Administrador (ADR-0039, RF-29a): o nível (cargo) só sai pra um token de Administrador —
// sem a flag, `Nivel` vem null. A flag é obrigatória (sem default) pra que um chamador novo não
// vaze o cargo por esquecimento.
public sealed class ListarConferentes(IConferenteRepository conferentes, IUsuarioRepository usuarios, IConfiguracaoRepository configuracao)
{
    public async Task<IReadOnlyList<ConferenteComUsuario>> ExecutarAsync(bool incluirNivel, CancellationToken cancellationToken = default)
    {
        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var usuarioIds = todosConferentes.Select(c => c.UsuarioId).ToList();
        var todosUsuarios = await usuarios.ObterVariosPorIdsAsync(usuarioIds, cancellationToken);
        var tempoMedioPorAtoMinutos = (await configuracao.ObterAsync(cancellationToken)).TempoMedioPorAtoMinutos;

        return todosConferentes
            // RF-25: "remover" é soft delete (Usuario.Desativar) — ativo=false significa "não é
            // mais conferente", não é um estado que alguma tela deva mostrar. Filtrar aqui, na
            // única leitura agregada, poupa cada consumidor (tela de Conferentes, seletor de
            // atribuição manual em Exceções, o que mais vier) de ter que lembrar disso sozinho.
            .Where(conferente => todosUsuarios.Single(u => u.Id == conferente.UsuarioId).Ativo)
            .Select(conferente =>
            {
                var usuario = todosUsuarios.Single(u => u.Id == conferente.UsuarioId);
                var capacidadeEstimada = Math.Max(1, (int)Math.Round(conferente.JornadaHoras * 60 / tempoMedioPorAtoMinutos));
                return new ConferenteComUsuario(
                    conferente.Id, usuario.Nome, usuario.Email, usuario.Ativo,
                    incluirNivel ? conferente.Nivel : null, conferente.JornadaHoras, conferente.NaEscala, conferente.CargaAtual, capacidadeEstimada);
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
    Guid Id, string Nome, string Email, bool Ativo, Nivel? Nivel, double JornadaHoras, bool NaEscala, int CargaAtual, int CapacidadeEstimada);
