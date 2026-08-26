namespace Dispatch.Domain;

// Classe, não record: Conferente tem identidade (o mesmo conferente pode mudar de carga
// ou sair da escala e continuar sendo "o mesmo"), diferente de um value object como TipoAto.
public sealed class Conferente
{
    public Guid Id { get; }
    public Guid UsuarioId { get; }
    public Nivel Nivel { get; private set; }
    public double JornadaHoras { get; private set; }
    public bool NaEscala { get; private set; }
    public int CargaAtual { get; }

    public Conferente(Guid id, Guid usuarioId, Nivel nivel, double jornadaHoras, bool naEscala, int cargaAtual)
    {
        Id = id;
        UsuarioId = usuarioId;
        Nivel = nivel;
        JornadaHoras = jornadaHoras;
        NaEscala = naEscala;
        CargaAtual = cargaAtual;
    }

    // RF-26: editar nível e jornada.
    public void AtualizarNivelEJornada(Nivel nivel, double jornadaHoras)
    {
        Nivel = nivel;
        JornadaHoras = jornadaHoras;
    }

    // RF-27: marcar presença na escala.
    public void MarcarPresenca(bool presente)
    {
        NaEscala = presente;
    }
}
