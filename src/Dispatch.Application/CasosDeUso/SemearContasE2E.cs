using Dispatch.Domain;

namespace Dispatch.Application;

// Só existe pra suportar a suíte e2e do dispatch-web — nunca é registrado fora de Development
// (ver DevSeedEndpoints.cs/Program.cs). Garante que as poucas contas fixas que os specs usam
// pra logar existem, com senha e estado conhecidos, não importa o que já tinha no banco antes
// (seed antigo, clone de produção, banco vazio). Idempotente: cria quem não existe, reseta
// senha/bloqueio de quem já existe. Não cria protocolo/regra/nada além disso — cada spec
// continua responsável por criar e apagar o próprio dado de teste, esse caso de uso só garante
// o "chão" de login que antes dependia de o banco já ter as contas certas por acaso.
public sealed class SemearContasE2E(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    IHashDeSenha hashDeSenha,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public const string Senha = "Senha123!";

    public async Task ExecutarAsync(CancellationToken cancellationToken = default)
    {
        await GarantirContaDeGestaoAsync("Distribuidora Teste", "distribuidora@cartorio.com", Papel.Distribuidora, cancellationToken);
        // Perfil Administrador (ADR-0039): conta própria, pra os testes cobrirem as duas visões —
        // a distribuidora continua distribuidora (combo com Conferente), sem cargo nem score.
        await GarantirContaDeGestaoAsync("Administrador Teste", "administrador@cartorio.com", Papel.Administrador, cancellationToken);
        await GarantirConferenteAsync("Conferente RF27", "conferente-rf27@cartorio.com", cancellationToken);
        await GarantirConferenteAsync("Conferente Visual", "conferente-visual@cartorio.com", cancellationToken);
        await unitOfWork.SalvarAsync(cancellationToken);
    }

    // Usuario.Papel não muda depois de criado — uma conta existente com esse e-mail e outro papel
    // fica como está (não acontece com as contas de teste, que só nascem aqui).
    private async Task GarantirContaDeGestaoAsync(string nome, string email, Papel papel, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        if (usuario is null)
        {
            usuarios.Adicionar(new Usuario(Guid.NewGuid(), nome, email, hashDeSenha.Hash(Senha), papel));
            return;
        }

        usuario.AtualizarPerfil(nome, email);
        usuario.RedefinirSenha(hashDeSenha.Hash(Senha), relogio.Agora);
        usuario.RegistrarLoginComSucesso();
    }

    private async Task GarantirConferenteAsync(string nome, string email, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        if (usuario is null)
        {
            usuario = new Usuario(Guid.NewGuid(), nome, email, hashDeSenha.Hash(Senha), Papel.Conferente);
            usuarios.Adicionar(usuario);
            conferentes.Adicionar(new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0));
            return;
        }

        usuario.AtualizarPerfil(nome, email);
        usuario.RedefinirSenha(hashDeSenha.Hash(Senha), relogio.Agora);
        usuario.RegistrarLoginComSucesso();

        var conferente = await conferentes.ObterPorUsuarioIdAsync(usuario.Id, cancellationToken);
        if (conferente is null)
        {
            conferentes.Adicionar(new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0));
        }
        else
        {
            conferente.MarcarPresenca(true);
        }
    }
}
