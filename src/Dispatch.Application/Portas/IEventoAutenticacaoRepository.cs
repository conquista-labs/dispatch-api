using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEventoAutenticacaoRepository
{
    void Adicionar(EventoAutenticacao evento);
}
