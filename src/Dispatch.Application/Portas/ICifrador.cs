namespace Dispatch.Application;

// RNF-15: segredo TOTP cifrado em repouso, chave fora do banco (config/secret, nunca coluna).
// Só o suficiente pra ida-e-volta de bytes — não é um KMS, é AES simétrico com chave fixa de
// configuração, adequado ao volume e ao risco deste sistema.
public interface ICifrador
{
    string Cifrar(byte[] dados);
    byte[] Decifrar(string textoCifrado);
}
