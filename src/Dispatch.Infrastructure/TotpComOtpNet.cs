using System.Security.Cryptography;
using Dispatch.Application;
using OtpNet;

namespace Dispatch.Infrastructure;

// RFC 6238 de verdade via Otp.NET — nada de mock aqui (diferente do protótipo, que aceita
// qualquer código de 6 dígitos exceto "000000"). Issuer fixo "Dispatch", como no protótipo
// ("Conta: {{email}} · Dispatch").
public sealed class TotpComOtpNet : ITotp
{
    private const string Issuer = "Dispatch";

    public byte[] GerarSegredo() => RandomNumberGenerator.GetBytes(20);

    public string CodificarBase32(byte[] segredo) => Base32Encoding.ToString(segredo);

    public string MontarUriOtpAuth(byte[] segredo, string email)
    {
        var rotulo = Uri.EscapeDataString($"{Issuer}:{email}");
        var secret = Base32Encoding.ToString(segredo);
        return $"otpauth://totp/{rotulo}?secret={secret}&issuer={Issuer}&algorithm=SHA1&digits=6&period=30";
    }

    // RF-01e: janela de 1 bloco pra cada lado (±30s de tolerância de relógio), e barra reuso —
    // um código só vale se o bloco dele for mais novo que o último já aceito.
    public bool Validar(byte[] segredo, string codigo, long? ultimoContadorAceito, out long contador)
    {
        var totp = new Totp(segredo);
        var valido = totp.VerifyTotp(codigo, out var blocoUsado, new VerificationWindow(previous: 1, future: 1));
        contador = blocoUsado;

        if (!valido)
        {
            return false;
        }

        return ultimoContadorAceito is not { } ultimo || blocoUsado > ultimo;
    }
}
