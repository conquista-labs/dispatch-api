using Dispatch.Domain;

namespace Dispatch.Application;

// Perfil Administrador (ADR-0039, RF-30a): a distribuidora lê as regras em vigor, mas não pode
// ficar sabendo o cargo de ninguém. Regra por nível ("Júnior não pode fazer pré") diz, somada ao
// "quem pode conferir", o nível de cada pessoa — então, sem a flag, o sujeito de nível sai oculto.
// A flag é obrigatória pra um chamador novo não vazar por esquecimento.
public sealed class ListarRegrasAlcada(IRegraAlcadaRepository regras)
{
    public async Task<IReadOnlyList<RegraAlcadaVisivel>> ExecutarAsync(bool incluirNivel, CancellationToken cancellationToken = default) =>
        (await regras.ObterTodasAsync(cancellationToken))
            .Select(regra => new RegraAlcadaVisivel(regra, NivelOculto: !incluirNivel && regra.Sujeito is SujeitoAlcada.PorNivel))
            .ToList();
}

public sealed record RegraAlcadaVisivel(RegraAlcada Regra, bool NivelOculto)
{
    // "Regra base da alçada": regra por nível vista por quem não é admin. As triplas de
    // "equipe não faz etapa" (Motor v4, uma Nega por nível) também escondem o nível, mas não são
    // "regra base" — elas não revelam cargo (valem pros três níveis) e o front as mostra como uma
    // linha "Equipe X não faz <etapa>".
    public bool RegraBase => NivelOculto && Regra.Alvo is not AlvoAlcada.PorEquipeEEtapa;
}
