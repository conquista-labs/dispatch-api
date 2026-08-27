namespace Dispatch.Application;

// Uma linha do relatório já traduzida pro que a importação precisa (RF-06) — nome do
// protocolo, do ato e do escrevente como texto, e o instante do andamento (vira o
// momentoDeReferencia do prazo e a base da linha de corte que evita reimportar duplicata).
public sealed record LinhaImportacao(string Protocolo, string TipoAto, string Escrevente, DateTimeOffset DataHoraAndamento);
