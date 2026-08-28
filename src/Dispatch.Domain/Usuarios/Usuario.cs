namespace Dispatch.Domain;

public sealed class Usuario
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public string Email { get; private set; }
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

    // RF-25 "editar" — nome/e-mail são do Usuario, não do Conferente (que só sabe nível/jornada/
    // escala). Unicidade de e-mail é responsabilidade de quem chama isso (precisa checar contra
    // o repositório, o Domain não tem visão do resto da base).
    public void AtualizarPerfil(string nome, string email)
    {
        Nome = nome;
        Email = email;
    }
}
