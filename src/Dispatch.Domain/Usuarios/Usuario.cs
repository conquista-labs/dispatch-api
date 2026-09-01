namespace Dispatch.Domain;

public sealed class Usuario
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public string Email { get; private set; }
    public string SenhaHash { get; private set; }
    public Papel Papel { get; }
    public bool Ativo { get; private set; }

    // RF-01k: "carimbo de segurança" — todo JWT emitido antes deste instante deixa de valer.
    // MinValue por padrão (sem novo parâmetro de ctor, sem quebrar os call sites existentes):
    // usuário nunca trocou a senha, então nenhum token real jamais teria IssuedAt anterior a
    // isso. Só passa a importar de verdade depois da primeira troca de senha.
    public DateTimeOffset SessoesValidasApartirDe { get; private set; } = DateTimeOffset.MinValue;

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

    // RF-01j/RF-01k: troca de senha (recuperação) sempre encerra qualquer sessão emitida antes
    // dela — daí o bump do carimbo junto, não é opcional. Truncado pro segundo: o "iat" de um
    // JWT só tem precisão de segundo (NumericDate, RFC 7519) — guardar milissegundos aqui criaria
    // uma janela de até 999ms em que um login legítimo, no mesmíssimo segundo da troca, seria
    // rejeitado à toa por IssuedAt < SessoesValidasApartirDe.
    public void RedefinirSenha(string novoHash, DateTimeOffset agora)
    {
        SenhaHash = novoHash;
        SessoesValidasApartirDe = agora.AddTicks(-(agora.Ticks % TimeSpan.TicksPerSecond));
    }
}
