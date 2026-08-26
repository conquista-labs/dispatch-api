namespace Dispatch.Application;

// Abstrai "agora" pra manter o caso de uso testável: sem isso, todo teste que envolve
// prazo dependeria do relógio real da máquina rodando o teste, e não daria pra fixar um
// instante determinístico no Arrange.
public interface IRelogio
{
    DateTimeOffset Agora { get; }
}
