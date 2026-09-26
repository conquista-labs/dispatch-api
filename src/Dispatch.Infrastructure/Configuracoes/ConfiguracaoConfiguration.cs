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
        // RF-42b/RF-46. O DEFAULT das colunas (95%/90%, 40/30/20/10) mora só na migration, não em
        // HasDefaultValue: com default no modelo, o EF omite no INSERT o valor igual ao default do CLR
        // (0), e um peso 0 viraria o DEFAULT do banco. Aqui só existe UPDATE, mas não vale a armadilha.
        builder.Property(c => c.MetaNoPrazo);
        builder.Property(c => c.MetaAprovadoNaPrimeira);
        builder.Property(c => c.PesoVolume);
        builder.Property(c => c.PesoPrazo);
        builder.Property(c => c.PesoQualidade);
        builder.Property(c => c.PesoComplexidade);
        // ADR-0046 (regra do pool). Mesma razão das metas: o DEFAULT (5 / true) só na migration — com
        // HasDefaultValue(true) o EF omitiria no INSERT o false (default do CLR) e ele viraria true.
        builder.Property(c => c.LimiteDeAtosNaMao);
        builder.Property(c => c.PoolEmOrdemObrigatoria);
        // Leituras calculadas sobre as colunas acima — não são coluna nem tabela.
        builder.Ignore(c => c.Metas);
        builder.Ignore(c => c.Pesos);
    }
}
