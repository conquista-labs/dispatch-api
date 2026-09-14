namespace Dispatch.Api;

// RNF-16: "origem" de um evento de autenticação (EventoAutenticacao.Origem) — IP do cliente.
// Atrás de um proxy (Render, Netlify não entra aqui — é o back que recebe direto), o IP real do
// cliente vem em X-Forwarded-For, não em RemoteIpAddress (que seria o IP do proxy); por isso
// checa o header primeiro, cai pro IP da conexão só se ele não existir (ex.: chamado direto,
// sem proxy — dev local).
public static class HttpContextExtensions
{
    public static string? ObterOrigem(this HttpContext context)
    {
        var encaminhado = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(encaminhado))
        {
            // Pode vir "cliente, proxy1, proxy2" — o primeiro é o IP original do cliente.
            return encaminhado.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
