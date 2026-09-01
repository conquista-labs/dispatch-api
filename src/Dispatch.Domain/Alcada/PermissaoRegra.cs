namespace Dispatch.Domain;

public enum PermissaoRegra
{
    Permite,
    Nega,

    // Reserva um alvo pra um sujeito só — todo mundo mais fica bloqueado nesse alvo, mesmo que
    // tivesse Permite por outra regra. Checada antes de qualquer camada (ver ResolvedorAlcada).
    // Não concede acesso sozinha pro próprio sujeito: ele ainda precisa de outra regra (ou do
    // padrão aberto) pra realmente ter Permitido.
    Reserva
}
