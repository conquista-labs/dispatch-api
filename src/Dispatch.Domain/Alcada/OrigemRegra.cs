namespace Dispatch.Domain;

// RF-33: toda regra mostra a própria origem. Aprendida ainda não tem produtor (RF-39 a RF-41,
// "aprendizado sem IA" — não construído) — hoje toda regra criada pela Central de Regras nasce
// Manual.
public enum OrigemRegra
{
    Manual,
    Aprendida
}
