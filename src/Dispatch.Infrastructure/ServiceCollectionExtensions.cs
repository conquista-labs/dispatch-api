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
        services.AddScoped<ISugestaoRepository, SugestaoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWorkEfCore>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IHashDeSenha, HashDeSenhaAspNetCore>();
        services.AddScoped<IEmissorDeToken, EmissorDeTokenJwt>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddScoped<DistribuirProtocolo>();
        services.AddScoped<Autenticar>();
        services.AddScoped<ObterUsuarioAtual>();
        services.AddScoped<CadastrarConferente>();
        services.AddScoped<EditarNivelEJornada>();
        services.AddScoped<EditarPerfilConferente>();
        services.AddScoped<MarcarPresenca>();
        services.AddScoped<RemoverConferente>();
        services.AddScoped<ImportarLote>();
        services.AddScoped<ObterVisaoDistribuicao>();
        services.AddScoped<RedistribuirPool>();
        services.AddScoped<AtribuirManualmente>();
        services.AddScoped<DescartarExcecao>();
        services.AddScoped<DefinirObservacao>();
        services.AddScoped<CriarRegraAlcada>();
        services.AddScoped<AtivarRegraAlcada>();
        services.AddScoped<DesativarRegraAlcada>();
        services.AddScoped<RemoverRegraAlcada>();
        services.AddScoped<ObterAlcancePorConferente>();
        services.AddScoped<ObterCoberturaDeAlcada>();
        services.AddScoped<CriarEquipe>();
        services.AddScoped<EditarEquipe>();
        services.AddScoped<MoverEscreventeParaEquipe>();
        services.AddScoped<ListarEscreventesSemEquipe>();
        services.AddScoped<ListarEscreventes>();
        services.AddScoped<ListarRegrasAlcada>();
        services.AddScoped<ListarEquipes>();
        services.AddScoped<ListarTiposAto>();
        services.AddScoped<CriarTipoAto>();
        services.AddScoped<ObterMinhaFila>();
        services.AddScoped<PegarProtocolo>();
        services.AddScoped<IniciarConferencia>();
        services.AddScoped<ConcluirConferencia>();
        services.AddScoped<ObterConcluidosHoje>();
        services.AddScoped<GerarSugestoes>();
        services.AddScoped<AplicarSugestao>();
        services.AddScoped<DescartarSugestao>();
        services.AddScoped<ListarSugestoesPendentes>();
        services.AddScoped<ListarHistoricoSugestoes>();
        services.AddScoped<ListarConferentes>();

        return services;
    }
}
