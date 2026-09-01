using Dispatch.Domain;

namespace Dispatch.Application;

// RF-01a-c: gera um segredo novo, cifra e grava (pendente de confirmação), devolve o segredo em
// claro (Base32) e a URI otpauth:// — a ÚNICA vez que o segredo aparece fora do banco cifrado.
public sealed class RegistrarTotp(
    IUsuarioRepository usuarios,
    IUsuarioTotpRepository usuariosTotp,
    ITotp totp,
    ICifrador cifrador,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoRegistrarTotp?> ExecutarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return null;
        }

        var segredo = totp.GerarSegredo();
        var segredoCifrado = cifrador.Cifrar(segredo);

        var existente = await usuariosTotp.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
        if (existente is null)
        {
            usuariosTotp.Adicionar(new UsuarioTotp(usuarioId, segredoCifrado, relogio.Agora));
        }
        else
        {
            existente.IniciarRegistro(segredoCifrado);
        }

        eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuarioId, TipoEventoAutenticacao.RegistroTotpIniciado, relogio.Agora));
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoRegistrarTotp(totp.CodificarBase32(segredo), totp.MontarUriOtpAuth(segredo, usuario.Email));
    }
}

public sealed record ResultadoRegistrarTotp(string ChaveBase32, string UriOtpAuth);
