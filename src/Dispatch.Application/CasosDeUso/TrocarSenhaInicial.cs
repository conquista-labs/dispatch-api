using Dispatch.Domain;

namespace Dispatch.Application;

// RF-45 / ADR-0040: a troca obrigatória do primeiro acesso (e a troca de senha com a sessão
// aberta, em geral). Confere a senha atual, exige a regra forte (RF-01j), grava — o que encerra
// as sessões anteriores (RedefinirSenha bumpa SessoesValidasApartirDe) e desliga a troca
// pendente — e devolve um token novo, já sem a claim de troca, pra a pessoa seguir sem logar de novo.
public sealed class TrocarSenhaInicial(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    IHashDeSenha hashDeSenha,
    IEmissorDeToken emissorDeToken,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoTrocarSenhaInicial> ExecutarAsync(
        Guid usuarioId, string senhaAtual, string novaSenha, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Ativo)
        {
            return new ResultadoTrocarSenhaInicial.NaoEncontrado();
        }

        if (!hashDeSenha.Verificar(usuario.SenhaHash, senhaAtual))
        {
            return new ResultadoTrocarSenhaInicial.SenhaAtualIncorreta();
        }

        if (!RegrasDeSenha.EhForte(novaSenha))
        {
            return new ResultadoTrocarSenhaInicial.SenhaFraca();
        }

        usuario.RedefinirSenha(hashDeSenha.Hash(novaSenha), relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);

        var papeis = await PapeisEfetivos.ObterAsync(usuario, conferentes, cancellationToken);
        return new ResultadoTrocarSenhaInicial.Sucesso(emissorDeToken.EmitirToken(usuario, papeis));
    }
}

public abstract record ResultadoTrocarSenhaInicial
{
    private ResultadoTrocarSenhaInicial() { }

    public sealed record Sucesso(string Token) : ResultadoTrocarSenhaInicial;

    public sealed record NaoEncontrado : ResultadoTrocarSenhaInicial;

    public sealed record SenhaAtualIncorreta : ResultadoTrocarSenhaInicial;

    public sealed record SenhaFraca : ResultadoTrocarSenhaInicial;
}
