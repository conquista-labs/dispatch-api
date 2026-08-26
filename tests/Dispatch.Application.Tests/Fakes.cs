using Dispatch.Domain;

namespace Dispatch.Application.Tests;

internal sealed class FakeConferenteRepository(IReadOnlyCollection<Conferente> conferentes) : IConferenteRepository
{
    public Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Conferente>>(conferentes.Where(c => c.NaEscala).ToList());
}

internal sealed class FakeEquipeRepository(IReadOnlyCollection<Equipe> equipes) : IEquipeRepository
{
    public Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken) =>
        Task.FromResult(equipes);
}

internal sealed class FakeRegraAlcadaRepository(IReadOnlyCollection<RegraAlcada> regras) : IRegraAlcadaRepository
{
    public Task<IReadOnlyCollection<RegraAlcada>> ObterAtivasAsync(CancellationToken cancellationToken) =>
        Task.FromResult(regras);
}

internal sealed class FakeTipoAtoRepository(IReadOnlyCollection<TipoAto> tipos) : ITipoAtoRepository
{
    public Task<IReadOnlyCollection<TipoAto>> ObterTodosAsync(CancellationToken cancellationToken) =>
        Task.FromResult(tipos);
}

internal sealed class FakeRelogio(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; } = agora;
}
