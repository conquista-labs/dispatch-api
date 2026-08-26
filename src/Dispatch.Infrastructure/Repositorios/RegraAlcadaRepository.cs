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

    private static RegraAlcada ParaDominio(RegraAlcadaRegistro registro)
    {
        SujeitoAlcada sujeito = registro.SujeitoConferenteId is { } conferenteId
            ? new SujeitoAlcada.PorPessoa(conferenteId)
            : new SujeitoAlcada.PorNivel(registro.SujeitoNivel!.Value);

        AlvoAlcada alvo = registro.AlvoTipoAtoId is { } tipoAtoId
            ? new AlvoAlcada.PorTipoAto(tipoAtoId)
            : new AlvoAlcada.PorEtapa(registro.AlvoEtapa!.Value);

        return new RegraAlcada(registro.Id, sujeito, registro.Permissao, alvo, registro.Ativa);
    }
}
