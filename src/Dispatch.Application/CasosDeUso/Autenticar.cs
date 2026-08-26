namespace Dispatch.Application;

public sealed class Autenticar(IUsuarioRepository usuarios, IHashDeSenha hashDeSenha, IEmissorDeToken emissorDeToken)
{
    public async Task<ResultadoAutenticacao> ExecutarAsync(string email, string senha, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);

        if (usuario is null || !usuario.Ativo || !hashDeSenha.Verificar(usuario.SenhaHash, senha))
        {
            return new ResultadoAutenticacao.Rejeitado();
        }

        return new ResultadoAutenticacao.Autenticado(emissorDeToken.EmitirToken(usuario));
    }
}
