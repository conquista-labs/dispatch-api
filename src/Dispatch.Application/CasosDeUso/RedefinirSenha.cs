using Dispatch.Domain;

namespace Dispatch.Application;

// RF-01g etapa 3 / RF-01j / RF-01k: fecha a recuperação. Só chega aqui quem passou pela etapa 2
// (token de posse comprovada) — troca a senha, encerra todas as sessões (Usuario.RedefinirSenha
// já bumpa o carimbo), devolve pro pool os atos que estavam em conferência com o usuário, e
// consome o token (uso único).
public sealed class RedefinirSenha(
    IUsuarioRepository usuarios,
    IUsuarioTotpRepository usuariosTotp,
    IConferenteRepository conferentes,
    IProtocoloRepository protocolos,
    IHashDeSenha hashDeSenha,
    IEventoAutenticacaoRepository eventos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoRedefinirSenha> ExecutarAsync(string tokenRecuperacao, string novaSenha, CancellationToken cancellationToken = default)
    {
        var partes = tokenRecuperacao.Split('.', 2);
        if (partes.Length != 2 || !Guid.TryParse(partes[0], out var usuarioId))
        {
            return ResultadoRedefinirSenha.TokenInvalido;
        }

        var usuario = await usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        var usuarioTotp = await usuariosTotp.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);

        var tokenValido = usuario is not null && usuarioTotp is { TokenRecuperacaoHash: not null } &&
            usuarioTotp.TokenRecuperacaoExpiraEm > relogio.Agora &&
            hashDeSenha.Verificar(usuarioTotp.TokenRecuperacaoHash, partes[1]);

        if (!tokenValido)
        {
            return ResultadoRedefinirSenha.TokenInvalido;
        }

        if (!RegrasDeSenha.EhForte(novaSenha))
        {
            return ResultadoRedefinirSenha.SenhaFraca;
        }

        usuario!.RedefinirSenha(hashDeSenha.Hash(novaSenha), relogio.Agora);
        usuarioTotp!.ConsumirTokenRecuperacao();

        var conferente = await conferentes.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
        if (conferente is not null)
        {
            foreach (var protocolo in await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken))
            {
                protocolo.EnviarParaPool();
            }
        }

        eventos.Adicionar(new EventoAutenticacao(Guid.NewGuid(), usuarioId, TipoEventoAutenticacao.SenhaRedefinida, relogio.Agora));
        await unitOfWork.SalvarAsync(cancellationToken);

        return ResultadoRedefinirSenha.Sucesso;
    }
}

public enum ResultadoRedefinirSenha
{
    Sucesso,
    TokenInvalido,
    SenhaFraca
}
