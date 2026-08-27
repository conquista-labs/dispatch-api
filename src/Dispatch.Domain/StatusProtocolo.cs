namespace Dispatch.Domain;

// Seção 8 do documento de requisitos. Conferindo/Aprovado/Reprovado ainda não têm transição
// implementada — entram junto com "Minha fila" (RF-19 a RF-24), quando o conferente passar
// a interagir com o próprio protocolo. Por ora só Pool/Atribuido/Excecao são alcançáveis.
public enum StatusProtocolo
{
    Pool,
    Atribuido,
    Conferindo,
    Aprovado,
    Reprovado,
    Excecao
}
