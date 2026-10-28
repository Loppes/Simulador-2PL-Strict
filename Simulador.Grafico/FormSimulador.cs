namespace Simulador2PL.Grafico;

// Uma tela simples: a entrada cria o simulador e cada botão chama o motor.
public sealed class FormSimulador : Form
{
    private const string Exemplo = "w1[x,10] w2[y,20] w1[y,11] w2[x,21] c1 c2";
    private readonly TextBox _entrada = CaixaTexto("entrada", false);
    private readonly TextBox _hi = CaixaTexto("hi");
    private readonly TextBox _hf = CaixaTexto("hf");
    private readonly TextBox _estado = CaixaTexto("estado");
    private readonly TextBox _registro = CaixaTexto("registro");
    private readonly Label _situacao = new() { Name = "situacao", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _iniciar = new() { Name = "iniciar", Text = "Iniciar / Reiniciar", AutoSize = true };
    private readonly Button _proximo = new() { Name = "proximo", Text = "Próximo passo", AutoSize = true };
    private readonly Button _executarTudo = new() { Name = "executarTudo", Text = "Executar tudo", AutoSize = true };
    private readonly Button _exemplo = new() { Name = "exemplo", Text = "Exemplo de deadlock", AutoSize = true };
    private Simulador? _simulador;

    public FormSimulador()
    {
        Text = "Trabalho de Banco de Dados — Simulador Strict 2PL";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 740);
        MinimumSize = new Size(800, 640);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tela = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 8 };
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        tela.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tela.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        Controls.Add(tela);

        tela.Controls.Add(Grupo("História inicial (HI)", _entrada), 0, 0);
        tela.Controls.Add(new Label { Text = "Leitura: r1[x]   Escrita: w1[x,5] ou w1[x] (+1)   Commit: c1. Separe as operações por espaços.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        var botoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        botoes.Controls.AddRange([_iniciar, _proximo, _executarTudo, _exemplo]);
        tela.Controls.Add(botoes, 0, 2);
        tela.Controls.Add(_situacao, 0, 3);

        var historias = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        historias.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        historias.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        historias.Controls.Add(Grupo("HI restante", _hi), 0, 0);
        historias.Controls.Add(Grupo("História final (HF)", _hf), 1, 0);
        tela.Controls.Add(historias, 0, 4);
        tela.Controls.Add(Grupo("Dados, locks e transações em espera", _estado), 0, 5);
        tela.Controls.Add(Grupo("Execução passo a passo", _registro), 0, 6);
        tela.Controls.Add(new Label { Text = "Locks S e X são mantidos até commit/abort. Em deadlock, aborta a transação de maior identificador no ciclo.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 7);

        _iniciar.Click += (_, _) => Iniciar();
        _proximo.Click += (_, _) => Avancar();
        _executarTudo.Click += (_, _) =>
        {
            while (_simulador != null && !_simulador.Finalizada) Avancar();
        };
        _exemplo.Click += (_, _) => { _entrada.Text = Exemplo; Limpar(); };
        _entrada.TextChanged += (_, _) => Limpar();
        _entrada.Text = Exemplo;
        Limpar();
        AcceptButton = _iniciar;
    }

    private void Iniciar()
    {
        Limpar();
        try
        {
            var historia = HistoriaParser.Parse(_entrada.Text);
            _simulador = new Simulador(historia, TextWriter.Null);
            _registro.Text = "HI: " + string.Join(" ", historia) + Environment.NewLine + Environment.NewLine;
            AtualizarTela();
            _situacao.Text = "História validada. Clique em Próximo passo ou Executar tudo.";
        }
        catch (ArgumentException erro)
        {
            _situacao.Text = "Entrada inválida.";
            _registro.Text = erro.Message + Environment.NewLine + "Nenhuma operação foi executada.";
        }
    }

    private void Avancar()
    {
        PassoSimulacao? passo = _simulador?.ExecutarProximoPasso();
        if (passo == null) return;
        _registro.AppendText(passo.Mensagens);
        _registro.SelectionStart = _registro.TextLength;
        _registro.ScrollToCaret();
        AtualizarTela();
        string situacao = passo.Finalizado ? (passo.Estado.Concluida ? "Concluída" : "Incompleta") : "Em execução";
        _situacao.Text = $"Passo {passo.Numero}: {passo.Chegada}  |  Deadlocks: {passo.Estado.Deadlocks}  |  {situacao}";
    }

    private void AtualizarTela()
    {
        if (_simulador == null) return;
        ResultadoSimulacao estado = _simulador.EstadoAtual;
        _hi.Text = string.Join(" ", _simulador.HistoriaRestante);
        _hf.Text = string.Join(" ", estado.HistoriaFinal);
        string dados = string.Join("   ", estado.Dados.Select(d => $"{d.Key} = {d.Value}"));
        string locks = estado.LocksAtivos.Count == 0 ? "nenhum" : string.Join("   ", estado.LocksAtivos);
        string delay = estado.TransacoesEmDelay.Count == 0 ? "nenhuma" : string.Join(", ", estado.TransacoesEmDelay.Select(t => $"T{t}"));
        _estado.Lines = [$"Dados: {dados}", $"Locks: {locks}", $"Delay: {delay}"];
        _proximo.Enabled = _executarTudo.Enabled = !_simulador.Finalizada;
    }

    private void Limpar()
    {
        _simulador = null;
        _proximo.Enabled = _executarTudo.Enabled = false;
        _hi.Clear();
        _hf.Clear();
        _estado.Clear();
        _registro.Clear();
        _situacao.Text = "Digite a HI e clique em Iniciar / Reiniciar.";
    }

    private static TextBox CaixaTexto(string nome, bool somenteLeitura = true) => new()
    {
        Name = nome, Dock = DockStyle.Fill, Multiline = true, ReadOnly = somenteLeitura,
        ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10), BackColor = Color.White
    };

    private static GroupBox Grupo(string titulo, Control conteudo)
    {
        var grupo = new GroupBox { Text = titulo, Dock = DockStyle.Fill, Padding = new Padding(8) };
        grupo.Controls.Add(conteudo);
        return grupo;
    }
}

internal static class ProgramaGrafico
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FormSimulador());
    }
}
