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

    // O relatório chega em caixa alta e sem acento ("INVENTARIO"), o catálogo tem o nome
    // acentuado ("Inventário") — casar só por OrdinalIgnoreCase criava tipo duplicado.
    [Theory]
    [InlineData("INVENTARIO", "Inventário")]
    [InlineData("venda e compra", "Venda e Compra")]
    [InlineData("PROCURACAO", "Procuração")]
    [InlineData("  ESCRITURA   DE DOACAO ", "Escritura de Doação")]
    [InlineData("Divórcio", "DIVÓRCIO")]
    public void ComparadorDeNome_IgnoraCaixaAcentoEEspacos(string doRelatorio, string doCatalogo)
    {
        Assert.True(NormalizadorDeTexto.ComparadorDeNome.Equals(doRelatorio, doCatalogo));
        Assert.Equal(0, NormalizadorDeTexto.ComparadorDeNome.Compare(doRelatorio, doCatalogo));
        Assert.Equal(
            NormalizadorDeTexto.ComparadorDeNome.GetHashCode(doRelatorio),
            NormalizadorDeTexto.ComparadorDeNome.GetHashCode(doCatalogo));
    }

    [Theory]
    [InlineData("Inventário", "Inventários")]
    [InlineData("Venda e Compra", "Venda Compra")]
    [InlineData("Doação", "Dotação")]
    public void ComparadorDeNome_NomesDiferentesContinuamDiferentes(string a, string b)
    {
        Assert.False(NormalizadorDeTexto.ComparadorDeNome.Equals(a, b));
        Assert.NotEqual(0, NormalizadorDeTexto.ComparadorDeNome.Compare(a, b));
    }

    // Serve de chave de dicionário: o nome do relatório acha a entrada gravada com acento.
    [Fact]
    public void ComparadorDeNome_FuncionaComoChaveDeDicionario()
    {
        var catalogo = new Dictionary<string, int>(NormalizadorDeTexto.ComparadorDeNome) { ["Inventário"] = 1 };

        Assert.True(catalogo.TryGetValue("INVENTARIO", out var valor));
        Assert.Equal(1, valor);
    }
}
