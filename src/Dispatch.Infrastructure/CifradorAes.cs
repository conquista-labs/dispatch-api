using System.Security.Cryptography;
using Dispatch.Application;
using Microsoft.Extensions.Options;

namespace Dispatch.Infrastructure;

// AES-CBC com IV aleatório por chamada (guardado junto do texto cifrado, prática padrão — IV
// não é segredo, só não pode repetir). Chave vem de TotpOptions (RNF-15: fora do banco).
public sealed class CifradorAes(IOptions<TotpOptions> opcoes) : ICifrador
{
    private readonly byte[] _chave = Convert.FromBase64String(opcoes.Value.ChaveDeCifragem);

    public string Cifrar(byte[] dados)
    {
        using var aes = Aes.Create();
        aes.Key = _chave;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cifrado = encryptor.TransformFinalBlock(dados, 0, dados.Length);

        return Convert.ToBase64String([.. aes.IV, .. cifrado]);
    }

    public byte[] Decifrar(string textoCifrado)
    {
        var bytes = Convert.FromBase64String(textoCifrado);

        using var aes = Aes.Create();
        aes.Key = _chave;
        aes.IV = bytes[..16];

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(bytes, 16, bytes.Length - 16);
    }
}
