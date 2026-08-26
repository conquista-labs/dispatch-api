namespace Dispatch.Domain;

// Classe, não record: Conferente tem identidade (o mesmo conferente pode mudar de carga
// ou sair da escala e continuar sendo "o mesmo"), diferente de um value object como TipoAto.
public sealed class Conferente
{
    public Guid Id { get; }
    public Nivel Nivel { get; }
    public bool NaEscala { get; }
    public int CargaAtual { get; }

    public Conferente(Guid id, Nivel nivel, bool naEscala, int cargaAtual)
    {
        Id = id;
        Nivel = nivel;
        NaEscala = naEscala;
        CargaAtual = cargaAtual;
    }
}
