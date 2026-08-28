namespace Dispatch.Domain;

// Virou classe (era record) — RF-34b (renomear), RF-34d (ativar/desativar) e RF-34f (peso de
// complexidade) pedem mudança de estado ao longo do tempo, mesma razão de RegraAlcada ter
// deixado de ser record antes. Renomear não precisa "migrar" protocolo/regra nenhum (RF-34b) —
// os dois referenciam por Id, não por nome, então a migração já é automática.
public sealed class TipoAto
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public bool Ativo { get; private set; }

    // RF-34f: alimenta o score do conferente (RF-46, Dashboard). Sem uso ainda (Dashboard não
    // construído), mas nasce aqui pra não precisar de outra migration quando ele for. Peso
    // mínimo 1 — não existe "peso zero" no requisito.
    public int PesoComplexidade { get; private set; }

    public TipoAto(Guid id, string nome, bool ativo = true, int pesoComplexidade = 1)
    {
        Id = id;
        Nome = nome;
        Ativo = ativo;
        PesoComplexidade = pesoComplexidade;
    }

    public void Renomear(string nome) => Nome = nome;

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;

    public void DefinirPesoDeComplexidade(int peso) => PesoComplexidade = peso;
}
