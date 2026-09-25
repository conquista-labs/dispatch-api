"""Gera os .xls sintéticos que os testes do conector de relatório usam.

Por que um gerador e não um arquivo real: o relatório de verdade do cartório tem nomes de
escreventes e apresentantes (dado pessoal) — não entra no repositório. Este script reproduz o
LAYOUT do "Relatório de Andamentos dos Protocolos" (Excel 97-2003, BIFF8, planilha "Plan1" com
~38 colunas de layout de impressão) com nomes fictícios, pra poder regerar os fixtures quando o
layout mudar.

Como regerar (xlwt escreve .xls binário; não precisa de Excel):

    python3 -m venv /tmp/venv-xls && /tmp/venv-xls/bin/pip install xlwt
    cd tests/Dispatch.Api.Tests/Conectores/Fixtures && /tmp/venv-xls/bin/python gerar_fixtures.py

Colunas (índice 0 = A) — as mesmas que ConversorRelatorioDeAndamentos lê:
  A(0)  andamento ("Pré - Conferência"), nome do escrevente, "Total Protocolos Andamento: ..."
  C(2)  título do relatório; número do protocolo (texto só com dígitos)
  H(7)  "L:  / F: 0 - 0" na linha do número
  J(9)  tipo de ato (linha seguinte à da data)
  Q(16) apresentante (ignorado pelo conector)
  R(17) "Total Protocolos Escrevente: N"
  V(21) "Substituto:" na linha do escrevente
  AB(27) data do andamento "dd/mm/aaaa"; AE(30) hora "hh:mm:ss" (duas linhas abaixo do número)
"""

import xlwt

COLUNAS = 39
APRESENTANTE = "APRESENTANTE FICTICIO LTDA"


class Planilha:
    def __init__(self):
        self.livro = xlwt.Workbook(encoding="utf-8")
        self.aba = self.livro.add_sheet("Plan1")
        self.livro.add_sheet("Plan2")
        self.livro.add_sheet("Plan3")
        self.linha = 0

    def escrever(self, coluna, valor):
        self.aba.write(self.linha, coluna, valor)

    def pular(self, n=1):
        self.linha += n

    # Cabeçalho de página: endereço, site, título. O relatório real repete isso a cada página,
    # inclusive no meio do bloco de um escrevente — o conector não pode depender de offset de linha.
    def cabecalho_de_pagina(self):
        self.escrever(0, "")
        self.pular(2)
        self.escrever(18, "")
        self.pular(2)
        self.escrever(19, "RUA FICTICIA, 100 - CENTRO - CIDADE EXEMPLO - UF - CEP 00000-000")
        self.pular(2)
        self.escrever(19, "www.cartorio-exemplo.invalid - contato@cartorio-exemplo.invalid")
        self.pular(6)
        self.aba.write_merge(self.linha, self.linha, 2, 20, "Relatório de Andamentos dos Protocolos")
        self.pular(2)

    def rodape(self, pagina, total_paginas):
        self.escrever(0, "25/09/2026 - 20:27:48")
        self.escrever(24, f"p. {pagina}/ {total_paginas}")
        self.pular(2)

    def andamento(self, texto):
        self.escrever(0, texto)
        self.pular(4)

    def escrevente(self, nome):
        self.aba.write_merge(self.linha, self.linha, 0, 20, nome)
        self.escrever(21, "Substituto:")
        self.escrever(23, "")
        self.pular(2)
        # Linhas de título das colunas
        self.escrever(9, "")
        self.escrever(11, "ATO")
        self.escrever(31, "Hora")
        self.pular(3)
        self.escrever(3, "Protocolo")
        self.escrever(7, "Livro / Folhas")
        self.escrever(15, "Apresentante")
        self.escrever(26, "DT Andamento")
        self.pular(2)

    def protocolo(self, numero, data, hora, tipo_ato):
        self.escrever(2, numero)
        self.escrever(7, "L:  / F: 0 - 0")
        self.escrever(9, "")
        self.pular(2)
        self.escrever(27, data)
        self.escrever(30, hora)
        self.pular(1)
        self.escrever(9, tipo_ato)
        self.escrever(16, APRESENTANTE)
        self.pular(3)

    def total_escrevente(self, n):
        self.escrever(17, f"Total Protocolos Escrevente: {n}")
        self.pular(3)

    def total_andamento(self, andamento, n):
        self.escrever(0, f"Total Protocolos Andamento: {andamento} - {n}")
        self.pular(2)

    def salvar(self, caminho):
        self.livro.save(caminho)


