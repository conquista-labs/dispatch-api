namespace Dispatch.Domain;

// Hierarquia fechada (construtor privado + tipos aninhados) emulando um sum type: "sujeito"
// só pode ser uma dessas duas formas, nunca outra. É o jeito idiomático em C# de dizer
// "isto é ou pessoa ou nível, nunca os dois nem nenhum" sem precisar de um enum + campos
// nulos soltos.
public abstract record SujeitoAlcada
{
    private SujeitoAlcada() { }

    public sealed record PorPessoa(Guid ConferenteId) : SujeitoAlcada;

    public sealed record PorNivel(Nivel Nivel) : SujeitoAlcada;
}
