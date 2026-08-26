using Dispatch.Application;
using Microsoft.AspNetCore.Identity;

namespace Dispatch.Infrastructure;

// PasswordHasher<TUser> não usa a instância de usuário pra nada por padrão (é só um
// parâmetro genérico de extensibilidade) — por isso "object", com null!, em vez de Usuario aqui.
public sealed class HashDeSenhaAspNetCore : IHashDeSenha
{
    private static readonly PasswordHasher<object> Hasher = new();

    public string Hash(string senha) => Hasher.HashPassword(null!, senha);

    public bool Verificar(string senhaHash, string senhaInformada) =>
        Hasher.VerifyHashedPassword(null!, senhaHash, senhaInformada) != PasswordVerificationResult.Failed;
}
