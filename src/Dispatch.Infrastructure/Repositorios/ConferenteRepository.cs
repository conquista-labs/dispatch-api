using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class ConferenteRepository(DispatchDbContext dbContext) : IConferenteRepository
{
    // RF-28: CargaAtual é "quantos protocolos essa pessoa tem na mão agora" — não é uma coluna
    // que alguém atualiza a cada atribuição/devolução (isso duplicaria estado e dessincronizaria
    // fatalmente cedo ou tarde), é sempre recalculado na leitura a partir da fonte de verdade
    // (Protocolos.DonoId). Efeito colateral bem-vindo: MotorDistribuicao.cs usa CargaAtual pra
    // desempatar quem pega um protocolo urgente (`OrderBy(a => a.Conferente.CargaAtual)`) — antes
    // dessa mudança essa comparação sempre dava 0 contra 0 (a coluna nunca era escrita depois do
    // cadastro), então o desempate nunca funcionou de verdade. Só ObterTodosAsync/ObterNaEscalaAsync
    // computam isso — ObterPorIdAsync continua uma query simples e rastreada pelo EF, porque as
    // únicas mutações de Conferente (EditarNivelEJornada/MarcarPresenca/RemoverConferente) passam
    // por ela, e uma entidade construída via projeção fica desconectada do change tracker (mesma
    // armadilha já documentada pra RegraAlcada/Sugestao).
    private static readonly StatusProtocolo[] StatusQueContamComoCarga = [StatusProtocolo.Atribuido, StatusProtocolo.Conferindo];

    public async Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        await dbContext.Conferentes
            .Where(c => c.NaEscala)
            .Select(c => new Conferente(
                c.Id, c.UsuarioId, c.Nivel, c.JornadaHoras, c.NaEscala,
                dbContext.Protocolos.Count(p => p.DonoId == c.Id && StatusQueContamComoCarga.Contains(p.Status))))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Conferente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        await dbContext.Conferentes
            .Select(c => new Conferente(
                c.Id, c.UsuarioId, c.Nivel, c.JornadaHoras, c.NaEscala,
                dbContext.Protocolos.Count(p => p.DonoId == c.Id && StatusQueContamComoCarga.Contains(p.Status))))
            .ToListAsync(cancellationToken);

    public async Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Conferentes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Conferente?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await dbContext.Conferentes.SingleOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

    public void Adicionar(Conferente conferente) => dbContext.Conferentes.Add(conferente);
}
