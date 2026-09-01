using Dispatch.Domain;

namespace Dispatch.Application;

// RF-01g etapa 1 / RF-01h: sempre "sucesso" pro chamador, sem exceção — e-mail inexistente ou
// sem autenticador confirmado não gera nenhuma resposta diferente (anti-enumeração, mesmo
// espírito de ResultadoAutenticacao.Rejeitado não distinguir e-mail de senha errados). Por isso
// não devolve nada: não há decisão nenhuma pro chamador tomar a partir do resultado.
public sealed class IniciarRecuperacaoSenha(
    IUsuarioRepository usuarios,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task ExecutarAsync(string email, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        if (usuario is null)
        {
            return;
        }

        eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuario.Id, TipoEventoAutenticacao.RecuperacaoIniciada, relogio.Agora));
        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
