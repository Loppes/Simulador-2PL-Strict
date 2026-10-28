public class ConsoleTests
{
    [Teste]
    [Caso(null)]
    [Caso("q1[x] c1")]
    [Caso("w1[x] r2[x]")]
    [Caso("c1 c1")]
    [Caso("w1[x] c1 w1[x] c1")]
    public void EntradaInvalidaRetornaErroSemExecutarParcialmente(string? texto)
    {
        var saida = new StringWriter();
        var erros = new StringWriter();
        int codigo = Program.Executar(new StringReader(texto ?? ""), saida, erros);
        Verificar.Igual(1, codigo);
        Verificar.Contem("Entrada inválida:", erros.ToString());
        Verificar.NaoContem("Passo 1", saida.ToString());
        Verificar.NaoContem("HISTÓRIA FINAL", saida.ToString());
    }

    [Teste]
    public void HistoriaValidaRetornaSucesso()
    {
        var saida = new StringWriter();
        var erros = new StringWriter();
        Verificar.Igual(0, Program.Executar(new StringReader("w1[x] c1"), saida, erros));
        Verificar.Contem("Simulação concluída:", saida.ToString());
        Verificar.Igual("", erros.ToString());
    }

    [Teste]
    public void OverflowRetornaCodigoDeExecucaoIncompleta()
    {
        var saida = new StringWriter();
        Verificar.Igual(2, Program.Executar(new StringReader("w1[x,2147483647] w1[x] c1"), saida, new StringWriter()));
        Verificar.Contem("AVISO: simulação incompleta", saida.ToString());
    }
}
