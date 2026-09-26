using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ImportarLoteTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");
    private static readonly DateTimeOffset LinhaDeCorte = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);

    private static ImportarLote NovoCasoDeUso(
        out FakeEscreventeRepository escreventes,
        out FakeProtocoloRepository protocolos,
        out FakeTipoAtoRepository tiposAto,
        IReadOnlyCollection<Conferente>? conferentes = null,
        IReadOnlyCollection<Escrevente>? escreventesIniciais = null,
        IReadOnlyCollection<Equipe>? equipes = null,
        IReadOnlyCollection<Protocolo>? protocolosIniciais = null,
        IReadOnlyCollection<TipoAto>? tiposIniciais = null)
    {
        escreventes = new FakeEscreventeRepository(escreventesIniciais ?? []);
        protocolos = new FakeProtocoloRepository(protocolosIniciais ?? []);
        tiposAto = new FakeTipoAtoRepository(tiposIniciais ?? [Inventario]);
        return new ImportarLote(
            escreventes,
            new FakeEquipeRepository(equipes ?? []),
            new FakeConferenteRepository(conferentes ?? []),
            new FakeRegraAlcadaRepository([]),
            tiposAto,
            protocolos,
            new FakeLoteImportacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(LinhaDeCorte));
    }

    [Fact]
    public async Task LinhaAntesDaLinhaDeCorte_EhIgnorada()
    {
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(-1)),
            new LinhaImportacao("262204", "Inventário", "Fulano", LinhaDeCorte.AddHours(1))
        };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(2, resumo.TotalNoArquivo);
        Assert.Equal(1, resumo.IgnoradasPelaLinhaDeCorte);
        Assert.Equal(1, resumo.Processadas);
    }

    [Fact]
    public async Task EscreventeDesconhecido_EhCriadoSemEquipeESinalizado()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano Novo", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Fulano Novo", resumo.EscreventesSemEquipe);
        // Prévia não persiste (RF-11) — o escrevente novo não pode ter ido pro repositório ainda.
        Assert.Equal(0, escreventes.Quantidade);
    }

    // Tipo de ato novo entra direto no catálogo (nome normalizado) em vez de travar esperando
    // uma sugestão de aprendizado (RF-39/RF-40) — sem conferente na escala, ainda vira exceção,
    // mas por "ninguém com alçada", não mais "tipo desconhecido" (o tipo já existe).
    [Fact]
    public async Task TipoDeAtoNovo_EhSinalizadoECadastradoNoCatalogo()
    {
        var linhas = new[] { new LinhaImportacao("262203", "ATO QUE NÃO EXISTE", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out var tiposAto);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Contains("Ato Que Não Existe", resumo.TiposDesconhecidos);
        Assert.Equal(1, resumo.Excecoes);
        // Prévia não persiste (RF-11) — o tipo novo não pode ter ido pro repositório ainda.
        Assert.Equal(1, tiposAto.Quantidade);
    }

    // Seção 11 do documento de requisitos: "ao distribuir um lote o motor considera a carga
    // acumulada dentro da própria rodada, e não apenas a carga já gravada". Dois protocolos
    // urgentes (prazo de 1h) do mesmo tipo/etapa, dois conferentes empatados em carga — sem
    // o incremento em memória, os dois iriam pro mesmo conferente (sempre o de menor carga
    // gravada); com o incremento, o segundo protocolo já enxerga a carga do primeiro.
    [Fact]
    public async Task DoisProtocolosUrgentesNoMesmoLote_NaoRepetemOMesmoConferente()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.UmaHora), new Prazo(TipoPrazo.UmaHora));
        var escreventeA = new Escrevente(Guid.NewGuid(), "Escrevente A", equipeId);
        var escreventeB = new Escrevente(Guid.NewGuid(), "Escrevente B", equipeId);
        var conferenteX = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var conferenteY = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Escrevente A", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "Inventário", "Escrevente B", LinhaDeCorte.AddHours(1))
        };
        var casoDeUso = NovoCasoDeUso(
            out _, out var protocolos, out _,
            [conferenteX, conferenteY], [escreventeA, escreventeB], [equipe]);

        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        var donos = protocolos.Todos.Select(p => p.DonoId).ToList();
        Assert.Equal(2, donos.Distinct().Count());
    }

    // Sem regra de alçada nenhuma, "ausência de regra = permitido" (RF-31) — um tipo novo com
    // pelo menos um conferente na escala já flui pro pool na hora, sem exceção nenhuma. É
    // exatamente o cenário de um cartório novo, sem nenhuma alçada configurada ainda.
    [Fact]
    public async Task TipoDeAtoNovo_ComConferenteNaEscala_VaiParaOPoolSemExcecao()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[] { new LinhaImportacao("262203", "VENDA E COMPRA", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, [conferente]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(0, resumo.Excecoes);
        Assert.Equal(1, resumo.EnviadosParaPool);
        Assert.Contains("Venda e Compra", resumo.TiposDesconhecidos);
    }

    // RF confirmado nesta sessão: relatório vem em CAIXA ALTA, mas o cadastro precisa sair
    // normalizado — tanto no tipo de ato (aqui) quanto no escrevente (teste acima).
    [Fact]
    public async Task Confirmar_CadastraTipoDeAtoNovoNormalizado()
    {
        var linhas = new[] { new LinhaImportacao("262203", "VENDA E COMPRA", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto);

        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        // Catálogo já nasce com "Inventário" (fixture) — "Venda e Compra" é o segundo.
        Assert.Equal(2, tiposAto.Quantidade);
        var protocolo = Assert.Single(protocolos.Todos);
        Assert.NotNull(protocolo.TipoAtoId);
    }

    // O relatório vem em caixa alta e sem acento ("INVENTARIO"); o catálogo tem "Inventário".
    // Casar por OrdinalIgnoreCase (sensível a acento) tratava como tipo novo e a confirmação
    // cadastrava um "Inventario" duplicado.
    [Fact]
    public async Task PreVisualizar_TipoSemAcentoNoRelatorio_CasaComOTipoAcentuadoDoCatalogo()
    {
        var linhas = new[] { new LinhaImportacao("262203", "INVENTARIO", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Empty(resumo.TiposDesconhecidos);
        Assert.True(Assert.Single(resumo.Linhas!).TipoConhecido);
    }

    [Fact]
    public async Task Confirmar_TipoSemAcentoNoRelatorio_UsaOTipoExistenteSemCriarDuplicata()
    {
        var linhas = new[] { new LinhaImportacao("262203", "INVENTARIO", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(1, tiposAto.Quantidade);
        Assert.Empty(resumo.TiposDesconhecidos);
        Assert.Equal(Inventario.Id, Assert.Single(protocolos.Todos).TipoAtoId);
    }

    [Fact]
    public async Task TipoSoComCaixaDiferente_CasaComOCatalogoNaPreviaENaConfirmacao()
    {
        var vendaECompra = new TipoAto(Guid.NewGuid(), "Venda e Compra");
        var linhas = new[] { new LinhaImportacao("262203", "venda e compra", "Fulano", LinhaDeCorte.AddHours(1)) };

        var previa = await NovoCasoDeUso(out _, out _, out _, tiposIniciais: [Inventario, vendaECompra])
            .PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto, tiposIniciais: [Inventario, vendaECompra]);
        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.True(Assert.Single(previa.Linhas!).TipoConhecido);
        Assert.Equal(2, tiposAto.Quantidade);
        Assert.Equal(vendaECompra.Id, Assert.Single(protocolos.Todos).TipoAtoId);
    }

    // Dentro do mesmo lote, a mesma grafia com e sem acento de um tipo que ainda não existe
    // vira um tipo só — o recém-criado entra no mesmo dicionário usado pras linhas seguintes.
    [Fact]
    public async Task Confirmar_MesmoTipoNovoComESemAcentoNoLote_CadastraUmSo()
    {
        var linhas = new[]
        {
            new LinhaImportacao("262203", "ESCRITURA DE DOACAO", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "ESCRITURA DE DOAÇÃO", "Fulano", LinhaDeCorte.AddHours(1))
        };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(2, tiposAto.Quantidade);
        Assert.Single(resumo.TiposDesconhecidos);
        Assert.Single(protocolos.Todos.Select(p => p.TipoAtoId).Distinct());
    }

    // "Conhecido" é "já estava no catálogo antes do lote": o tipo criado na 1ª linha entra no
    // dicionário do laço, e perguntar a ele marcava a 2ª linha em diante como conhecida.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PreVisualizar_TodasAsLinhasDeUmTipoNovo_SaemComoDesconhecidas(int quantidade)
    {
        var linhas = Enumerable.Range(0, quantidade)
            .Select(i => new LinhaImportacao($"26220{i}", "ATA NOTARIAL", "Fulano", LinhaDeCorte.AddHours(1 + i)))
            .Append(new LinhaImportacao("262299", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)))
            .ToList();
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.All(resumo.Linhas!.Where(l => l.TipoAto == "ATA NOTARIAL"), l => Assert.False(l.TipoConhecido));
        Assert.True(resumo.Linhas!.Single(l => l.TipoAto == "Inventário").TipoConhecido);
        Assert.Equal(new TipoDesconhecidoContagem("Ata Notarial", quantidade), Assert.Single(resumo.TiposDesconhecidosContagem));
    }

    // Grafia com e sem acento do mesmo tipo novo na mesma rodada: um tipo só, as duas linhas
    // marcadas como desconhecidas e somadas na mesma contagem.
    [Fact]
    public async Task PreVisualizar_MesmoTipoNovoComESemAcento_MarcacaoEContagemUnicas()
    {
        var linhas = new[]
        {
            new LinhaImportacao("262203", "ESCRITURA DE DOACAO", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "Escritura de Doação", "Fulano", LinhaDeCorte.AddHours(2)),
            new LinhaImportacao("262205", "escritura de doação", "Fulano", LinhaDeCorte.AddHours(3))
        };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.All(resumo.Linhas!, l => Assert.False(l.TipoConhecido));
        var nome = Assert.Single(resumo.TiposDesconhecidos);
        Assert.Equal(new TipoDesconhecidoContagem(nome, 3), Assert.Single(resumo.TiposDesconhecidosContagem));
    }

    // Contagem só das linhas processadas (as de antes da linha de corte não contam), na mesma
    // ordem de TiposDesconhecidos — e igual na prévia e na confirmação.
    [Fact]
    public async Task Contagem_PorTipoNovo_IgnoraLinhaDeCorte_MesmaOrdemDosNomes_NaPreviaENaConfirmacao()
    {
        var linhas = new[]
        {
            new LinhaImportacao("262201", "USUCAPIAO", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262202", "ATA NOTARIAL", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262203", "USUCAPIAO", "Fulano", LinhaDeCorte.AddHours(2)),
            new LinhaImportacao("262204", "ATA NOTARIAL", "Fulano", LinhaDeCorte.AddHours(-1)),
            new LinhaImportacao("262205", "Inventário", "Fulano", LinhaDeCorte.AddHours(1))
        };

        var previa = await NovoCasoDeUso(out _, out _, out _)
            .PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);
        var confirmacao = await NovoCasoDeUso(out _, out _, out _)
            .ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        foreach (var resumo in new[] { previa, confirmacao })
        {
            Assert.Equal(["Ata Notarial", "Usucapiao"], resumo.TiposDesconhecidos);
            Assert.Equal(
                [new TipoDesconhecidoContagem("Ata Notarial", 1), new TipoDesconhecidoContagem("Usucapiao", 2)],
                resumo.TiposDesconhecidosContagem);
        }
    }

    [Fact]
    public async Task Contagem_SemTipoNovo_EhVazia()
    {
        var linhas = new[] { new LinhaImportacao("262203", "INVENTARIO", "Fulano", LinhaDeCorte.AddHours(1)) };

        var resumo = await NovoCasoDeUso(out _, out _, out _)
            .PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Empty(resumo.TiposDesconhecidosContagem);
        Assert.True(Assert.Single(resumo.Linhas!).TipoConhecido);
    }

    // Duplicata por acento já gravada antes da correção (o bug criava "Inventario" ao lado de
    // "Inventário") não pode derrubar a importação com chave repetida no dicionário: vale a
    // ativa — desativar a duplicata na tela Tipos de ato é o jeito de escolher qual fica.
    [Fact]
    public async Task CatalogoComDuplicataPorAcento_NaoQuebraEUsaATipoAtiva()
    {
        var duplicataInativa = new TipoAto(Guid.NewGuid(), "Inventario", ativo: false);
        var linhas = new[] { new LinhaImportacao("262203", "INVENTARIO", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out var tiposAto, tiposIniciais: [duplicataInativa, Inventario]);

        await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(2, tiposAto.Quantidade);
        Assert.Equal(Inventario.Id, Assert.Single(protocolos.Todos).TipoAtoId);
    }

    [Fact]
    public async Task PreVisualizar_NaoPersisteNada()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        Assert.Equal(0, protocolos.Quantidade);
        Assert.Equal(0, escreventes.Quantidade);
        Assert.Null(resumo.LoteImportacaoId);
    }

    [Fact]
    public async Task Confirmar_PersisteProtocolosENovosEscreventes()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var linhas = new[]
        {
            new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)),
            new LinhaImportacao("262204", "Inventário", "Fulano", LinhaDeCorte.AddHours(2))
        };
        var casoDeUso = NovoCasoDeUso(out var escreventes, out var protocolos, out _, conferentes: [conferente]);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(2, protocolos.Quantidade);
        Assert.Equal(1, escreventes.Quantidade);
        Assert.Equal(2, resumo.EnviadosParaPool);
        Assert.NotNull(resumo.LoteImportacaoId);
        var todosDoLote = await protocolos.ObterParaDistribuicaoAsync(resumo.LoteImportacaoId, CancellationToken.None);
        Assert.Equal(2, todosDoLote.Count);
    }

    [Fact]
    public async Task ProtocoloUrgentePorPrazo_ContaNoResumoPorConferente()
    {
        // Prazo D0 (via equipe) é urgente mesmo sem prioridade alta — não tem coluna de
        // prioridade no relatório importado, só o prazo derivado da equipe decide isso aqui.
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D0));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var atribuicao = Assert.Single(resumo.AtribuidosPorConferente);
        Assert.Equal(conferente.Id, atribuicao.ConferenteId);
        Assert.Equal(1, atribuicao.Quantidade);
    }

    [Fact]
    public async Task PreVisualizar_LinhaAntesDaLinhaDeCorte_VemComoJaExisteSemPrazoResolvido()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(-1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.True(linha.JaExiste);
        Assert.Null(linha.Equipe);
        Assert.Null(linha.Prazo);
        Assert.Null(linha.Semaforo);
        Assert.Equal(0, linha.ComAlcada);
    }

    [Fact]
    public async Task PreVisualizar_LinhaComEquipe_TrazEquipeEPrazoDaRegra()
    {
        // Mesmo exemplo do documento de requisitos: "5º andar · pós-conferência".
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D0));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _, conferentes: [conferente], escreventesIniciais: [escrevente], equipes: [equipe]);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PosConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.False(linha.JaExiste);
        Assert.True(linha.TipoConhecido);
        Assert.Equal("5º andar", linha.Equipe);
        Assert.Equal(TipoPrazo.D0, linha.Prazo);
        Assert.NotNull(linha.VencimentoEm);
        Assert.NotNull(linha.Semaforo);
        Assert.Equal(1, linha.ComAlcada);
    }

    [Fact]
    public async Task PreVisualizar_TipoDesconhecido_TrazTipoConhecidoFalsoEComAlcadaZero()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Ato Que Não Existe", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.PreVisualizarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte, FaixaAtencao, FaixaUrgente);

        var linha = Assert.Single(resumo.Linhas!);
        Assert.False(linha.TipoConhecido);
        Assert.Equal(0, linha.ComAlcada);
    }

    // Continuidade de conferência (pedido do dono, não é RF numerado — ver
    // ResolvedorDeContinuidade): protocolo reprovado reaparecendo num lote seguinte, mesmo
    // Número e mesma etapa, vai direto pro mesmo dono de antes — mesmo ele não sendo quem o
    // motor escolheria por carga (conferenteX está mais carregado que conferenteY aqui).
    [Fact]
    public async Task LinhaComMesmoNumeroEEtapaDeUmProtocoloReprovadoAnterior_AtribuiDiretoAoDonoDaquelaVez()
    {
        var conferenteX = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 5);
        var conferenteY = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocoloAnterior = new Protocolo(Guid.NewGuid(), "262203", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, LinhaDeCorte.AddHours(-5));
        protocoloAnterior.AtribuirA(conferenteX.Id, LinhaDeCorte.AddHours(-4));
        protocoloAnterior.Reprovar(LinhaDeCorte.AddHours(-3));
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(
            out _, out _, out _, [conferenteX, conferenteY], protocolosIniciais: [protocoloAnterior]);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        var atribuicao = Assert.Single(resumo.AtribuidosPorConferente);
        Assert.Equal(conferenteX.Id, atribuicao.ConferenteId);
    }

    [Fact]
    public async Task LinhaComContinuidade_DonoAnteriorForaDaEscala_VaiParaExcecaoComMotivoProprio()
    {
        var protocoloAnterior = new Protocolo(Guid.NewGuid(), "262203", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, LinhaDeCorte.AddHours(-5));
        protocoloAnterior.AtribuirA(Guid.NewGuid(), LinhaDeCorte.AddHours(-4));
        protocoloAnterior.Reprovar(LinhaDeCorte.AddHours(-3));
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out var protocolos, out _, protocolosIniciais: [protocoloAnterior]);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Equal(1, resumo.Excecoes);
        var novo = protocolos.Todos.Single(p => p.Id != protocoloAnterior.Id);
        Assert.Equal("conferente da primeira conferência não está mais disponível", novo.MotivoExcecao);
    }

    [Fact]
    public async Task Confirmar_NaoTrazLinhasDePrevia()
    {
        var linhas = new[] { new LinhaImportacao("262203", "Inventário", "Fulano", LinhaDeCorte.AddHours(1)) };
        var casoDeUso = NovoCasoDeUso(out _, out _, out _);

        var resumo = await casoDeUso.ConfirmarAsync(linhas, Etapa.PreConferencia, LinhaDeCorte);

        Assert.Null(resumo.Linhas);
    }
}
