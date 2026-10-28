public class HistoriaParserTests
{
    [Teste]
    [Caso(null)]
    [Caso("")]
    [Caso(" \t ")]
    [Caso("q1[x] c1")]
    [Caso("r1[x,5] c1")]
    [Caso("w1[x,1,2] c1")]
    [Caso("w1[x,] c1")]
    [Caso("w1[x,1.5] c1")]
    [Caso("w1[x,2147483648] c1")]
    [Caso("w1[x,-2147483649] c1")]
    [Caso("r0[x] c0")]
    [Caso("r-1[x] c-1")]
    [Caso("r2147483648[x] c1")]
    [Caso("c2147483648")]
    [Caso("r1[] c1")]
    [Caso("r1[ ] c1")]
    [Caso("r1[2x] c1")]
    [Caso("r1[x y] c1")]
    [Caso("r1[x]lixo c1")]
    [Caso("r1[x)c1")]
    [Caso("r1[x c1")]
    [Caso("r1[[x]] c1")]
    [Caso("r1x] c1")]
    [Caso("r[x] c1")]
    [Caso("c1[x]")]
    [Caso("r1[x]c1")]
    [Caso("r1[x][y] c1")]
    [Caso("w1[x,1](y) c1")]
    [Caso("w1[x,5]lixo c1")]
    [Caso("w1[x,1e2] c1")]
    [Caso("c+1")]
    [Caso("r+1[x] c1")]
    public void RejeitaSintaxeInvalida(string? entrada)
    {
        Verificar.Lanca<ArgumentException>(() => HistoriaParser.Parse(entrada));
    }

    [Teste]
    [Caso("w1[x]", "Falta commit para: T1")]
    [Caso("r2[x] r1[y]", "Falta commit para: T1, T2")]
    [Caso("r1[x] c1 c1", "Commit repetido")]
    [Caso("w1[x] c1 r1[x]", "após o commit")]
    [Caso("c1 w1[x] c1", "após o commit")]
    public void ValidaCicloDeVidaDaTransacao(string entrada, string mensagem)
    {
        var erro = Verificar.Lanca<ArgumentException>(() => HistoriaParser.Parse(entrada));
        Verificar.Contem(mensagem, erro.Message);
    }

    [Teste]
    [Caso("R12[x] W12[x, -5] C12", -5)]
    [Caso("r12( x )\tw12( x, +5 )\tc12", 5)]
    [Caso("r12[x]\nw12[x,2147483647] c12", int.MaxValue)]
    [Caso("r12[x] w12[x,-2147483648] c12", int.MinValue)]
    public void AceitaFormatosValidos(string entrada, int valor)
    {
        var ops = HistoriaParser.Parse(entrada);
        Verificar.Igual(3, ops.Count);
        Verificar.Igual(12, ops[0].Transacao);
        Verificar.Igual('r', ops[0].Tipo);
        Verificar.Igual("X", ops[0].Dado);
        Verificar.Verdadeiro(ops[1].TemValor);
        Verificar.Igual(valor, ops[1].Valor);
        Verificar.Igual('c', ops[2].Tipo);
    }

    [Teste]
    public void NormalizaNomesEPermiteCommitSemAcessos()
    {
        var ops = HistoriaParser.Parse("w1[_saldo2] c1 c2");
        Verificar.Igual("_SALDO2", ops[0].Dado);
        Verificar.Falso(ops[0].TemValor);
        Verificar.Igual("c2", ops[2].ToString());
    }

    [Teste]
    [Caso(0, 'r', "X", false, 0)]
    [Caso(-1, 'w', "X", false, 0)]
    [Caso(1, 'q', "X", false, 0)]
    [Caso(1, 'r', "", false, 0)]
    [Caso(1, 'c', "X", false, 0)]
    [Caso(1, 'r', "X", true, 5)]
    [Caso(1, 'w', "X", false, 5)]
    [Caso(1, 'w', null, false, 0)]
    public void OperacaoImpedeEstadosInvalidos(int id, char tipo, string? dado, bool temValor, int valor)
    {
        Verificar.Lanca<ArgumentException>(() => new Operacao(id, tipo, dado!, temValor, valor));
    }

    [Teste]
    public void ValidadorRejeitaHistoriaNulaVaziaOuComOperacaoNula()
    {
        Verificar.Lanca<ArgumentNullException>(() => ValidadorHistoria.Validar(null!));
        Verificar.Lanca<ArgumentException>(() => ValidadorHistoria.Validar(Array.Empty<Operacao>()));
        Verificar.Lanca<ArgumentException>(() => ValidadorHistoria.Validar(new Operacao[] { null! }));
    }
}
