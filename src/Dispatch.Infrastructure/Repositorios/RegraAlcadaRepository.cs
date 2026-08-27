using Dispatch.Application;
using Dispatch.Domain;
using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class RegraAlcadaRepository(DispatchDbContext dbContext) : IRegraAlcadaRepository
{
    public async Task<IReadOnlyCollection<RegraAlcada>> ObterAtivasAsync(CancellationToken cancellationToken)
    {
        var registros = await dbContext.RegrasDeAlcada.Where(r => r.Ativa).ToListAsync(cancellationToken);
        return registros.Select(ParaDominio).ToList();
    }

    public async Task<IReadOnlyCollection<RegraAlcada>> ObterTodasAsync(CancellationToken cancellationToken)
    {
        var registros = await dbContext.RegrasDeAlcada.ToListAsync(cancellationToken);
        return registros.Select(ParaDominio).ToList();
    }

    public async Task<RegraAlcada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var registro = await dbContext.RegrasDeAlcada.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return registro is null ? null : ParaDominio(registro);
    }

    public void Adicionar(RegraAlcada regra) => dbContext.RegrasDeAlcada.Add(ParaRegistro(regra));

    public async Task<bool> AtivarAsync(Guid id, CancellationToken cancellationToken) =>
        await AlterarAtivaAsync(id, ativa: true, cancellationToken);

    public async Task<bool> DesativarAsync(Guid id, CancellationToken cancellationToken) =>
        await AlterarAtivaAsync(id, ativa: false, cancellationToken);

    private async Task<bool> AlterarAtivaAsync(Guid id, bool ativa, CancellationToken cancellationToken)
    {
        var registro = await dbContext.RegrasDeAlcada.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (registro is null)
        {
            return false;
        }

        registro.Ativa = ativa;
        return true;
    }

    public async Task<bool> RemoverAsync(Guid id, CancellationToken cancellationToken)
    {
        var registro = await dbContext.RegrasDeAlcada.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (registro is null)
        {
            return false;
        }

        dbContext.RegrasDeAlcada.Remove(registro);
        return true;
    }

    private static RegraAlcada ParaDominio(RegraAlcadaRegistro registro)
    {
        SujeitoAlcada sujeito = registro.SujeitoConferenteId is { } conferenteId
            ? new SujeitoAlcada.PorPessoa(conferenteId)
            : new SujeitoAlcada.PorNivel(registro.SujeitoNivel!.Value);

        AlvoAlcada alvo = registro.AlvoTipoAtoId is { } tipoAtoId
            ? new AlvoAlcada.PorTipoAto(tipoAtoId)
            : new AlvoAlcada.PorEtapa(registro.AlvoEtapa!.Value);

        return new RegraAlcada(registro.Id, sujeito, registro.Permissao, alvo, registro.Origem, registro.Ativa);
    }

    private static RegraAlcadaRegistro ParaRegistro(RegraAlcada regra) => new()
    {
        Id = regra.Id,
        SujeitoConferenteId = (regra.Sujeito as SujeitoAlcada.PorPessoa)?.ConferenteId,
        SujeitoNivel = (regra.Sujeito as SujeitoAlcada.PorNivel)?.Nivel,
        AlvoEtapa = (regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa,
        AlvoTipoAtoId = (regra.Alvo as AlvoAlcada.PorTipoAto)?.TipoAtoId,
        Permissao = regra.Permissao,
        Origem = regra.Origem,
        Ativa = regra.Ativa
    };
}
