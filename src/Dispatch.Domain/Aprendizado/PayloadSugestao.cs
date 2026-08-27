namespace Dispatch.Domain;

// Hierarquia fechada, mesmo padrão de SujeitoAlcada/AlvoAlcada: cada tipo de sugestão (seção 7
// do requisito) carrega dados diferentes, e "aplicar" precisa saber exatamente qual mudança
// de verdade executar — não dá pra representar isso como um payload solto tipo objeto/JSON.
public abstract record PayloadSugestao
{
    private PayloadSugestao() { }

    // "Tipo desconhecido": tipo fora do catálogo com ≥5 ocorrências. Aplicar = classifica o
    // tipo (entra pro catálogo). NivelSugerido é a moda de quem resolveu na mão — só evidência
    // pra ajudar a decisão humana, não vira regra de alçada sozinho.
    public sealed record TipoDesconhecido(string NomeTipo, Nivel NivelSugerido) : PayloadSugestao;

    // "Prazo irreal": ≥8 casos e >60% de estouro em equipe+etapa. Aplicar = muda o prazo
    // daquela equipe/etapa pra faixa mais próxima do percentil 80 do tempo real observado.
    public sealed record PrazoIrreal(Guid EquipeId, Etapa Etapa, TipoPrazo PrazoSugerido) : PayloadSugestao;

    // "Escrevente órfão": ≥3 protocolos sem equipe. Aplicar = aloca na equipe dominante entre
    // os outros escreventes do(s) mesmo(s) lote(s) em que ele apareceu.
    public sealed record EscreventeOrfao(Guid EscreventeId, Guid EquipeSugeridaId) : PayloadSugestao;

    // "Risco de qualidade": ≥6 casos e >50% de reprovação em tipo+nível. Aplicar = cria regra
    // de alçada negando aquele nível para aquele tipo — restringe ao nível acima.
    public sealed record RiscoQualidade(Guid TipoAtoId, Nivel NivelRestrito) : PayloadSugestao;
}
