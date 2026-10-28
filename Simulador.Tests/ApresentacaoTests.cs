public class ApresentacaoTests
{
    private static Simulador Criar(string hi) => new(HistoriaParser.Parse(hi), TextWriter.Null);

    [Teste]
    public void IniciarNaoExecutaOperacoesEAvancarExecutaUmaChegada()
    {
        var motor = Criar("w1[x,10] w2[x,20] c1 c2");
        Verificar.Vazio(motor.EstadoAtual.HistoriaFinal);
        Verificar.Igual(0, motor.EstadoAtual.Dados["X"]);
        Verificar.Igual(4, motor.HistoriaRestante.Count);
        var passo = motor.ExecutarProximoPasso()!;
        Verificar.Igual(1, passo.Numero);
        Verificar.Unico(passo.Estado.HistoriaFinal);
        Verificar.Igual(10, passo.Estado.Dados["X"]);
        Verificar.Igual(3, motor.HistoriaRestante.Count);
        var anterior = passo.Estado;
        motor.ExecutarProximoPasso();
        Verificar.Igual(new[] { 2 }, motor.EstadoAtual.TransacoesEmDelay);
        Verificar.Vazio(anterior.TransacoesEmDelay);
    }

    [Teste]
    public void DeadlockMantemRollbackERetornoAoFinalDaHi()
    {
        var motor = Criar("w1[x,10] w2[y,20] w1[y,11] w2[x,21] c1 c2");
        for (int i = 0; i < 3; i++) motor.ExecutarProximoPasso();
        var antes = motor.EstadoAtual;
        var passo = motor.ExecutarProximoPasso()!;
        Verificar.Igual(1, passo.Estado.Deadlocks);
        Verificar.Igual(11, passo.Estado.Dados["Y"]);
        Verificar.Igual("w1[X,10] w1[Y,11]", string.Join(" ", passo.Estado.HistoriaFinal));
        Verificar.Igual("c1 w2[Y,20] w2[X,21] c2", string.Join(" ", motor.HistoriaRestante));
        Verificar.Contem("rollback: Y volta de 20 para 0", passo.Mensagens);
        Verificar.Contem("Vítima: T2", passo.Mensagens);
        Verificar.Igual(20, antes.Dados["Y"]);
        while (motor.ExecutarProximoPasso() != null) { }
        Verificar.Verdadeiro(motor.EstadoAtual.Concluida);
        Verificar.Vazio(motor.HistoriaRestante);
    }

    [Teste]
    public void MensagensSaoIsoladasPorPassoEFinalNaoExecutaDuasVezes()
    {
        var motor = Criar("w1[x] c1");
        Verificar.Contem("Passo 1", motor.ExecutarProximoPasso()!.Mensagens);
        var ultimo = motor.ExecutarProximoPasso()!;
        Verificar.Contem("Passo 2", ultimo.Mensagens);
        Verificar.NaoContem("Passo 1", ultimo.Mensagens);
        Verificar.Contem("Simulação concluída", ultimo.Mensagens);
        Verificar.Verdadeiro(motor.Finalizada);
        Verificar.Nulo(motor.ExecutarProximoPasso());
        Verificar.Igual(2, motor.EstadoAtual.HistoriaFinal.Count);
    }

    [Teste]
    public void OverflowFinalizaPassosEPreservaErroNoEstadoAtual()
    {
        var motor = Criar("w1[x,2147483647] w1[x] c1");
        motor.ExecutarProximoPasso();
        var passo = motor.ExecutarProximoPasso()!;
        Verificar.Verdadeiro(motor.Finalizada);
        Verificar.Falso(motor.EstadoAtual.Concluida);
        Verificar.Igual(passo.Estado.Erro, motor.EstadoAtual.Erro);
        Verificar.NaoNulo(motor.EstadoAtual.Erro);
        Verificar.Igual(int.MaxValue, motor.EstadoAtual.Dados["X"]);
        Verificar.Unico(motor.HistoriaRestante);
        Verificar.Nulo(motor.ExecutarProximoPasso());
    }

    [Teste]
    public void HistoriaInicialPermaneceDisponivelAposReinicioDaVitima()
    {
        var motor = Criar("r1[x] r2[x] w1[x] w2[x] c1 c2");
        string original = string.Join(" ", motor.HistoriaInicial);
        while (motor.ExecutarProximoPasso() != null) { }
        Verificar.Igual(original, string.Join(" ", motor.HistoriaInicial));
        Verificar.Igual(1, motor.EstadoAtual.Deadlocks);
    }

    [Teste]
    public void IniciarHistoriaInvalidaNaoCriaSimulador()
    {
        Verificar.Lanca<ArgumentException>(() => Criar("w1[x]"));
    }

    [Teste]
    public void NaoPermiteSimularTudoNovamenteAposUmPasso()
    {
        var motor = Criar("w1[x] c1");
        motor.ExecutarProximoPasso();
        Verificar.Lanca<InvalidOperationException>(() => motor.Simular());
    }
}
