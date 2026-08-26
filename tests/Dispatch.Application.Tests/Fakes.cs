using Dispatch.Domain;

namespace Dispatch.Application.Tests;

internal sealed class FakeConferenteRepository : IConferenteRepository
{
    private readonly List<Conferente> _conferentes;

    public FakeConferenteRepository(IReadOnlyCollection<Conferente> conferentes) => _conferentes = conferentes.ToList();

    public Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Conferente>>(_conferentes.Where(c => c.NaEscala).ToList());

    public Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_conferentes.SingleOrDefault(c => c.Id == id));

    public void Adicionar(Conferente conferente) => _conferentes.Add(conferente);
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

internal sealed class FakeUsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _usuarios;

    public FakeUsuarioRepository(IReadOnlyCollection<Usuario> usuarios) => _usuarios = usuarios.ToList();

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_usuarios.SingleOrDefault(u => u.Email == email));

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_usuarios.SingleOrDefault(u => u.Id == id));

    public Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_usuarios.Any(u => u.Email == email));

    public void Adicionar(Usuario usuario) => _usuarios.Add(usuario);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task SalvarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeRelogio(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; } = agora;
}
