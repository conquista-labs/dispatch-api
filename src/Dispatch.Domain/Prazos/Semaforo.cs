namespace Dispatch.Domain;

// As duas faixas (atenção e urgência) são configuração do sistema, não constante do
// domínio — por isso entram como parâmetro, nunca fixas aqui dentro (seção 5 do requisito).
public static class Semaforo
{
    public static FaixaSemaforo Calcular(DateTimeOffset vencimento, DateTimeOffset agora, TimeSpan faixaAtencao, TimeSpan faixaUrgente)
    {
        var restante = vencimento - agora;

        if (restante < TimeSpan.Zero) return FaixaSemaforo.Vermelho;
        if (restante < faixaUrgente) return FaixaSemaforo.Laranja;
        if (restante < faixaAtencao) return FaixaSemaforo.Amarelo;
        return FaixaSemaforo.Verde;
    }
}
