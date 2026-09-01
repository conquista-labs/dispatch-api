using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class EventoAutenticacaoRepository(DispatchDbContext dbContext) : IEventoAutenticacaoRepository
{
    public void Adicionar(EventoAutenticacao evento) => dbContext.EventosAutenticacao.Add(evento);
}
