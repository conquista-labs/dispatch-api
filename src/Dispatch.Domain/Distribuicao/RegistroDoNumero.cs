namespace Dispatch.Domain;

// Recorte leve de um Protocolo — só o que as regras de "mesmo Número" precisam olhar. Existe pra
// que uma listagem inteira possa buscar o histórico dos seus números numa query só, projetada,
// sem carregar as coleções filhas (ciclos, pausas, ajustes) de cada linha.
public sealed record RegistroDoNumero(
    Guid Id,
    string Numero,
    Etapa Etapa,
    DateTimeOffset AndamentoEm,
    StatusProtocolo Status)
{
    public static RegistroDoNumero De(Protocolo protocolo) =>
        new(protocolo.Id, protocolo.Numero, protocolo.Etapa, protocolo.AndamentoEm, protocolo.Status);
}
