using Dispatch.Domain;

namespace Dispatch.Application;

// Classificação vista na Matriz da aba Alçada (v2 do protótipo) — o protótipo não tem nenhuma
// tela de gestão de grupo, só leitura agrupada; este caso de uso é o que falta pra alguém
// conseguir classificar um tipo novo, já que sem isso a Matriz nunca teria dado real pra ler.
public sealed class DefinirGrupoDoTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid tipoAtoId, GrupoTipoAto? grupo, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return false;
        }

        tipoAto.DefinirGrupo(grupo);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
