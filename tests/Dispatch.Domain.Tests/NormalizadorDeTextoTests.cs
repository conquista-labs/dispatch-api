namespace Dispatch.Domain.Tests;

public class NormalizadorDeTextoTests
{
    [Theory]
    [InlineData("VENDA E COMPRA", "Venda e Compra")]
    [InlineData("ALICE JORGE FERNANDES SILVA", "Alice Jorge Fernandes Silva")]
    [InlineData("ANA BEATRIZ DA SILVA OLIVEIRA", "Ana Beatriz da Silva Oliveira")]
    [InlineData("josé  da   silva", "José da Silva")]
    [InlineData("PROCURAÇÃO", "Procuração")]
    public void NormalizaCaixaAltaEConectivos(string entrada, string esperado)
    {
        Assert.Equal(esperado, NormalizadorDeTexto.ParaNomeProprio(entrada));
    }
}
