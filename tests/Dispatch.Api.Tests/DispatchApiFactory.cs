using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Dispatch.Api.Tests;

// Sobe a API inteira em memória (Program.cs de verdade: DI, autenticação JWT, todos os
// endpoints) apontando pro Postgres do Testcontainers. Bater HTTP aqui exercita, de uma vez,
// Api → Application → Domain → EF Core → Postgres real — inclusive FK, CHECK e change tracker,
// que é justamente o que as fakes de Dispatch.Application.Tests não conseguem cobrir.
public sealed class DispatchApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development não é detalhe: POST /dev/seed-e2e (como os testes autenticam) só é
        // mapeado nesse ambiente, e appsettings.Development.json traz Jwt/Cors/Totp — sem ele
        // o Program.cs nem sobe (lança na ausência de Cors:AllowedOrigin).
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DispatchDb"] = connectionString,
            }));
    }
}
