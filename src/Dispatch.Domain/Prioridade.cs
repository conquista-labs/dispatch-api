namespace Dispatch.Domain;

// "Normal" continua com esse nome (não "Média") de propósito: é o valor gravado hoje em todo
// protocolo já existente (HasConversion<string> — renomear quebraria a leitura desses
// registros). O rótulo "Média" mostrado ao usuário é só do front (ver PRIORIDADE_LABEL no
// dispatch-web), fiel ao protótipo sem mexer no dado.
public enum Prioridade
{
    Baixa,
    Normal,
    Alta
}
