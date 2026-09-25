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

    // RF-34f: alimenta a parcela de complexidade do score (RF-46) e a estimativa do tempo de
    // referência (RF-46c). Decimal 0,50–2,50 em passos de 0,05 (PesoDeComplexidade) desde a fatia 5 do
    // Dashboard v2 — era inteiro ≥ 1 antes.
    public decimal PesoComplexidade { get; private set; }

    // RF-34a/RF-46c: tempo de referência informado pelo administrador (2–240 min). Nulo = não
    // informado — a referência efetiva cai pra mediana do histórico ou pra estimativa, calculadas na
    // leitura (TempoDeReferencia), nunca gravadas aqui.
    public int? TempoReferenciaMinutos { get; private set; }

    // Nascido nulo quando o tipo entra sozinho pela importação (RF-09 não pede classificação
    // nesse momento) — a distribuidora classifica depois na tela "Tipos de ato". Não existe
    // tela de gestão de grupo no protótipo (só leitura agrupada na Matriz de alçada), então os
    // 5 valores ficam fixos como enum, mesmo padrão de Nivel/Etapa/TipoPrazo.
    public GrupoTipoAto? Grupo { get; private set; }

    // O EF também constrói por aqui (constructor binding); o CHECK do banco garante que o que vem de lá
    // já passa na validação. TempoReferenciaMinutos entra pelo setter privado.
    public TipoAto(Guid id, string nome, bool ativo = true, decimal pesoComplexidade = PesoDeComplexidade.Padrao, GrupoTipoAto? grupo = null)
    {
        Id = id;
        Nome = nome;
        Ativo = ativo;
        PesoComplexidade = ValidarPeso(pesoComplexidade);
        Grupo = grupo;
    }

    public void Renomear(string nome) => Nome = nome;

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;

    // A Application valida antes e devolve 400 com o motivo; validar de novo aqui garante que nenhum
    // caminho grave um peso fora da regra (mesmo padrão de Configuracao.DefinirMetasEPesos).
    public void DefinirPesoDeComplexidade(decimal peso) => PesoComplexidade = ValidarPeso(peso);

    public void DefinirTempoDeReferencia(int? minutos)
    {
        var motivo = TempoDeReferencia.ValidarInformado(minutos);
        if (motivo is not null)
        {
            throw new ArgumentException(motivo, nameof(minutos));
        }

        TempoReferenciaMinutos = minutos;
    }

    private static decimal ValidarPeso(decimal peso) =>
        PesoDeComplexidade.Validar(peso) is { } motivo ? throw new ArgumentException(motivo, nameof(peso)) : peso;

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
