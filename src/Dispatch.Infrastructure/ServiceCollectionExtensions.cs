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
        services.AddScoped<IUsuarioTotpRepository, UsuarioTotpRepository>();
        services.AddScoped<IEventoAutenticacaoRepository, EventoAutenticacaoRepository>();
        services.AddScoped<IProtocoloRepository, ProtocoloRepository>();
        services.AddScoped<IEscreventeRepository, EscreventeRepository>();
        services.AddScoped<ILoteImportacaoRepository, LoteImportacaoRepository>();
        services.AddScoped<ISugestaoRepository, SugestaoRepository>();
        services.AddScoped<IPedidoReaberturaRepository, PedidoReaberturaRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWorkEfCore>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IHashDeSenha, HashDeSenhaAspNetCore>();
        services.AddScoped<IEmissorDeToken, EmissorDeTokenJwt>();
        services.AddSingleton<ITotp, TotpComOtpNet>();
        services.AddSingleton<ICifrador, CifradorAes>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));
        services.Configure<TotpOptions>(configuration.GetSection(TotpOptions.Secao));

        // Agrupado por arquivo de endpoint que consome (auditoria de qualidade — antes era uma
        // lista só, sem estrutura nenhuma; o projeto já foi mordido uma vez por esquecer de
        // registrar um caso de uso aqui, ver "Motor de alçada v2" no CLAUDE.md — só falha em
        // dotnet run, não em build/test). Uma linha faltando dentro do grupo errado continua
        // possível, mas fica bem mais fácil de notar numa lista curta e nomeada do que numa de
        // ~70 linhas soltas.

        // Auth (AuthEndpoints)
        services.AddScoped<Autenticar>();
        services.AddScoped<ObterUsuarioAtual>();

        // Totp (TotpEndpoints)
        services.AddScoped<RegistrarTotp>();
        services.AddScoped<ConfirmarRegistroTotp>();

        // Recuperação de senha (RecuperacaoSenhaEndpoints)
        services.AddScoped<IniciarRecuperacaoSenha>();
        services.AddScoped<ValidarCodigoRecuperacao>();
        services.AddScoped<RedefinirSenha>();

        // Conferente (ConferenteEndpoints)
        services.AddScoped<CadastrarConferente>();
        services.AddScoped<EditarNivelEJornada>();
        services.AddScoped<EditarPerfilConferente>();
        services.AddScoped<MarcarPresenca>();
        services.AddScoped<RemoverConferente>();
        services.AddScoped<ListarConferentes>();
        services.AddScoped<ObterCoberturaDeAlcada>();

        // Protocolo (ProtocoloEndpoints) — DistribuirProtocolo não tem endpoint próprio desde a
        // auditoria de qualidade (achado sem consumidor no front), mas continua registrado:
        // CriarProtocoloManual reaproveita ele internamente pra persistir.
        services.AddScoped<DistribuirProtocolo>();
        services.AddScoped<RedistribuirPool>();
        services.AddScoped<AtribuirManualmente>();
        services.AddScoped<ObterDetalheProtocolo>();
        services.AddScoped<DevolverAoPool>();
        services.AddScoped<AtribuirAoMenosCarregado>();
        services.AddScoped<DefinirPrioridadeDoProtocolo>();
        services.AddScoped<DescartarExcecao>();
        services.AddScoped<DefinirObservacao>();
        services.AddScoped<DecidirPedidoReabertura>();
        services.AddScoped<ReabrirConferencia>();
        services.AddScoped<ListarPedidosReaberturaPendentes>();
        services.AddScoped<SimularProtocoloManual>();
        services.AddScoped<CriarProtocoloManual>();
        services.AddScoped<EditarProtocoloManual>();
        services.AddScoped<ExcluirProtocolo>();
        services.AddScoped<RestaurarProtocolo>();

        // Importação (ImportacaoEndpoints)
        services.AddScoped<ImportarLote>();

        // Distribuição (DistribuicaoEndpoints)
        services.AddScoped<ObterVisaoDistribuicao>();

        // Regra de alçada / Central de regras (RegraAlcadaEndpoints)
        services.AddScoped<SimularAlcada>();
        services.AddScoped<CriarRegraAlcada>();
        services.AddScoped<AtivarRegraAlcada>();
        services.AddScoped<DesativarRegraAlcada>();
        services.AddScoped<RemoverRegraAlcada>();
        services.AddScoped<ObterAlcancePorConferente>();
        services.AddScoped<ListarRegrasAlcada>();

        // Equipe / Escrevente (EquipeEndpoints)
        services.AddScoped<CriarEquipe>();
        services.AddScoped<EditarEquipe>();
        services.AddScoped<MoverEscreventeParaEquipe>();
        services.AddScoped<ListarEscreventesSemEquipe>();
        services.AddScoped<ListarEscreventes>();
        services.AddScoped<ListarEquipes>();

        // Tipo de ato (TipoAtoEndpoints)
        services.AddScoped<ListarTiposAto>();
        services.AddScoped<ListarTiposAtoComUso>();
        services.AddScoped<CriarTipoAto>();
        services.AddScoped<RenomearTipoAto>();
        services.AddScoped<AtivarTipoAto>();
        services.AddScoped<DesativarTipoAto>();
        services.AddScoped<DefinirPesoDeComplexidadeDoTipoAto>();
        services.AddScoped<DefinirGrupoDoTipoAto>();
        services.AddScoped<RemoverTipoAto>();

        // Minha fila (MinhaFilaEndpoints) — ObterMinhaFila/ObterConcluidosHoje também servem
        // ConferenteEndpoints (Distribuidora vendo a fila de outra pessoa), mas nascem aqui.
        services.AddScoped<ObterMinhaFila>();
        services.AddScoped<PegarProtocolo>();
        services.AddScoped<IniciarConferencia>();
        services.AddScoped<ConcluirConferencia>();
        services.AddScoped<ObterConcluidosHoje>();
        services.AddScoped<CorrigirResultado>();
        services.AddScoped<PedirReabertura>();
        services.AddScoped<CancelarPedidoReabertura>();

        // Sugestões / aprendizado (SugestaoEndpoints)
        services.AddScoped<GerarSugestoes>();
        services.AddScoped<AplicarSugestao>();
        services.AddScoped<DescartarSugestao>();
        services.AddScoped<ListarSugestoesPendentes>();
        services.AddScoped<ListarHistoricoSugestoes>();

        // Dashboard (DashboardEndpoints)
        services.AddScoped<ObterDashboard>();

        return services;
    }
}
