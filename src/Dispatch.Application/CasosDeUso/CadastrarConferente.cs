using Dispatch.Domain;

namespace Dispatch.Application;

// RF-25 (cadastrar) + RF-26 (nível/jornada). Cadastrar um conferente sempre cria também o
// Usuario por trás (login) — não existe conferente sem credencial, e o papel nunca é
// escolhido no login (seção 3), então já nasce fixo como Papel.Conferente aqui.
public sealed class CadastrarConferente(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    IHashDeSenha hashDeSenha,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCadastroConferente> ExecutarAsync(
        string nome,
        string email,
        string senha,
        Nivel nivel,
        double jornadaHoras,
        CancellationToken cancellationToken = default)
    {
        // Antes, qualquer senha passava (o front pedia 6 caracteres). Agora é a mesma regra da
        // senha inicial de Contas (RF-45): 8+, e troca obrigatória no primeiro acesso.
        if (!RegrasDeSenha.ServeComoSenhaInicial(senha))
        {
            return new ResultadoCadastroConferente.SenhaInicialCurta();
        }

        if (await usuarios.ExisteComEmailAsync(email, cancellationToken))
        {
            return new ResultadoCadastroConferente.EmailJaCadastrado();
        }

        var usuario = new Usuario(Guid.NewGuid(), nome, email, hashDeSenha.Hash(senha), Papel.Conferente);
        usuario.ExigirTrocaDeSenha();
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, nivel, jornadaHoras, naEscala: true, cargaAtual: 0);

        usuarios.Adicionar(usuario);
        conferentes.Adicionar(conferente);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoCadastroConferente.Sucesso(conferente.Id);
    }
}
