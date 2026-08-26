namespace Dispatch.Domain;

public sealed class Usuario
{
    public Guid Id { get; }
    public string Nome { get; }
    public string Email { get; }
    public string SenhaHash { get; }
    public Papel Papel { get; }
    public bool Ativo { get; }

    public Usuario(Guid id, string nome, string email, string senhaHash, Papel papel, bool ativo = true)
    {
        Id = id;
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Papel = papel;
        Ativo = ativo;
    }
}
