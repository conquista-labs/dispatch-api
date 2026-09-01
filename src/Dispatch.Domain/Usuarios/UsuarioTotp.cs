namespace Dispatch.Domain;

// RF-01a-RF-01l: registro de autenticador (TOTP, RFC 6238) e o estado de recuperação de senha
// que depende dele — 1:1 com Usuario (Id = UsuarioId, sem chave própria). Mapeamento direto,
// como Conferente/Escrevente — não é um sum type, não precisa de Registro achatado.
public sealed class UsuarioTotp
{
    public Guid UsuarioId { get; }
    public string SegredoCifrado { get; private set; }
    public DateTimeOffset? ConfirmadoEm { get; private set; }

    // Contador RFC 6238 (bloco de 30s) do último código aceito — impede reaproveitar o mesmo
    // código (ou um mais antigo) numa segunda tentativa (RF-01e: "só serve uma vez").
    public long? UltimoContadorAceito { get; private set; }

    // RF-01i: 5 tentativas erradas de código bloqueiam por 15 minutos.
    public int TentativasFalhas { get; private set; }
    public DateTimeOffset? BloqueadoAte { get; private set; }

    // Token de recuperação (etapa 2 → etapa 3): nunca guardamos o token em claro, só o hash
    // (mesmo IHashDeSenha da senha) — mesmo raciocínio de nunca guardar senha em claro.
    public string? TokenRecuperacaoHash { get; private set; }
    public DateTimeOffset? TokenRecuperacaoExpiraEm { get; private set; }

    public DateTimeOffset CriadoEm { get; }

    public UsuarioTotp(Guid usuarioId, string segredoCifrado, DateTimeOffset criadoEm)
    {
        UsuarioId = usuarioId;
        SegredoCifrado = segredoCifrado;
        CriadoEm = criadoEm;
    }

    // RF-01a/RF-01d: (re)iniciar o registro sempre substitui qualquer segredo anterior —
    // confirmado ou não — e exige nova confirmação. Nunca fica um segredo velho confirmado
    // valendo em paralelo com um novo pendente.
    public void IniciarRegistro(string segredoCifrado)
    {
        SegredoCifrado = segredoCifrado;
        ConfirmadoEm = null;
        UltimoContadorAceito = null;
    }

    public void ConfirmarRegistro(long contador, DateTimeOffset agora)
    {
        ConfirmadoEm = agora;
        UltimoContadorAceito = contador;
    }

    public void RegistrarTentativaFalha(DateTimeOffset agora)
    {
        TentativasFalhas++;
        if (TentativasFalhas >= 5)
        {
            BloqueadoAte = agora.AddMinutes(15);
        }
    }

    public void RegistrarSucesso(long contador)
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        UltimoContadorAceito = contador;
    }

    public void EmitirTokenRecuperacao(string hash, DateTimeOffset expiraEm)
    {
        TokenRecuperacaoHash = hash;
        TokenRecuperacaoExpiraEm = expiraEm;
    }

    // Uso único — depois de consumido (senha redefinida com sucesso), o mesmo token não serve
    // de novo.
    public void ConsumirTokenRecuperacao()
    {
        TokenRecuperacaoHash = null;
        TokenRecuperacaoExpiraEm = null;
    }
}
