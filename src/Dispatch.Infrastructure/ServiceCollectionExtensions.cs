using Dispatch.Application;
using Dispatch.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatch.Infrastructure;

// Ponto único onde "quando alguém pedir a interface X, entregue a implementação Y" é
// declarado — o Program.cs só chama isso, não conhece as classes concretas de dentro.
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DispatchDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DispatchDb"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IConferenteRepository, ConferenteRepository>();
        services.AddScoped<IEquipeRepository, EquipeRepository>();
        services.AddScoped<IRegraAlcadaRepository, RegraAlcadaRepository>();
        services.AddScoped<ITipoAtoRepository, TipoAtoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IProtocoloRepository, ProtocoloRepository>();
        services.AddScoped<IEscreventeRepository, EscreventeRepository>();
        services.AddScoped<ILoteImportacaoRepository, LoteImportacaoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWorkEfCore>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IHashDeSenha, HashDeSenhaAspNetCore>();
        services.AddScoped<IEmissorDeToken, EmissorDeTokenJwt>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddScoped<DistribuirProtocolo>();
        services.AddScoped<Autenticar>();
        services.AddScoped<CadastrarConferente>();
        services.AddScoped<EditarNivelEJornada>();
        services.AddScoped<MarcarPresenca>();
        services.AddScoped<RemoverConferente>();
        services.AddScoped<ImportarLote>();
        services.AddScoped<ObterVisaoDistribuicao>();

        return services;
    }
}
