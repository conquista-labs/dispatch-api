using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class ProtocoloConfiguration : IEntityTypeConfiguration<Protocolo>
{
    public void Configure(EntityTypeBuilder<Protocolo> builder)
    {
        builder.ToTable("protocolos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Numero).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Etapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Prioridade).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.AndamentoEm);
        builder.Property(p => p.EscreventeId);
        builder.Property(p => p.LoteImportacaoId);
        builder.Property(p => p.TipoAtoNomeOriginal).HasMaxLength(200);

        // Prazo aqui é opcional (só existe depois de DistribuirProtocolo rodar).
        builder.Property(p => p.Prazo)
            .HasConversion(PrazoConversoes.ParaTextoOpcional)
            .HasColumnName("prazo_tipo")
            .HasMaxLength(20);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        // RF-18i/j — explícito de propósito (mesma armadilha já documentada no CLAUDE.md:
        // propriedade só-com-getter sem declaração aqui falha o constructor binding do EF Core
        // em tempo de design, mesmo existindo de verdade).
        builder.Property(p => p.StatusAntesDeExcluir).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MotivoExcecao);
        builder.Property(p => p.Observacao);
        builder.Property(p => p.AtribuidoEm);
        builder.Property(p => p.CorrigidoEm);
        builder.Property(p => p.ReabertoEm);
        // Sem relacionamento/FK de propósito: é só um registro de auditoria (RNF-02), não uma
        // dependência de verdade — remover a regra de alçada mais tarde não pode quebrar (nem
        // travar via Restrict) a leitura de um protocolo antigo que a citou.
        builder.Property(p => p.RegraAplicadaId);

        builder.HasOne<TipoAto>()
            .WithMany()
            .HasForeignKey(p => p.TipoAtoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Conferente>()
            .WithMany()
            .HasForeignKey(p => p.DonoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Escrevente>()
            .WithMany()
            .HasForeignKey(p => p.EscreventeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LoteImportacao>()
            .WithMany()
            .HasForeignKey(p => p.LoteImportacaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
