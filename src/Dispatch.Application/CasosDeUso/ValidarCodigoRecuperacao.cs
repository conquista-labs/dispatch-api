using System.Security.Cryptography;
using Dispatch.Domain;

namespace Dispatch.Application;

// RF-01g etapa 2 / RF-01h / RF-01i: valida o código TOTP como prova de identidade. E-mail
// inexistente e código errado devolvem exatamente o mesmo resultado (CodigoInvalido) — nunca
// dá pra saber, pela resposta, se a conta existe.
public sealed class ValidarCodigoRecuperacao(
    IUsuarioRepository usuarios,
    IUsuarioTotpRepository usuariosTotp,
    ITotp totp,
    ICifrador cifrador,
    IHashDeSenha hashDeSenha,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoValidarCodigoRecuperacao> ExecutarAsync(string email, string codigo, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        var usuarioTotp = usuario is null ? null : await usuariosTotp.ObterPorUsuarioIdAsync(usuario.Id, cancellationToken);

        if (usuario is null || usuarioTotp is null || usuarioTotp.ConfirmadoEm is null)
        {
            return new ResultadoValidarCodigoRecuperacao.CodigoInvalido();
        }

        if (usuarioTotp.BloqueadoAte is { } bloqueadoAte && bloqueadoAte > relogio.Agora)
        {
            return new ResultadoValidarCodigoRecuperacao.Bloqueado(bloqueadoAte);
        }

        var segredo = cifrador.Decifrar(usuarioTotp.SegredoCifrado);
        if (!totp.Validar(segredo, codigo, usuarioTotp.UltimoContadorAceito, out var contador))
        {
            usuarioTotp.RegistrarTentativaFalha(relogio.Agora);
            var tipoEvento = usuarioTotp.BloqueadoAte is not null
                ? TipoEventoAutenticacao.RecuperacaoContaBloqueada
                : TipoEventoAutenticacao.RecuperacaoCodigoFalhou;
            eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuario.Id, tipoEvento, relogio.Agora));
            await unitOfWork.SalvarAsync(cancellationToken);

            return usuarioTotp.BloqueadoAte is { } novoBloqueio
                ? new ResultadoValidarCodigoRecuperacao.Bloqueado(novoBloqueio)
                : new ResultadoValidarCodigoRecuperacao.CodigoInvalido();
        }

        var tokenBruto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        usuarioTotp.RegistrarSucesso(contador);
        usuarioTotp.EmitirTokenRecuperacao(hashDeSenha.Hash(tokenBruto), relogio.Agora.AddMinutes(10));

        eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuario.Id, TipoEventoAutenticacao.RecuperacaoCodigoValidado, relogio.Agora));
        await unitOfWork.SalvarAsync(cancellationToken);

        // Token opaco pro cliente = usuarioId + segredo aleatório — dá pra achar o UsuarioTotp
        // de novo na etapa 3 sem precisar consultar por hash (PasswordHasher salga, não dá pra
        // indexar/comparar isso numa query). O UsuarioId sozinho não é segredo (front já sabe o
        // e-mail nesse ponto do fluxo); quem garante que a posse do token é legítima é o hash.
        return new ResultadoValidarCodigoRecuperacao.TokenEmitido($"{usuario.Id:N}.{tokenBruto}");
    }
}

public abstract record ResultadoValidarCodigoRecuperacao
{
    private ResultadoValidarCodigoRecuperacao() { }

    public sealed record TokenEmitido(string Token) : ResultadoValidarCodigoRecuperacao;
    public sealed record CodigoInvalido : ResultadoValidarCodigoRecuperacao;
    public sealed record Bloqueado(DateTimeOffset BloqueadoAte) : ResultadoValidarCodigoRecuperacao;
}
