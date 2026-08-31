using Dispatch.Application;
using Dispatch.Domain;
using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class SugestaoRepository(DispatchDbContext dbContext) : ISugestaoRepository
{
    public async Task<IReadOnlyCollection<Sugestao>> ObterPendentesAsync(CancellationToken cancellationToken)
    {
        var registros = await dbContext.Sugestoes.Where(s => s.Status == StatusSugestao.Pendente).ToListAsync(cancellationToken);
        return registros.Select(ParaDominio).ToList();
    }

    public async Task<IReadOnlyCollection<Sugestao>> ObterHistoricoAsync(CancellationToken cancellationToken)
    {
        var registros = await dbContext.Sugestoes.Where(s => s.Status != StatusSugestao.Pendente).ToListAsync(cancellationToken);
        return registros.Select(ParaDominio).ToList();
    }

    public async Task<Sugestao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var registro = await dbContext.Sugestoes.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        return registro is null ? null : ParaDominio(registro);
    }

    public async Task<Sugestao?> ObterPorChaveAtivaAsync(string chave, CancellationToken cancellationToken)
    {
        var registro = await dbContext.Sugestoes
            .Where(s => s.Chave == chave)
            .OrderByDescending(s => s.CriadaEm)
            .FirstOrDefaultAsync(cancellationToken);
        return registro is null ? null : ParaDominio(registro);
    }

    public void Adicionar(Sugestao sugestao) => dbContext.Sugestoes.Add(ParaRegistro(sugestao));

    public async Task AtualizarEvidenciaAsync(
        Guid id, int ocorrencias, string evidencia, double indiceConfianca, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var registro = await dbContext.Sugestoes.SingleAsync(s => s.Id == id, cancellationToken);
        registro.Ocorrencias = ocorrencias;
        registro.Evidencia = evidencia;
        registro.IndiceConfianca = indiceConfianca;
        registro.AtualizadaEm = agora;
    }

    public async Task<bool> AplicarAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var registro = await dbContext.Sugestoes.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (registro is null)
        {
            return false;
        }

        registro.Status = StatusSugestao.Aplicada;
        registro.DecididaEm = agora;
        return true;
    }

    public async Task<bool> DescartarAsync(Guid id, DateTimeOffset agora, DateTimeOffset descartarAte, CancellationToken cancellationToken)
    {
        var registro = await dbContext.Sugestoes.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (registro is null)
        {
            return false;
        }

        registro.Status = StatusSugestao.Descartada;
        registro.DecididaEm = agora;
        registro.DescartarAte = descartarAte;
        return true;
    }

    private static Sugestao ParaDominio(SugestaoRegistro registro)
    {
        PayloadSugestao payload = registro.Tipo switch
        {
            TipoSugestaoRegistro.TipoDesconhecido => new PayloadSugestao.TipoDesconhecido(
                registro.TipoDesconhecidoNomeTipo!, registro.TipoDesconhecidoNivelSugerido!.Value),

            TipoSugestaoRegistro.PrazoIrreal => new PayloadSugestao.PrazoIrreal(
                registro.PrazoIrrealEquipeId!.Value, registro.PrazoIrrealEtapa!.Value, registro.PrazoIrrealPrazoSugerido!.Value),

            TipoSugestaoRegistro.EscreventeOrfao => new PayloadSugestao.EscreventeOrfao(
                registro.EscreventeOrfaoEscreventeId!.Value, registro.EscreventeOrfaoEquipeSugeridaId!.Value),

            TipoSugestaoRegistro.RiscoQualidade => new PayloadSugestao.RiscoQualidade(
                registro.RiscoQualidadeTipoAtoId!.Value, registro.RiscoQualidadeNivelRestrito!.Value),

            _ => throw new InvalidOperationException($"Tipo de sugestão não mapeado: {registro.Tipo}")
        };

        return new Sugestao(
            registro.Id, registro.Chave, payload, registro.Evidencia, registro.Ocorrencias, registro.IndiceConfianca, registro.CriadaEm,
            registro.AtualizadaEm, registro.Status, registro.DecididaEm, registro.DescartarAte);
    }

    private static SugestaoRegistro ParaRegistro(Sugestao sugestao)
    {
        var registro = new SugestaoRegistro
        {
            Id = sugestao.Id,
            Chave = sugestao.Chave,
            Evidencia = sugestao.Evidencia,
            Ocorrencias = sugestao.Ocorrencias,
            IndiceConfianca = sugestao.IndiceConfianca,
            Status = sugestao.Status,
            CriadaEm = sugestao.CriadaEm,
            AtualizadaEm = sugestao.AtualizadaEm,
            DecididaEm = sugestao.DecididaEm,
            DescartarAte = sugestao.DescartarAte
        };

        switch (sugestao.Payload)
        {
            case PayloadSugestao.TipoDesconhecido tipoDesconhecido:
                registro.Tipo = TipoSugestaoRegistro.TipoDesconhecido;
                registro.TipoDesconhecidoNomeTipo = tipoDesconhecido.NomeTipo;
                registro.TipoDesconhecidoNivelSugerido = tipoDesconhecido.NivelSugerido;
                break;

            case PayloadSugestao.PrazoIrreal prazoIrreal:
                registro.Tipo = TipoSugestaoRegistro.PrazoIrreal;
                registro.PrazoIrrealEquipeId = prazoIrreal.EquipeId;
                registro.PrazoIrrealEtapa = prazoIrreal.Etapa;
                registro.PrazoIrrealPrazoSugerido = prazoIrreal.PrazoSugerido;
                break;

            case PayloadSugestao.EscreventeOrfao escreventeOrfao:
                registro.Tipo = TipoSugestaoRegistro.EscreventeOrfao;
                registro.EscreventeOrfaoEscreventeId = escreventeOrfao.EscreventeId;
                registro.EscreventeOrfaoEquipeSugeridaId = escreventeOrfao.EquipeSugeridaId;
                break;

            case PayloadSugestao.RiscoQualidade riscoQualidade:
                registro.Tipo = TipoSugestaoRegistro.RiscoQualidade;
                registro.RiscoQualidadeTipoAtoId = riscoQualidade.TipoAtoId;
                registro.RiscoQualidadeNivelRestrito = riscoQualidade.NivelRestrito;
                break;
        }

        return registro;
    }
}
