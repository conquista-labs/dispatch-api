using Dispatch.Domain;

namespace Dispatch.Application;

// Reaproveitado por ObterMinhaFila (RF-19) e PegarProtocolo (RF-20) — "esse conferente pode
// pegar esse protocolo" é sempre a mesma pergunta: etapa permitida E tipo permitido E equipe
// do escrevente permitida (RF-29a).
internal static class VerificadorDeAlcada
{
    public static bool TemAlcada(
        Conferente conferente, Protocolo protocolo, Guid? equipeDoEscreventeId, IReadOnlyCollection<RegraAlcada> regras)
    {
        var etapaPermitida = Permitido(conferente, new AlvoAlcada.PorEtapa(protocolo.Etapa), regras);
        if (!etapaPermitida)
        {
            return false;
        }

        if (!Permitido(conferente, new AlvoAlcada.PorEquipeDeEscrevente(equipeDoEscreventeId), regras))
        {
            return false;
        }

        return protocolo.TipoAtoId is { } tipoAtoId && Permitido(conferente, new AlvoAlcada.PorTipoAto(tipoAtoId), regras);
    }

    private static bool Permitido(Conferente conferente, AlvoAlcada alvo, IReadOnlyCollection<RegraAlcada> regras) =>
        ResolvedorAlcada.Resolver(conferente, alvo, regras).Resultado == ResultadoAlcada.Permitido;
}
