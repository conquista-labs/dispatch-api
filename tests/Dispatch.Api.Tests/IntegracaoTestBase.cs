using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dispatch.Domain;
using Dispatch.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatch.Api.Tests;

public abstract class IntegracaoTestBase(IntegracaoFixture fixture) : IAsyncLifetime
{
    private const string EmailDistribuidora = "distribuidora@cartorio.com";
    private const string EmailConferente = "conferente-rf27@cartorio.com";
    private const string EmailAdministrador = "administrador@cartorio.com";

    protected IntegracaoFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient CriarCliente() => Fixture.Factory.CreateClient();

    // Reaproveita POST /dev/seed-e2e (o mesmo endpoint que o globalSetup do Playwright do
    // dispatch-web já usa) em vez de inventar um caminho de autenticação só pros testes —
    // login aqui é o fluxo real, com hash de senha e JWT de verdade.
    //
    // Semeia UMA vez por teste (xUnit cria uma instância por teste, e o banco é zerado no
    // InitializeAsync): o seed redefine a senha das contas, e RedefinirSenha encerra as sessões
    // emitidas antes (SessoesValidasApartirDe, truncado ao segundo). Re-semear a cada chamada fazia o
    // token de um cliente autenticado antes cair em 401 sempre que um segundo virava entre os dois
    // logins — flake real em DashboardIntegracaoTests e PainelDeHojeIntegracaoTests.
    private bool _contasSemeadas;

    protected async Task<HttpClient> AutenticarComoAsync(Papel papel)
    {
        var cliente = CriarCliente();

        if (!_contasSemeadas)
        {
            var seed = await cliente.PostAsync("/dev/seed-e2e", content: null);
            seed.EnsureSuccessStatusCode();
            _contasSemeadas = true;
        }

        var email = papel switch
        {
            Papel.Distribuidora => EmailDistribuidora,
            Papel.Administrador => EmailAdministrador,
            _ => EmailConferente,
        };
        return await LogarAsync(cliente, email, SenhaDeTeste);
    }

    // Login sem re-semear — pra contas criadas pelo próprio teste (ex.: uma conta nova via /contas).
    protected async Task<HttpClient> LogarAsync(HttpClient cliente, string email, string senha)
    {
        var login = await cliente.PostAsJsonAsync("/auth/login", new { email, senha });
        login.EnsureSuccessStatusCode();

        var autenticado = await login.Content.ReadFromJsonAsync<RespostaLogin>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", autenticado!.Token);
        return cliente;
    }

    // Mesma senha fixa de SemearContasE2E (Dispatch.Application) — as contas semeadas por
    // /dev/seed-e2e nascem com ela.
    private const string SenhaDeTeste = "Senha123!";

    // Assertiva contra o banco de verdade, não só contra a resposta HTTP — é o ponto de existir
    // um teste de integração em vez de mais um teste com fake.
    protected async Task NoBancoAsync(Func<DispatchDbContext, Task> assercao)
    {
        using var escopo = Fixture.Factory.Services.CreateScope();
        await assercao(escopo.ServiceProvider.GetRequiredService<DispatchDbContext>());
    }

    private sealed record RespostaLogin(string Token);
}
