using Dispatch.Domain;

namespace Dispatch.Application;

// RF-45 (perfil Administrador, ADR-0039): criar conta de gestão — Distribuidora ou Administrador.
// Conferente não se cria aqui (continua em Conferentes, que cria o Conferente junto). A pessoa entra
// com a senha inicial e é obrigada a trocar no primeiro acesso (ADR-0040).
public sealed class CriarConta(IUsuarioRepository usuarios, IHashDeSenha hashDeSenha, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCriarConta> ExecutarAsync(
        string nome, string email, string senhaInicial, Papel papel, CancellationToken cancellationToken = default)
    {
        if (papel is not (Papel.Distribuidora or Papel.Administrador))
        {
            return new ResultadoCriarConta.PapelInvalido();
        }

        nome = nome.Trim();
        email = email.Trim();
        if (nome.Length == 0 || !EmailParece(email))
        {
            return new ResultadoCriarConta.DadosInvalidos();
        }

        if (!RegrasDeSenha.ServeComoSenhaInicial(senhaInicial))
        {
            return new ResultadoCriarConta.SenhaInicialCurta();
        }

        if (await usuarios.ExisteComEmailAsync(email, cancellationToken))
        {
            return new ResultadoCriarConta.EmailJaCadastrado();
        }

        var usuario = new Usuario(Guid.NewGuid(), nome, email, hashDeSenha.Hash(senhaInicial), papel);
        usuario.ExigirTrocaDeSenha();
        usuarios.Adicionar(usuario);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoCriarConta.Sucesso(usuario.Id);
    }

    // Validação de formato só pro erro óbvio de digitação (o protótipo: "Esse e-mail não parece
    // válido"); quem garante que o e-mail é da pessoa é quem cria a conta.
    private static bool EmailParece(string email)
    {
        var arroba = email.IndexOf('@');
        return arroba > 0 && arroba < email.Length - 1 && email.IndexOf('.', arroba) > arroba + 1 && !email.Contains(' ');
    }
}

public abstract record ResultadoCriarConta
{
    private ResultadoCriarConta() { }

    public sealed record Sucesso(Guid UsuarioId) : ResultadoCriarConta;

    public sealed record PapelInvalido : ResultadoCriarConta;

    public sealed record DadosInvalidos : ResultadoCriarConta;

    public sealed record SenhaInicialCurta : ResultadoCriarConta;

    public sealed record EmailJaCadastrado : ResultadoCriarConta;
}
