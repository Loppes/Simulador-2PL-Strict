public class SimuladorTests
{
    [Teste]
    [Caso("r1[x] w1[x,5] c1", "r1[X] w1[X,5] c1", 5, 0)]
    [Caso("r1[x] r2[x] c1 c2", "r1[X] r2[X] c1 c2", 0, 0)]
    [Caso("w1[x,7] r2[x] w2[y,4] c2 c1", "w1[X,7] c1 r2[X] w2[Y,4] c2", 7, 0)]
    [Caso("w1[x,10] w2[y,20] w1[y,11] w2[x,21] c1 c2", "w1[X,10] w1[Y,11] c1 w2[Y,20] w2[X,21] c2", 21, 1)]
    [Caso("r1[x] r2[x] w1[x] w2[x] c1 c2", "r1[X] w1[X] c1 r2[X] w2[X] c2", 2, 1)]
    [Caso("w1[x] w2[y,3] w2[y,7] w1[y] w2[x] c1 c2", "w1[X] w1[Y] c1 w2[Y,3] w2[Y,7] w2[X] c2", 2, 1)]
    [Caso("w1[x] w2[y] w3[z] w1[y] w2[z] w3[x] c1 c2 c3", "w1[X] w2[Y] w2[Z] c2 w1[Y] c1 w3[Z] w3[X] c3", 2, 1)]
    public void PreservaEscalonamentoEConcluiSemPendencias(string entrada, string hf, int x, int deadlocks)
    {
        var resultado = new Simulador(HistoriaParser.Parse(entrada), TextWriter.Null).Simular();
        Verificar.Igual(hf, string.Join(" ", resultado.HistoriaFinal));
        Verificar.Igual(x, resultado.Dados["X"]);
        Verificar.Igual(deadlocks, resultado.Deadlocks);
        Verificar.Verdadeiro(resultado.Concluida);
        Verificar.Vazio(resultado.TransacoesPendentes);
        Verificar.Vazio(resultado.TransacoesEmDelay);
        Verificar.Vazio(resultado.LocksAtivos);
        Verificar.Nulo(resultado.Erro);
    }

    [Teste]
    public void RollbackRestauraValorConfirmadoEReiniciaSemDuplicarOperacoes()
    {
        var saida = new StringWriter();
        var resultado = new Simulador(HistoriaParser.Parse(
            "w9[y,8] c9 w1[x] w2[y,3] w2[y,7] w1[y] w2[x] c1 c2"), saida).Simular();
        Verificar.Contem("rollback: Y volta de 7 para 3", saida.ToString());
        Verificar.Contem("rollback: Y volta de 3 para 8", saida.ToString());
        Verificar.Igual(7, resultado.Dados["Y"]);
        Verificar.Igual(4, resultado.HistoriaFinal.Count(op => op.Transacao == 2));
        Verificar.Verdadeiro(resultado.Concluida);
    }

    [Teste]
    public void VitimaTemMaiorIdMesmoQuandoFoiPrimeiraTransacaoDaHistoria()
    {
        var saida = new StringWriter();
        var resultado = new Simulador(HistoriaParser.Parse(
            "w9[x] w1[y] w9[y] w1[x] c9 c1"), saida).Simular();
        Verificar.Contem("maior identificador no ciclo. Vítima: T9", saida.ToString());
        Verificar.NaoContem("mais nova", saida.ToString());
        Verificar.Igual("w1[Y] w1[X] c1 w9[X] w9[Y] c9", string.Join(" ", resultado.HistoriaFinal));
        Verificar.Verdadeiro(resultado.Concluida);
    }

    [Teste]
    public void HistoriaParcialInformaTransacoesDelayELocksSemAfirmarSucesso()
    {
        var saida = new StringWriter();
        Operacao[] historia = [new(1, 'w', "X", false, 0), new(2, 'r', "X", false, 0)];
        var resultado = new Simulador(historia, saida).Simular();
        Verificar.Falso(resultado.Concluida);
        Verificar.Igual(new[] { 1, 2 }, resultado.TransacoesPendentes);
        Verificar.Igual(new[] { 2 }, resultado.TransacoesEmDelay);
        Verificar.Igual(new[] { "X: X(T1)" }, resultado.LocksAtivos);
        Verificar.Contem("AVISO: simulação incompleta", saida.ToString());
        Verificar.Contem("Transações sem commit executado: T1, T2", saida.ToString());
        Verificar.Contem("Transações em delay: T2", saida.ToString());
        Verificar.NaoContem("Simulação concluída:", saida.ToString());
    }

    [Teste]
    public void HistoriaParcialSemDelayTambemInformaLeitorPendente()
    {
        var saida = new StringWriter();
        var resultado = new Simulador(new[] { new Operacao(1, 'r', "X", false, 0) }, saida).Simular();
        Verificar.Falso(resultado.Concluida);
        Verificar.Vazio(resultado.TransacoesEmDelay);
        Verificar.Igual(new[] { "X: S(T1)" }, resultado.LocksAtivos);
        Verificar.Contem("Transações em delay: (nenhuma)", saida.ToString());
    }

    [Teste]
    public void OverflowNaoCorrompeDadoNemIncluiEscritaNaHF()
    {
        var saida = new StringWriter();
        var resultado = new Simulador(HistoriaParser.Parse("w1[x,2147483647] w1[x] c1"), saida).Simular();
        Verificar.Falso(resultado.Concluida);
        Verificar.Igual(int.MaxValue, resultado.Dados["X"]);
        Verificar.Unico(resultado.HistoriaFinal);
        Verificar.NaoNulo(resultado.Erro);
        Verificar.Contem("escrita não foi aplicada", saida.ToString());
        Verificar.Contem("AVISO: simulação incompleta", saida.ToString());
    }

    [Teste]
    public void SimuladorNaoPodeSerReutilizado()
    {
        var simulador = new Simulador(HistoriaParser.Parse("c1"), TextWriter.Null);
        Verificar.Verdadeiro(simulador.Simular().Concluida);
        Verificar.Lanca<InvalidOperationException>(() => simulador.Simular());
    }

    [Teste]
    public void SimuladorCopiaEntradaEProtegeResultadoContraAlteracao()
    {
        var entrada = HistoriaParser.Parse("w1[x] c1");
        var simulador = new Simulador(entrada, TextWriter.Null);
        entrada.Clear();
        var resultado = simulador.Simular();
        Verificar.Igual(2, resultado.HistoriaFinal.Count);
        Verificar.Lanca<NotSupportedException>(() => ((IList<Operacao>)resultado.HistoriaFinal).Clear());
        Verificar.Lanca<NotSupportedException>(() => ((IDictionary<string, int>)resultado.Dados)["X"] = 99);
    }

    [Teste]
    public void SimuladorTambemValidaEntradasCriadasSemParser()
    {
        Verificar.Lanca<ArgumentNullException>(() => new Simulador(null!));
        Verificar.Lanca<ArgumentException>(() => new Simulador(Array.Empty<Operacao>()));
        Operacao[] historia = [new(1, 'c', "", false, 0), new(1, 'r', "X", false, 0)];
        Verificar.Lanca<ArgumentException>(() => new Simulador(historia));
    }
}
