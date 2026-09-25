using System.Net;
using System.Net.Http.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

[Collection(IntegracaoCollection.Nome)]
public sealed class AutorizacaoIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    // RNF-04 ("a restrição entre papéis é sempre no servidor") passando pelo pipeline real de
    // autenticação/autorização — fake nenhum simula [Authorize]/RequireRole, e o front sozinho
    // nunca foi a garantia.
    [Fact]
    public async Task Conferente_NaoCadastraConferente()
    {
        var cliente = await AutenticarComoAsync(Papel.Conferente);

        var resposta = await cliente.PostAsJsonAsync("/conferentes", NovoConferente("conferente-bloqueado@cartorio.com"));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // Par do teste acima: prova que o 403 é sobre papel, não sobre um request malformado. Desde o
    // perfil Administrador (ADR-0039), cadastrar pessoas é só do admin.
    [Fact]
    public async Task Administrador_CadastraConferente()
    {
        var cliente = await AutenticarComoAsync(Papel.Administrador);

        var resposta = await cliente.PostAsJsonAsync("/conferentes", NovoConferente("conferente-novo@cartorio.com"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task SemToken_NaoPassaDoPortao()
    {
        var resposta = await CriarCliente().GetAsync("/conferentes");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    private static object NovoConferente(string email) => new
    {
        nome = "Conferente de Teste",
        email,
        senha = "Senha123!",
        nivel = nameof(Nivel.Pleno),
        jornadaHoras = 8,
    };
}
