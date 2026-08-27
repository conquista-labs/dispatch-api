namespace Dispatch.Domain;

// Não usa o sistema (ver glossário, seção 2) — é só o dado de entrada que identifica a equipe.
public sealed class Escrevente
{
    public Guid Id { get; }
    public string Nome { get; }
    public Guid? EquipeId { get; private set; }

    public Escrevente(Guid id, string nome, Guid? equipeId)
    {
        Id = id;
        Nome = nome;
        EquipeId = equipeId;
    }

    // RF-35: mover escrevente entre equipes (ou tirar da equipe, com null).
    public void MoverParaEquipe(Guid? equipeId) => EquipeId = equipeId;
}
