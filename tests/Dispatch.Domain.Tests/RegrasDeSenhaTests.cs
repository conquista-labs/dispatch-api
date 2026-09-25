namespace Dispatch.Domain.Tests;

public class RegrasDeSenhaTests
{
    [Theory]
    [InlineData("curtademais")]
    [InlineData("onzecarac1")]
    public void TemComprimentoMinimo_FalsoAbaixoDe12(string senha) => Assert.False(RegrasDeSenha.TemComprimentoMinimo(senha));

    [Fact]
    public void TemComprimentoMinimo_VerdadeiroCom12OuMais() => Assert.True(RegrasDeSenha.TemComprimentoMinimo("doze-caracteres"));

    [Theory]
    [InlineData("Senha12345678")]
    [InlineData("123456789012345")]
    [InlineData("Cartorio123456")]
    [InlineData("DISPATCH123456")]
    public void NaoEhObvia_FalsoParaPrefixosConhecidos(string senha) => Assert.False(RegrasDeSenha.NaoEhObvia(senha));

    [Fact]
    public void NaoEhObvia_VerdadeiroParaFraseLongaSemPrefixoObvio() => Assert.True(RegrasDeSenha.NaoEhObvia("cavalo azul correndo"));

    [Fact]
    public void EhForte_ExigeComprimentoENaoSerObvia()
    {
        Assert.True(RegrasDeSenha.EhForte("cavalo azul correndo"));
        Assert.False(RegrasDeSenha.EhForte("curta"));
        Assert.False(RegrasDeSenha.EhForte("senha1234567890"));
    }

    // RF-45: a senha inicial (definida por quem cria a conta) só precisa de 8 caracteres — ela vale
    // até o primeiro acesso, quando a regra forte passa a valer (ADR-0040).
    [Theory]
    [InlineData("")]
    [InlineData("sete777")]
    public void ServeComoSenhaInicial_FalsoAbaixoDe8(string senha) => Assert.False(RegrasDeSenha.ServeComoSenhaInicial(senha));

    [Fact]
    public void ServeComoSenhaInicial_VerdadeiroCom8OuMais() => Assert.True(RegrasDeSenha.ServeComoSenhaInicial("abcd-e12"));
}
