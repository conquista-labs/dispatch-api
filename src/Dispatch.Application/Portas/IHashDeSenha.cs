namespace Dispatch.Application;

// O algoritmo de hash em si (PBKDF2, bcrypt etc.) é detalhe de segurança/infraestrutura,
// não regra de negócio — por isso é porta, com a implementação real em Infrastructure.
public interface IHashDeSenha
{
    string Hash(string senha);
    bool Verificar(string senhaHash, string senhaInformada);
}
