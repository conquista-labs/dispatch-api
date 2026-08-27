using Dispatch.Domain;

namespace Dispatch.Application.Tests;

internal sealed class FakeConferenteRepository : IConferenteRepository
{
    private readonly List<Conferente> _conferentes;

    public FakeConferenteRepository(IReadOnlyCollection<Conferente> conferentes) => _conferentes = conferentes.ToList();

    public Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Conferente>>(_conferentes.Where(c => c.NaEscala).ToList());

    public Task<IReadOnlyCollection<Conferente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Conferente>>(_conferentes.ToList());

    public Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_conferentes.SingleOrDefault(c => c.Id == id));

    public void Adicionar(Conferente conferente) => _conferentes.Add(conferente);
}

internal sealed class FakeEquipeRepository : IEquipeRepository
{
    private readonly List<Equipe> _equipes;

    public FakeEquipeRepository(IReadOnlyCollection<Equipe> equipes) => _equipes = equipes.ToList();

    public Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Equipe>>(_equipes.ToList());

    public Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_equipes.SingleOrDefault(e => e.Id == id));

    public void Adicionar(Equipe equipe) => _equipes.Add(equipe);

    public int Quantidade => _equipes.Count;
}

internal sealed class FakeRegraAlcadaRepository : IRegraAlcadaRepository
{
    private readonly List<RegraAlcada> _regras;

    public FakeRegraAlcadaRepository(IReadOnlyCollection<RegraAlcada> regras) => _regras = regras.ToList();

    public Task<IReadOnlyCollection<RegraAlcada>> ObterAtivasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<RegraAlcada>>(_regras.Where(r => r.Ativa).ToList());

    public Task<IReadOnlyCollection<RegraAlcada>> ObterTodasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<RegraAlcada>>(_regras.ToList());

    public Task<RegraAlcada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_regras.SingleOrDefault(r => r.Id == id));

    public void Adicionar(RegraAlcada regra) => _regras.Add(regra);

    public Task<bool> AtivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var regra = _regras.SingleOrDefault(r => r.Id == id);
        regra?.Ativar();
        return Task.FromResult(regra is not null);
    }

    public Task<bool> DesativarAsync(Guid id, CancellationToken cancellationToken)
    {
        var regra = _regras.SingleOrDefault(r => r.Id == id);
        regra?.Desativar();
        return Task.FromResult(regra is not null);
    }

    public Task<bool> RemoverAsync(Guid id, CancellationToken cancellationToken)
    {
        var regra = _regras.SingleOrDefault(r => r.Id == id);
        if (regra is null)
        {
            return Task.FromResult(false);
        }

        _regras.Remove(regra);
        return Task.FromResult(true);
    }

    public int Quantidade => _regras.Count;
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

    public Task<IReadOnlyCollection<Protocolo>> ObterSemDonoAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Protocolo>>(
            _protocolos.Where(p => p.Status is StatusProtocolo.Pool or StatusProtocolo.Excecao).ToList());

    public int Quantidade => _protocolos.Count;
}

internal sealed class FakeEscreventeRepository : IEscreventeRepository
{
    private readonly List<Escrevente> _escreventes;

    public FakeEscreventeRepository(IReadOnlyCollection<Escrevente> escreventes) => _escreventes = escreventes.ToList();

    public Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Escrevente>>(_escreventes.ToList());

    public Task<Escrevente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_escreventes.SingleOrDefault(e => e.Id == id));

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
