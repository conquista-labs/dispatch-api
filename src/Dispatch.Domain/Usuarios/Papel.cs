namespace Dispatch.Domain;

// Seção 3 do requisito: o papel vem do cadastro, nunca é escolhido no login.
// Administrador (requisitos v2) fica no fim de propósito: a coluna é string(20) e o enum é gravado
// pelo nome, então não há migration — mas reordenar mudaria o valor numérico de quem o usa.
public enum Papel
{
    Distribuidora,
    Conferente,
    Administrador
}
