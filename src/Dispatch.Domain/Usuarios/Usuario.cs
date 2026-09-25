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

    // Bloqueio de login por senha errada — mesmo mecanismo já usado pro código TOTP
    // (UsuarioTotp.TentativasFalhas/BloqueadoAte, RF-01i), só que aqui vale pra QUALQUER
    // usuário (login por senha não depende de ter registrado autenticador) — por isso mora em
    // Usuario, não em UsuarioTotp (que só existe depois de RegistrarTotp rodar).
    public int TentativasLoginFalhas { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }

    // RF-45 (perfil Administrador): conta criada por outra pessoa (Contas, ou um conferente
    // cadastrado) entra com uma senha inicial e troca no primeiro acesso. Enquanto ligado, o token
    // só vale pra trocar a senha (ADR-0040). Qualquer troca de senha desliga.
    public bool TrocarSenhaNoProximoAcesso { get; private set; }

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

    public void ExigirTrocaDeSenha() => TrocarSenhaNoProximoAcesso = true;

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
        TrocarSenhaNoProximoAcesso = false;
        SessoesValidasApartirDe = agora.AddTicks(-(agora.Ticks % TimeSpan.TicksPerSecond));
    }

    public bool EstaBloqueado(DateTimeOffset agora) => BloqueadoAte is { } ate && ate > agora;

    // 5 tentativas erradas bloqueiam por 15 minutos — mesmos números de RF-01i (código TOTP),
    // mesmo raciocínio de segurança, agora pro login em si.
    public void RegistrarTentativaLoginFalha(DateTimeOffset agora)
    {
        TentativasLoginFalhas++;
        if (TentativasLoginFalhas >= 5)
        {
            BloqueadoAte = agora.AddMinutes(15);
        }
    }

    public void RegistrarLoginComSucesso()
    {
        TentativasLoginFalhas = 0;
        BloqueadoAte = null;
    }
}
