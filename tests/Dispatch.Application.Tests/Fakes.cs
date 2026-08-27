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

internal sealed class FakeProtocoloRepository : IProtocoloRepository
{
    private readonly List<Protocolo> _protocolos;

    public FakeProtocoloRepository(IReadOnlyCollection<Protocolo> protocolos) => _protocolos = protocolos.ToList();

    public void Adicionar(Protocolo protocolo) => _protocolos.Add(protocolo);

    public Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_protocolos.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Protocolo>>(
            _protocolos.Where(p => p.Status == StatusProtocolo.Atribuido && p.DonoId == conferenteId).ToList());

    public Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Protocolo>>(
            _protocolos.Where(p => loteImportacaoId == null || p.LoteImportacaoId == loteImportacaoId).ToList());

    public int Quantidade => _protocolos.Count;
}

internal sealed class FakeEscreventeRepository : IEscreventeRepository
{
    private readonly List<Escrevente> _escreventes;

    public FakeEscreventeRepository(IReadOnlyCollection<Escrevente> escreventes) => _escreventes = escreventes.ToList();

    public Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Escrevente>>(_escreventes.ToList());

    public void Adicionar(Escrevente escrevente) => _escreventes.Add(escrevente);

    public int Quantidade => _escreventes.Count;
}

internal sealed class FakeLoteImportacaoRepository : ILoteImportacaoRepository
{
    private readonly List<LoteImportacao> _lotes = [];

    public void Adicionar(LoteImportacao lote) => _lotes.Add(lote);

    public int Quantidade => _lotes.Count;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task SalvarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeRelogio(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; } = agora;
}
