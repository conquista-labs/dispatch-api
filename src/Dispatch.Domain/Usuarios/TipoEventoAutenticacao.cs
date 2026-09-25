namespace Dispatch.Domain;

// RNF-16: trilha de auditoria dos eventos de TOTP/recuperação de senha. Sem tela de consulta
// ainda (gap consciente, ver docs/gaps-requisitos.md, §7) — só grava, pra existir quando a
// tela for construída.
public enum TipoEventoAutenticacao
{
    RegistroTotpIniciado,
    RegistroTotpConfirmado,
    RecuperacaoIniciada,
    RecuperacaoCodigoValidado,
    RecuperacaoCodigoFalhou,
    RecuperacaoContaBloqueada,
    SenhaRedefinida,
    // Login por senha (POST /auth/login) — não existia rastro nenhum de tentativa errada até
    // aqui (achado na mesma auditoria que trouxe Origem acima).
    LoginFalhou,
    LoginBloqueado
}
