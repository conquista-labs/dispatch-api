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
    public int CargaAtual { get; private set; }

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

    // Seção 11 do documento de requisitos: "ao distribuir um lote o motor considera a carga
    // acumulada dentro da própria rodada, e não apenas a carga já gravada". CargaAtual em si
    // nunca é persistido (é sempre recalculado na leitura, ver CLAUDE.md) — este incremento é
    // só pra este objeto em memória continuar refletindo a carga real enquanto a mesma rodada
    // de distribuição (lote de importação ou redistribuição de pool) segue atribuindo mais
    // protocolos à mesma pessoa, sem precisar reconsultar o banco a cada atribuição.
    public void IncrementarCargaAtual() => CargaAtual++;
}
