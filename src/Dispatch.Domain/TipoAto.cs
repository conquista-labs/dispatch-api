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

    // Nascido nulo quando o tipo entra sozinho pela importação (RF-09 não pede classificação
    // nesse momento) — a distribuidora classifica depois na tela "Tipos de ato". Não existe
    // tela de gestão de grupo no protótipo (só leitura agrupada na Matriz de alçada), então os
    // 5 valores ficam fixos como enum, mesmo padrão de Nivel/Etapa/TipoPrazo.
    public GrupoTipoAto? Grupo { get; private set; }

    public TipoAto(Guid id, string nome, bool ativo = true, int pesoComplexidade = 1, GrupoTipoAto? grupo = null)
    {
        Id = id;
        Nome = nome;
        Ativo = ativo;
        PesoComplexidade = pesoComplexidade;
        Grupo = grupo;
    }

    public void Renomear(string nome) => Nome = nome;

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;

    public void DefinirPesoDeComplexidade(int peso) => PesoComplexidade = peso;

    public void DefinirGrupo(GrupoTipoAto? grupo) => Grupo = grupo;
}

// Classificação de alto nível do catálogo, vista ao vivo na Matriz da aba Alçada do protótipo
// (Transmissões, Sucessões, Família, Garantias, Notariais) — usada hoje só como agrupamento de
// leitura; nenhuma regra de negócio depende do valor em si.
public enum GrupoTipoAto
{
    Transmissoes,
    Sucessoes,
    Familia,
    Garantias,
    Notariais
}