def pre_conferencia():
    """Caminho feliz: 2 escreventes, 5 protocolos, quebra de página no meio do 2º bloco.
    Um número de protocolo vem como célula numérica (o conector aceita texto ou número)."""
    p = Planilha()
    p.cabecalho_de_pagina()
    p.andamento("Pré - Conferência")
    p.escrevente("ANA PAULA FICTICIA")
    p.protocolo("900101", "22/09/2026", "17:34:05", "VENDA E COMPRA")
    p.protocolo("900102", "23/09/2026", "09:00:00", "INVENTÁRIO")
    p.total_escrevente(2)
    p.escrevente("BRUNO TESTE EXEMPLO")
    p.protocolo(900201, "24/09/2026", "11:05:12", "ATA NOTARIAL DE USUCAPIÃO")
    p.rodape(1, 2)
    p.cabecalho_de_pagina()
    p.protocolo("900202", "25/09/2026", "13:09:14", "PERMUTA")
    p.protocolo("900203", "25/09/2026", "23:30:00", "VENDA E COMPRA")
    p.total_escrevente(3)
    p.total_andamento("Pré - Conferência", 5)
    p.rodape(2, 2)
    p.salvar("relatorio-pre-conferencia.xls")


def pos_conferencia():
    p = Planilha()
    p.cabecalho_de_pagina()
    p.andamento("Pós - Conferência")
    p.escrevente("CARLA NOME FICTICIO")
    p.protocolo("900301", "20/09/2026", "08:15:00", "PROCURAÇÃO")
    p.total_escrevente(1)
    p.total_andamento("Pós - Conferência", 1)
    p.rodape(1, 1)
    p.salvar("relatorio-pos-conferencia.xls")


def totais_nao_conferem():
    """O bloco declara 3 protocolos e só traz 2 — o layout mudou ou a leitura perdeu linha."""
    p = Planilha()
    p.cabecalho_de_pagina()
    p.andamento("Pré - Conferência")
    p.escrevente("ANA PAULA FICTICIA")
    p.protocolo("900401", "22/09/2026", "10:00:00", "VENDA E COMPRA")
    p.protocolo("900402", "22/09/2026", "11:00:00", "VENDA E COMPRA")
    p.total_escrevente(3)
    p.total_andamento("Pré - Conferência", 3)
    p.rodape(1, 1)
    p.salvar("relatorio-totais-nao-conferem.xls")


def etapas_misturadas():
    p = Planilha()
    p.cabecalho_de_pagina()
    p.andamento("Pré - Conferência")
    p.escrevente("ANA PAULA FICTICIA")
    p.protocolo("900501", "22/09/2026", "10:00:00", "VENDA E COMPRA")
    p.total_escrevente(1)
    p.total_andamento("Pré - Conferência", 1)
    p.andamento("Pós - Conferência")
    p.escrevente("BRUNO TESTE EXEMPLO")
    p.protocolo("900502", "22/09/2026", "11:00:00", "PERMUTA")
    p.total_escrevente(1)
    p.total_andamento("Pós - Conferência", 1)
    p.rodape(1, 1)
    p.salvar("relatorio-etapas-misturadas.xls")


def vazio():
    """O relatório saiu sem nenhum protocolo no andamento."""
    p = Planilha()
    p.cabecalho_de_pagina()
    p.andamento("Pré - Conferência")
    p.total_andamento("Pré - Conferência", 0)
    p.rodape(1, 1)
    p.salvar("relatorio-vazio.xls")


def planilha_qualquer():
    """Um .xls válido que não é o relatório (sem o título) — formato não reconhecido."""
    livro = xlwt.Workbook(encoding="utf-8")
    aba = livro.add_sheet("Plan1")
    aba.write(0, 0, "protocolo")
    aba.write(0, 1, "tipoAto")
    aba.write(1, 0, "900601")
    aba.write(1, 1, "VENDA E COMPRA")
    livro.save("planilha-qualquer.xls")


if __name__ == "__main__":
    pre_conferencia()
    pos_conferencia()
    totais_nao_conferem()
    etapas_misturadas()
    vazio()
    planilha_qualquer()
