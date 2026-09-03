using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("configuracao");
        builder.HasKey(c => c.Id);
        // Property explícito em cada campo (mesmo padrão do resto do projeto) — evita a
        // armadilha já documentada de constructor binding do EF Core com propriedades sem
        // setter público.
        builder.Property(c => c.FaixaAtencao);
        builder.Property(c => c.FaixaUrgente);
        builder.Property(c => c.LimiteDeAtosSimultaneos);
        builder.Property(c => c.JanelaDeCorrecao);
        builder.Property(c => c.DiasDeMemoriaDescarte);
        builder.Property(c => c.TempoMedioPorAtoMinutos);
        builder.Property(c => c.LimiarTipoDesconhecido);
        builder.Property(c => c.LimiarPrazoIrrealCasos);
        builder.Property(c => c.LimiarPrazoIrrealEstouro);
        builder.Property(c => c.LimiarEscreventeOrfao);
        builder.Property(c => c.LimiarRiscoQualidadeCasos);
        builder.Property(c => c.LimiarRiscoQualidadeReprovacao);
    }
}
