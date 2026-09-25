using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class Autenticar(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    IHashDeSenha hashDeSenha,
    IEmissorDeToken emissorDeToken,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoAutenticacao> ExecutarAsync(
        string email, string senha, string? origem, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        var agora = relogio.Agora;

        // Mesmo raciocínio de RF-01h (anti-enumeração): usuário inexistente não tem o que
        // bloquear nem registrar — só quem existe de verdade grava evento e conta tentativa.
        if (usuario is not null && usuario.EstaBloqueado(agora))
        {
            eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuario.Id, TipoEventoAutenticacao.LoginBloqueado, agora, origem));
            await unitOfWork.SalvarAsync(cancellationToken);
            return new ResultadoAutenticacao.Rejeitado();
        }

        if (usuario is null || !usuario.Ativo || !hashDeSenha.Verificar(usuario.SenhaHash, senha))
        {
            if (usuario is not null)
            {
                usuario.RegistrarTentativaLoginFalha(agora);
                eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuario.Id, TipoEventoAutenticacao.LoginFalhou, agora, origem));
                await unitOfWork.SalvarAsync(cancellationToken);
            }

            return new ResultadoAutenticacao.Rejeitado();
        }

        usuario.RegistrarLoginComSucesso();
        await unitOfWork.SalvarAsync(cancellationToken);

        var papeis = await PapeisEfetivos.ObterAsync(usuario, conferentes, cancellationToken);
        return new ResultadoAutenticacao.Autenticado(
            emissorDeToken.EmitirToken(usuario, papeis), usuario.Id, usuario.Nome, usuario.Email, papeis,
            usuario.TrocarSenhaNoProximoAcesso);
    }
}
