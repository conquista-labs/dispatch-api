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
        // Não único de propósito (RF-07 — reprocessamento/reimportação), mas é filtrado com
        // frequência: ObterPorNumerosAsync roda a cada importação de lote e a cada abertura do
        // painel de detalhe (continuidade de conferência) — sem índice, sequential scan da
        // tabela inteira (achado numa auditoria de performance/índices).
        builder.HasIndex(p => p.Numero);
        builder.Property(p => p.Etapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Prioridade).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.AndamentoEm);
        builder.Property(p => p.EscreventeId);
        builder.Property(p => p.LoteImportacaoId);
        builder.Property(p => p.TipoAtoNomeOriginal).HasMaxLength(200);

        // Prazo aqui é opcional (só existe depois de DistribuirProtocolo rodar). 30 (não 20)
        // porque o formato composto de corte de horário ("CorteDeHorario|10:00") tem 20 chars
        // exatos — sem folga nenhuma pra qualquer variação de formatação.
        builder.Property(p => p.Prazo)
            .HasConversion(PrazoConversoes.ParaTextoOpcional)
            .HasColumnName("prazo_tipo")
            .HasMaxLength(30);

        // Filtro mais repetido da tabela mais quente (ObterPoolAsync/ObterSemDonoAsync/
        // ObterParaDistribuicaoAsync/ObterConcluidosNoPeriodoAsync) — sustenta o caminho mais
        // quente do sistema (Minha fila, toda tela de todo conferente). Sem índice, cada
        // leitura varria a tabela inteira (achado na mesma auditoria do índice de Numero acima).
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(p => p.Status);
        // Composto pro Dashboard (ObterConcluidosNoPeriodoAsync: Status IN (Aprovado,
        // Reprovado) AND ConcluidoEm dentro do período) — sem isso, mesmo com o índice simples
        // de Status acima, o filtro por período ainda variava a tabela inteira de protocolos
        // com esse status, em vez de já vir estreitado pelas duas colunas juntas.
        builder.HasIndex(p => new { p.Status, p.ConcluidoEm });
        // RF-18i/j — explícito de propósito (mesma armadilha já documentada no CLAUDE.md:
        // propriedade só-com-getter sem declaração aqui falha o constructor binding do EF Core
        // em tempo de design, mesmo existindo de verdade).
        builder.Property(p => p.StatusAntesDeExcluir).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MotivoExcecao);
        builder.Property(p => p.Observacao);
        builder.Property(p => p.AtribuidoEm);
        builder.Property(p => p.CorrigidoEm);
        builder.Property(p => p.ReabertoEm);
        builder.Property(p => p.PausadoEm);

        // Um ciclo de conferência já encerrado (ver CicloConferencia.cs — por que é um registro
        // por ciclo, não um TimeSpan acumulado cego). Primeira coleção-filha do projeto — EF Core
        // acha o backing field `_ciclosAnteriores` sozinho (convenção `_<propriedade em
        // camelCase>`), então não precisa de `UsePropertyAccessMode` explícito aqui. Chave da
        // tabela filha é uma shadow property (`Id`, auto-incremento) porque `CicloConferencia`
        // não tem identidade própria fora do protocolo — é auditoria histórica, não uma entidade
        // que alguém busca/edita sozinha.
        builder.OwnsMany(p => p.CiclosAnteriores, ciclo =>
        {
            ciclo.ToTable("ciclos_conferencia");
            ciclo.WithOwner().HasForeignKey("protocolo_id");
            ciclo.Property<int>("Id").ValueGeneratedOnAdd();
            ciclo.HasKey("Id");
            ciclo.Property(c => c.ConferenteId).IsRequired();
            ciclo.Property(c => c.IniciadoEm).IsRequired();
            ciclo.Property(c => c.ConcluidoEm).IsRequired();
            // Índice pensando no consumidor real (ObterDashboard agrupando tempo por pessoa) —
            // sem isso, "quanto tempo a pessoa X gastou nos ciclos dela" varreria a tabela
            // inteira a cada carregamento do Dashboard, mesmo achado de auditoria já documentado
            // acima pra RegraAplicadaId.
            ciclo.HasIndex(c => c.ConferenteId);
        });

        // Uma pausa já encerrada (ver PausaConferencia.cs) — mesmo padrão de coleção-filha do
        // CiclosAnteriores acima, só que sem FK pra conferente (uma pausa é sempre do dono atual,
        // não muda de pessoa como um ciclo reaberto pode mudar).
        builder.OwnsMany(p => p.Pausas, pausa =>
        {
            pausa.ToTable("pausas_conferencia");
            pausa.WithOwner().HasForeignKey("protocolo_id");
            pausa.Property<int>("Id").ValueGeneratedOnAdd();
            pausa.HasKey("Id");
            pausa.Property(p => p.PausadoEm).IsRequired();
            pausa.Property(p => p.RetomadoEm).IsRequired();
        });

        // Um ajuste manual da Duracao já aplicado (ver AjusteDeDuracao.cs) — mesmo padrão de
        // coleção-filha, sem FK pro usuário que ajustou (mesmo raciocínio de RegraAplicadaId
        // abaixo: é só auditoria, não pode travar/quebrar se aquele usuário for removido depois).
        builder.OwnsMany(p => p.AjustesDeDuracao, ajuste =>
        {
            ajuste.ToTable("ajustes_de_duracao");
            ajuste.WithOwner().HasForeignKey("protocolo_id");
            ajuste.Property<int>("Id").ValueGeneratedOnAdd();
            ajuste.HasKey("Id");
            ajuste.Property(a => a.AjustadoPorId).IsRequired();
            ajuste.Property(a => a.AjustadoEm).IsRequired();
            ajuste.Property(a => a.DuracaoAnterior);
            ajuste.Property(a => a.DuracaoNova).IsRequired();
            ajuste.Property(a => a.Motivo);
        });
        // Sem relacionamento/FK de propósito: é só um registro de auditoria (RNF-02), não uma
        // dependência de verdade — remover a regra de alçada mais tarde não pode quebrar (nem
        // travar via Restrict) a leitura de um protocolo antigo que a citou. Sem FK, essa coluna
        // não ganha índice automático (diferente de DonoId/EscreventeId/TipoAtoId/
        // LoteImportacaoId acima, que ganham de graça por serem chave estrangeira de verdade) —
        // precisou de HasIndex explícito depois que ContarPorRegraAplicadaAsync (RF-33) foi
        // flagrado fazendo sequential scan a cada chamada (achado investigando lentidão real em
        // GET /regras-alcada, ver RegraAlcadaEndpoints.cs).
        builder.Property(p => p.RegraAplicadaId);
        builder.HasIndex(p => p.RegraAplicadaId);

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
