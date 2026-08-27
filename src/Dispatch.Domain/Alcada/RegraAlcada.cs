namespace Dispatch.Domain;

// Virou classe (era record): RF-33 precisa ativar/desativar/remover uma regra que já existe,
// e agora carrega Origem — ganhou identidade e ciclo de vida de verdade, deixou de ser só um
// valor imutável. Sujeito/Permissao/Alvo continuam fixos após criados (não existe "editar" o
// conteúdo de uma regra no requisito — só criar, ativar/desativar e remover).
public sealed class RegraAlcada
{
    public Guid Id { get; }
    public SujeitoAlcada Sujeito { get; }
    public PermissaoRegra Permissao { get; }
    public AlvoAlcada Alvo { get; }
    public OrigemRegra Origem { get; }
    public bool Ativa { get; private set; }

    public RegraAlcada(Guid id, SujeitoAlcada sujeito, PermissaoRegra permissao, AlvoAlcada alvo, OrigemRegra origem = OrigemRegra.Manual, bool ativa = true)
    {
        Id = id;
        Sujeito = sujeito;
        Permissao = permissao;
        Alvo = alvo;
        Origem = origem;
        Ativa = ativa;
    }

    public void Ativar() => Ativa = true;
    public void Desativar() => Ativa = false;
}
