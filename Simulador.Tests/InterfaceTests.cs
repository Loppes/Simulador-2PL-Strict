using Simulador2PL.Grafico;

public class InterfaceTests
{
    private static FormSimulador Abrir()
    {
        var janela = new FormSimulador { Opacity = 0, ShowInTaskbar = false };
        janela.Show();
        Application.DoEvents();
        return janela;
    }

    private static T Obter<T>(Form janela, string nome) where T : Control => (T)janela.Controls.Find(nome, true).Single();
    private static void Clicar(Form janela, string nome) => Obter<Button>(janela, nome).PerformClick();

    [Teste]
    public void JanelaExecutaDeadlockPassoAPassoEReinicia()
    {
        using var janela = Abrir();
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
        Clicar(janela, "iniciar");
        Verificar.Contem("História validada", Obter<Label>(janela, "situacao").Text);
        for (int i = 0; i < 4; i++) Clicar(janela, "proximo");
        Verificar.Contem("Deadlocks: 1", Obter<Label>(janela, "situacao").Text);
        Verificar.Contem("rollback", Obter<TextBox>(janela, "registro").Text);
        Verificar.Igual("w1[X,10] w1[Y,11]", Obter<TextBox>(janela, "hf").Text);
        Verificar.Igual("c1 w2[Y,20] w2[X,21] c2", Obter<TextBox>(janela, "hi").Text);
        for (int i = 0; i < 4; i++) Clicar(janela, "proximo");
        Verificar.Contem("Concluída", Obter<Label>(janela, "situacao").Text);
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
        Verificar.Falso(Obter<Button>(janela, "executarTudo").Enabled);
        Verificar.Contem("X = 21", Obter<TextBox>(janela, "estado").Text);
        Verificar.Contem("Y = 20", Obter<TextBox>(janela, "estado").Text);
        Clicar(janela, "iniciar");
        Verificar.Igual("", Obter<TextBox>(janela, "hf").Text);
        Verificar.Contem("X = 0", Obter<TextBox>(janela, "estado").Text);
    }

    [Teste]
    public void EntradaInvalidaPodeSerCorrigidaEEdicaoDescartaEstadoAntigo()
    {
        using var janela = Abrir();
        Obter<TextBox>(janela, "entrada").Text = "q1[x] c1";
        Clicar(janela, "iniciar");
        Verificar.Igual("Entrada inválida.", Obter<Label>(janela, "situacao").Text);
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
        Verificar.Contem("Nenhuma operação foi executada", Obter<TextBox>(janela, "registro").Text);
        Obter<TextBox>(janela, "entrada").Text = "w1[x] c1";
        Clicar(janela, "iniciar");
        Clicar(janela, "proximo");
        Obter<TextBox>(janela, "entrada").Text = "r1[x] c1";
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
        Verificar.Igual("", Obter<TextBox>(janela, "hf").Text);
    }

    [Teste]
    public void ExecutarTudoConcluiAposPassoManual()
    {
        using var janela = Abrir();
        Clicar(janela, "iniciar");
        Clicar(janela, "proximo");
        Clicar(janela, "executarTudo");
        Verificar.Igual("w1[X,10] w1[Y,11] c1 w2[Y,20] w2[X,21] c2", Obter<TextBox>(janela, "hf").Text);
        Verificar.Contem("Concluída", Obter<Label>(janela, "situacao").Text);
        Verificar.Contem("Locks: nenhum", Obter<TextBox>(janela, "estado").Text);
    }

    [Teste]
    public void BotaoExemploRestauraEntradaELimpaExecucao()
    {
        using var janela = Abrir();
        Obter<TextBox>(janela, "entrada").Text = "w1[x] c1";
        Clicar(janela, "iniciar");
        Clicar(janela, "executarTudo");
        Clicar(janela, "exemplo");
        Verificar.Contem("w2[y,20]", Obter<TextBox>(janela, "entrada").Text);
        Verificar.Igual("", Obter<TextBox>(janela, "registro").Text);
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
    }

    [Teste]
    public void JanelaMostraOverflowComoExecucaoIncompleta()
    {
        using var janela = Abrir();
        Obter<TextBox>(janela, "entrada").Text = "w1[x,2147483647] w1[x] c1";
        Clicar(janela, "iniciar");
        Clicar(janela, "executarTudo");
        Verificar.Contem("Incompleta", Obter<Label>(janela, "situacao").Text);
        Verificar.Falso(Obter<Button>(janela, "proximo").Enabled);
        Verificar.Contem("limite de inteiro", Obter<TextBox>(janela, "registro").Text);
    }
}
