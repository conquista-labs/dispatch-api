namespace Dispatch.Application;

// RFC 6238 (TOTP) é detalhe de infraestrutura (biblioteca externa), não regra de negócio — só a
// porta mora aqui, mesmo raciocínio de IHashDeSenha.
public interface ITotp
{
    // 20 bytes aleatórios (RFC 4226 recomenda pelo menos 160 bits).
    byte[] GerarSegredo();

    // Base32 é o formato padrão de exibição (RF-01a: "chave em blocos de 4 caracteres") — a
    // codificação em si é detalhe da biblioteca de TOTP, não decidimos reimplementar aqui.
    string CodificarBase32(byte[] segredo);

    // otpauth://totp/Dispatch:{email}?secret=...&issuer=Dispatch — o app autenticador lê isso
    // direto do QR (RF-01b).
    string MontarUriOtpAuth(byte[] segredo, string email);

    // RF-01e: aceita o bloco atual e um de tolerância pra cada lado. `ultimoContadorAceito`
    // barra reuso (um código já usado, ou mais antigo que o último aceito, é sempre inválido —
    // RF-01e "só serve uma vez"). `contador` só é significativo quando o retorno é true.
    bool Validar(byte[] segredo, string codigo, long? ultimoContadorAceito, out long contador);
}
