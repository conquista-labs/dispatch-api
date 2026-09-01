using Dispatch.Domain;

namespace Dispatch.Application;

// RF-01d: fecha o registro — só depois de confirmar um código válido é que o autenticador passa
// a valer de verdade pra recuperação de senha.
public sealed class ConfirmarRegistroTotp(
    IUsuarioTotpRepository usuariosTotp,
    ITotp totp,
    ICifrador cifrador,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoConfirmarTotp> ExecutarAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken = default)
    {
        var usuarioTotp = await usuariosTotp.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
        if (usuarioTotp is null)
        {
            return ResultadoConfirmarTotp.SemRegistroPendente;
        }

        var segredo = cifrador.Decifrar(usuarioTotp.SegredoCifrado);
        if (!totp.Validar(segredo, codigo, ultimoContadorAceito: null, out var contador))
        {
            return ResultadoConfirmarTotp.CodigoInvalido;
        }

        usuarioTotp.ConfirmarRegistro(contador, relogio.Agora);
        eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuarioId, TipoEventoAutenticacao.RegistroTotpConfirmado, relogio.Agora));
        await unitOfWork.SalvarAsync(cancellationToken);

        return ResultadoConfirmarTotp.Sucesso;
    }
}

public enum ResultadoConfirmarTotp
{
    Sucesso,
    SemRegistroPendente,
    CodigoInvalido
}
