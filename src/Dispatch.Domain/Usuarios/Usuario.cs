namespace Dispatch.Domain;

public sealed class Usuario
{
    public Guid Id { get; }
    public string Nome { get; }
    public string Email { get; }
    public string SenhaHash { get; }
    public Papel Papel { get; }
    public bool Ativo { get; private set; }

    public Usuario(Guid id, string nome, string email, string senhaHash, Papel papel, bool ativo = true)
    {
        Id = id;
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Papel = papel;
        Ativo = ativo;
    }

    // RF-25 "remover": soft delete, não apaga a linha — mantém rastro de quem conferiu o quê.
    public void Desativar() => Ativo = false;
}
