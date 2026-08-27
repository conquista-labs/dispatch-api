namespace Dispatch.Domain;

// Seção 8 do documento de requisitos, mais Descartado — não está na lista da seção 8, mas
// RF-17 pede explicitamente uma ação de "descartar" na fila de exceções, e precisa de um
// status terminal próprio (não é "reprovado", que implica conferente revisando de verdade).
// Conferindo/Aprovado/Reprovado ainda não têm transição implementada — entram junto com
// "Minha fila" (RF-19 a RF-24), quando o conferente passar a interagir com o próprio protocolo.
public enum StatusProtocolo
{
    Pool,
    Atribuido,
    Conferindo,
    Aprovado,
    Reprovado,
    Excecao,
    Descartado
}
