namespace Dispatch.Domain;

// RNF-16: trilha de auditoria dos eventos de TOTP/recuperação de senha. Sem tela de consulta
// ainda (gap consciente, ver CLAUDE.md) — só grava, pra existir quando a tela for construída.
public enum TipoEventoAutenticacao
{
    RegistroTotpIniciado,
    RegistroTotpConfirmado,
    RecuperacaoIniciada,
    RecuperacaoCodigoValidado,
    RecuperacaoCodigoFalhou,
    RecuperacaoContaBloqueada,
    SenhaRedefinida
}
