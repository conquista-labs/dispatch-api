using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("equipes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Nome).IsRequired().HasMaxLength(200);

        builder.Property(e => e.PrazoPreConferencia)
            .HasConversion(PrazoConversoes.ParaTexto)
            .HasColumnName("prazo_pre_tipo")
            .HasMaxLength(30);

        builder.Property(e => e.PrazoPosConferencia)
            .HasConversion(PrazoConversoes.ParaTexto)
            .HasColumnName("prazo_pos_tipo")
            .HasMaxLength(30);

        // Corte de horário (pedido do dono, genérico por Equipe+Etapa) — TimeOnly? mapeia
        // nativamente pra `time` no Npgsql, sem ValueConverter. Propriedade só-com-setter-
        // privado precisa de declaração explícita (armadilha já documentada no projeto), senão
        // `dotnet ef migrations add` falha o constructor binding.
        builder.Property(e => e.CortePreConferenciaHorarioCorte).HasColumnName("corte_pre_horario_corte");
        builder.Property(e => e.CortePreConferenciaHorarioVencimento).HasColumnName("corte_pre_horario_vencimento");
        builder.Property(e => e.CortePosConferenciaHorarioCorte).HasColumnName("corte_pos_horario_corte");
        builder.Property(e => e.CortePosConferenciaHorarioVencimento).HasColumnName("corte_pos_horario_vencimento");
    }
}
