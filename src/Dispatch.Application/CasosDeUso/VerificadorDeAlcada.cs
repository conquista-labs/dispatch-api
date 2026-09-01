using Dispatch.Domain;

namespace Dispatch.Application;

// Reaproveitado por ObterMinhaFila (RF-19) e PegarProtocolo (RF-20) — "esse conferente pode
// pegar esse protocolo" é sempre a mesma pergunta: o caso (etapa + tipo + equipe do
// escrevente) resolvido pela cascata de camadas do motor v3 (ResolvedorAlcada).
internal static class VerificadorDeAlcada
{
    public static bool TemAlcada(
        Conferente conferente, Protocolo protocolo, TipoAto tipo, Guid? equipeDoEscreventeId, IReadOnlyCollection<RegraAlcada> regras)
    {
        var caso = new CasoAlcada(protocolo.Etapa, tipo, equipeDoEscreventeId);
        return ResolvedorAlcada.Resolver(conferente, caso, regras).Resultado == ResultadoAlcada.Permitido;
    }
}
