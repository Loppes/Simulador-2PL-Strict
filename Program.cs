using System.Globalization;
using System.Text;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Simulador.Tests")]

internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        return Executar(Console.In, Console.Out, Console.Error);
    }

    // Entrada/saída injetáveis permitem testar o console sem iniciar outro processo.
    internal static int Executar(TextReader entrada, TextWriter saida, TextWriter erros)
    {
        saida.WriteLine("\n=== Simulador de Escalonamento Strict 2PL =====\n");
        saida.WriteLine("Insira a HI em uma linha, com operações separadas por espaço (ex.: r1[x] w1[x] c1).");
        saida.WriteLine("Aceita [x] ou (x). Escrita sem valor incrementa em 1; com valor: w1[x,5].");
        saida.WriteLine("Identificadores de transação devem ser positivos. Cada transação deve terminar com um único commit.");
        saida.Write("HI -> ");

        try
        {
            var hi = HistoriaParser.Parse(entrada.ReadLine());
            saida.WriteLine();
            var resultado = new Simulador(hi, saida).Simular();
            return resultado.Concluida ? 0 : 2;
        }
        catch (ArgumentException erro)
        {
            erros.WriteLine("Entrada inválida: " + erro.Message);
            erros.WriteLine("Exemplo válido: r1[x] w1[x,5] c1");
            return 1;
        }
    }
}

// ============================================================
// Simulador de escalonador de transações com 2PL Strict
// ============================================================

// Uma operação da história: r (leitura), w (escrita) ou c (commit).
// Em 'w' sem valor (ex.: w1[x]), o dado é incrementado em 1 na hora da execução.
public sealed class Operacao
{
    public int Transacao { get; }
    public char Tipo { get; }       // 'r', 'w' ou 'c'
    public string Dado { get; }     // vazio no commit
    public bool TemValor { get; }   // só vale para escrita
    public int Valor { get; }

    public Operacao(int transacao, char tipo, string dado, bool temValor, int valor)
    {
        if (transacao <= 0)
            throw new ArgumentException("O identificador da transação deve ser um inteiro positivo.");
        if (tipo != 'r' && tipo != 'w' && tipo != 'c')
            throw new ArgumentException("A operação deve ser r, w ou c.");
        if (tipo == 'c' ? dado != "" : !HistoriaParser.NomeDadoValido(dado))
            throw new ArgumentException("Use um nome de dado com letras, números e sublinhado, começando por letra ou sublinhado; commit não recebe dado.");
        if ((temValor && tipo != 'w') || (!temValor && valor != 0))
            throw new ArgumentException("Somente uma escrita pode receber um valor explícito.");
        Transacao = transacao;
        Tipo = tipo;
        Dado = dado.ToUpperInvariant();
        TemValor = temValor;
        Valor = valor;
    }

    public override string ToString()
    {
        if (Tipo == 'r')
            return $"r{Transacao}[{Dado}]";
        if (Tipo == 'w')
        {
            if (TemValor)
                return $"w{Transacao}[{Dado},{Valor}]";
            return $"w{Transacao}[{Dado}]";
        }
        return $"c{Transacao}";
    }
}

// Linha da tabela de locks de um dado:
//  - Leitores: transações com lock compartilhado (S)
//  - Escritor: transação com lock exclusivo (X), ou -1 se ninguém tem
internal sealed class LockInfo
{
    public readonly List<int> Leitores = new();
    public int Escritor = -1;
}

// Guarda o valor antigo de um dado, para desfazer a escrita se a transação abortar.
internal sealed class RegistroUndo
{
    public string Dado { get; }
    public int ValorAnterior { get; }

    public RegistroUndo(string dado, int valorAnterior)
    {
        Dado = dado;
        ValorAnterior = valorAnterior;
    }
}

/// <summary>Converte a entrada e valida toda a HI antes de executar qualquer operação.</summary>
public static class HistoriaParser
{
    // Um nome começa com letra ou sublinhado e depois pode conter números.
    internal static bool NomeDadoValido(string? dado)
    {
        if (string.IsNullOrEmpty(dado))
            return false;
        if (!char.IsLetter(dado[0]) && dado[0] != '_')
            return false;
        foreach (char letra in dado)
        {
            if (!char.IsLetter(letra) && !char.IsDigit(letra) && letra != '_')
                return false;
        }
        return true;
    }

    public static List<Operacao> Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Informe uma história com pelo menos uma operação.");

        var operacoes = new List<Operacao>();
        foreach (string token in SepararOperacoes(texto))
        {
            char tipo = char.ToLowerInvariant(token[0]);
            if (tipo == 'c')
            {
                int id = LerId(token.Substring(1), token);
                operacoes.Add(new Operacao(id, 'c', "", false, 0));
                continue;
            }

            int abre = token.IndexOfAny(new char[] { '[', '(' });
            if ((tipo != 'r' && tipo != 'w') || abre < 2)
                throw new ArgumentException($"Operação inválida: '{token}'. Use r1[x], w1[x], w1[x,5] ou c1.");

            char fecha = token[abre] == '[' ? ']' : ')';
            if (token[token.Length - 1] != fecha)
                throw new ArgumentException($"Fechamento inválido em '{token}'.");
            int transacao = LerId(token.Substring(1, abre - 1), token);
            string conteudo = token.Substring(abre + 1, token.Length - abre - 2);
            string[] argumentos = conteudo.Split(',');
            string dado = argumentos[0].Trim();
            if (!NomeDadoValido(dado))
                throw new ArgumentException($"Nome de dado inválido em '{token}'. Comece com letra ou sublinhado e use letras, números ou sublinhado.");
            if (argumentos.Length > 2 || (tipo == 'r' && argumentos.Length != 1))
                throw new ArgumentException($"Quantidade de argumentos inválida em '{token}'. Somente escrita recebe valor.");

            bool temValor = argumentos.Length == 2;
            int valor = 0;
            if (temValor && !int.TryParse(argumentos[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valor))
                throw new ArgumentException($"Valor inválido em '{token}'. Use um inteiro entre {int.MinValue} e {int.MaxValue}.");
            operacoes.Add(new Operacao(transacao, tipo, dado, temValor, valor));
        }

        ValidadorHistoria.Validar(operacoes);
        return operacoes;
    }

    private static int LerId(string texto, string token)
    {
        if (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
            throw new ArgumentException($"Identificador inválido em '{token}'. Use um inteiro positivo de até {int.MaxValue}.");
        return id;
    }

    // Espaços dentro dos delimitadores pertencem ao argumento; fora, separam operações.
    private static List<string> SepararOperacoes(string texto)
    {
        var operacoes = new List<string>();
        var token = new StringBuilder();
        char fechamento = '\0';
        foreach (char caractere in texto)
        {
            if (caractere == '[' || caractere == '(')
            {
                if (fechamento != '\0')
                    throw new ArgumentException("Delimitadores aninhados não são permitidos.");
                fechamento = caractere == '[' ? ']' : ')';
            }
            else if (caractere == ']' || caractere == ')')
            {
                if (caractere != fechamento)
                    throw new ArgumentException("Delimitadores incompatíveis ou sem abertura.");
                fechamento = '\0';
            }

            if (char.IsWhiteSpace(caractere) && fechamento == '\0')
            {
                if (token.Length > 0)
                {
                    operacoes.Add(token.ToString());
                    token.Clear();
                }
            }
            else
                token.Append(caractere);
        }
        if (fechamento != '\0')
            throw new ArgumentException("Operação com delimitador sem fechamento.");
        if (token.Length > 0)
            operacoes.Add(token.ToString());
        return operacoes;
    }
}

public static class ValidadorHistoria
{
    public static void Validar(IReadOnlyCollection<Operacao> historia, bool exigirCommit = true)
    {
        ArgumentNullException.ThrowIfNull(historia);
        if (historia.Count == 0)
            throw new ArgumentException("A história não pode ser vazia.");
        var presentes = new HashSet<int>();
        var encerradas = new HashSet<int>();
        foreach (var op in historia)
        {
            if (op is null)
                throw new ArgumentException("A história não pode conter operações nulas.");
            if (encerradas.Contains(op.Transacao))
                throw new ArgumentException(op.Tipo == 'c'
                    ? $"Commit repetido para T{op.Transacao}."
                    : $"Operação {op} após o commit de T{op.Transacao}.");
            presentes.Add(op.Transacao);
            if (op.Tipo == 'c')
                encerradas.Add(op.Transacao);
        }
        if (exigirCommit)
        {
            var semCommit = presentes.Except(encerradas).Order().Select(t => $"T{t}").ToArray();
            if (semCommit.Length > 0)
                throw new ArgumentException("Falta commit para: " + string.Join(", ", semCommit) + ".");
        }
    }
}

public sealed class Simulador
{
    const int NENHUM = -1;
    private readonly TextWriter _saida;
    private readonly StringBuilder _mensagensPasso = new StringBuilder();
    private readonly HashSet<int> _confirmadas = new();
    private bool _iniciada;
    private bool _finalizada;
    private string? _erro;
    public IReadOnlyList<Operacao> HistoriaInicial { get; }
    public IReadOnlyList<Operacao> HistoriaRestante => _hi.GetRange(_pos, _hi.Count - _pos).AsReadOnly();
    public bool Finalizada => _finalizada;

    // ---------- estruturas de dados ----------
    private List<Operacao> _hi;                                           // história inicial (cresce quando uma transação é reinserida)
    private List<Operacao> _hf = new List<Operacao>();                    // história final
    private SortedDictionary<string, int> _dados = new SortedDictionary<string, int>();   // banco de dados em memória
    private SortedDictionary<string, LockInfo> _locks = new SortedDictionary<string, LockInfo>(); // tabela de locks
    private Dictionary<int, List<Operacao>> _opsTx = new Dictionary<int, List<Operacao>>(); // operações originais de cada transação
    private Dictionary<int, Queue<Operacao>> _esperas = new Dictionary<int, Queue<Operacao>>();  // fila de delay de cada transação
    private Dictionary<int, List<RegistroUndo>> _undo = new Dictionary<int, List<RegistroUndo>>(); // log para rollback
    private List<int> _bloqueadas = new List<int>();                      // transações em delay, na ordem em que bloquearam
    private int _pos;        // posição da operação atual na HI
    private int _deadlocks;

    // variáveis usadas pela DFS que procura ciclo no grafo de espera
    private Dictionary<int, List<int>> _grafo = new Dictionary<int, List<int>>();
    private Dictionary<int, int> _estado = new Dictionary<int, int>();   // 1 = na pilha, 2 = terminado
    private List<int> _pilha = new List<int>();
    private List<int> _ciclo = new List<int>();

    // Histórias parciais são aceitas aqui para diagnóstico. A entrada do console
    // exige commits para todas as transações antes de iniciar a execução.
    public Simulador(IEnumerable<Operacao> hi, TextWriter? saida = null)
    {
        ArgumentNullException.ThrowIfNull(hi);
        _hi = new List<Operacao>(hi);
        ValidadorHistoria.Validar(_hi, exigirCommit: false);
        HistoriaInicial = Array.AsReadOnly(_hi.ToArray());
        _saida = saida ?? Console.Out;

        foreach (Operacao op in _hi)
        {
            int t = op.Transacao;

            // primeira vez que a transação aparece: cria suas estruturas
            if (!_opsTx.ContainsKey(t))
            {
                _opsTx[t] = new List<Operacao>();
                _esperas[t] = new Queue<Operacao>();
                _undo[t] = new List<RegistroUndo>();
            }
            _opsTx[t].Add(op);

            // cada dado usado na HI começa com valor 0 e ganha uma linha na tabela de locks
            if (op.Tipo != 'c' && !_dados.ContainsKey(op.Dado))
            {
                _dados[op.Dado] = 0;
                _locks[op.Dado] = new LockInfo();
            }
        }
    }

    // ============================================================
    // Laço principal: envia as operações da HI ao escalonador, na ordem
    // ============================================================
    public ResultadoSimulacao Simular()
    {
        if (_iniciada)
            throw new InvalidOperationException("Cada simulador só pode executar uma vez. Crie outro para uma nova história.");
        while (ExecutarProximoPasso() != null) { }
        return EstadoAtual;
    }

    public ResultadoSimulacao EstadoAtual => CriarResultado(_erro);

    // A interface chama este método uma vez por clique. O console repete até o fim.
    public PassoSimulacao? ExecutarProximoPasso()
    {
        if (_finalizada)
            return null;
        if (!_iniciada)
        {
            _iniciada = true;
            Escrever("HI: " + string.Join(" ", _hi));
            Escrever();
        }

        _mensagensPasso.Clear();
        Operacao op = _hi[_pos];
        int numero = _pos + 1;
        Escrever($"--- Passo {numero}: chega {op} ---");
        try
        {
            Chegar(op);
            Estabilizar();
        }
        catch (OverflowException)
        {
            _erro = "Um incremento excedeu o limite de inteiro de 32 bits. A escrita não foi aplicada.";
            Escrever("  ERRO: " + _erro);
        }
        ImprimirEstado();
        Escrever();

        // O abort usa a posição atual para mover a vítima para o fim da HI.
        // Por isso só avançamos o índice depois de estabilizar o escalonamento.
        _pos++;
        _finalizada = _erro != null || _pos >= _hi.Count;
        ResultadoSimulacao resultado = EstadoAtual;
        if (_finalizada)
            ImprimirResultado(resultado);
        return new PassoSimulacao(numero, op, resultado, HistoriaRestante, _mensagensPasso.ToString(), _finalizada);
    }

    // Trata uma operação que acabou de chegar da HI.
    private void Chegar(Operacao op)
    {
        int t = op.Transacao;

        // transação já em delay: a operação só entra na fila de espera dela
        if (_bloqueadas.Contains(t))
        {
            _esperas[t].Enqueue(op);
            Escrever($"  T{t} está em delay; {op} entra na fila de espera da transação.");
            return;
        }

        // não consegue o lock: a transação entra em delay
        if (!PodeExecutar(op))
        {
            _bloqueadas.Add(t);
            _esperas[t].Enqueue(op);
            string tipoLock = "exclusivo";
            if (op.Tipo == 'r')
                tipoLock = "compartilhado";
            Escrever($"  {op} não consegue o lock {tipoLock} em {op.Dado} (conflito com {ListaT(Conflitantes(op))}). T{t} entra em DELAY.");
            return;
        }

        Aplicar(op, false);
    }

    // Tira do delay quem pode; depois, se houver deadlock, resolve e repete.
    private void Estabilizar()
    {
        do
        {
            ResolverEsperas();
        } while (DetectarEResolverUmDeadlock());
    }

    // ============================================================
    // Locks
    // ============================================================

    // Lista as transações que impedem a operação de obter o lock que ela precisa.
    //  - leitura (S): conflita com outro escritor
    //  - escrita (X): conflita com outro escritor e com qualquer outro leitor
    private List<int> Conflitantes(Operacao op)
    {
        List<int> resultado = new List<int>();
        if (op.Tipo == 'c')
            return resultado;

        LockInfo info = _locks[op.Dado];
        int t = op.Transacao;

        if (info.Escritor != NENHUM && info.Escritor != t)
            resultado.Add(info.Escritor);

        if (op.Tipo == 'w')
        {
            foreach (int leitor in info.Leitores)
            {
                if (leitor != t)
                    resultado.Add(leitor);
            }
        }
        return resultado;
    }

    private bool PodeExecutar(Operacao op)
    {
        return Conflitantes(op).Count == 0;
    }

    private void ConcederLock(Operacao op)
    {
        int t = op.Transacao;
        LockInfo info = _locks[op.Dado];

        if (op.Tipo == 'r')
        {
            // já tem S ou X neste dado: não precisa de lock novo
            if (info.Escritor == t || info.Leitores.Contains(t))
                return;

            info.Leitores.Add(t);
            Escrever($"  lock: sl{t}[{op.Dado}] concedido (compartilhado).");
        }
        else
        {
            if (info.Escritor == t)
                return;

            // se a transação já era leitora, é um upgrade de S para X
            bool upgrade = info.Leitores.Remove(t);
            info.Escritor = t;

            if (upgrade)
                Escrever($"  lock: xl{t}[{op.Dado}] concedido (upgrade S -> X).");
            else
                Escrever($"  lock: xl{t}[{op.Dado}] concedido (exclusivo).");
        }
    }

    // Strict 2PL: todos os locks da transação são liberados juntos, no commit (ou no abort).
    private void LiberarLocks(int t)
    {
        string liberados = "";

        foreach (KeyValuePair<string, LockInfo> par in _locks)
        {
            LockInfo info = par.Value;

            if (info.Escritor == t)
            {
                info.Escritor = NENHUM;
                liberados += $" ul{t}[{par.Key}]";
            }
            if (info.Leitores.Remove(t))
                liberados += $" ul{t}[{par.Key}]";
        }

        _bloqueadas.Remove(t);
        _esperas[t].Clear();

        if (liberados == "")
            Escrever($"  unlock: T{t} não possuía locks.");
        else
            Escrever($"  unlock:{liberados}.");
    }

    // ============================================================
    // Execução de operações
    // ============================================================

    // Executa uma operação que já pode rodar (o lock está liberado) e coloca na HF.
    private void Aplicar(Operacao op, bool vindaDoDelay)
    {
        int t = op.Transacao;
        string sufixo = "";
        if (vindaDoDelay)
            sufixo = " [executada após delay]";

        if (op.Tipo == 'c')
        {
            _hf.Add(op);
            _confirmadas.Add(t);
            _undo[t].Clear();
            Escrever($"  {op}{sufixo}: commit de T{t}; vai para a HF.");
            LiberarLocks(t);
            return;
        }

        ConcederLock(op);

        if (op.Tipo == 'r')
        {
            _hf.Add(op);
            Escrever($"  {op}{sufixo}: lê {op.Dado} = {_dados[op.Dado]}; vai para a HF.");
        }
        else
        {
            int antigo = _dados[op.Dado];
            int novo;
            if (op.TemValor)
                novo = op.Valor;
            else
                novo = checked(antigo + 1);

            _undo[t].Add(new RegistroUndo(op.Dado, antigo));
            _dados[op.Dado] = novo;
            _hf.Add(op);
            Escrever($"  {op}{sufixo}: escreve {op.Dado} = {novo} (antes: {antigo}); vai para a HF.");
        }
    }

    // Tenta executar, em ordem, as operações que estão em delay.
    // A transação só sai de _bloqueadas quando a fila dela esvazia; se a próxima
    // operação ainda conflita, ela continua em delay.
    private void ResolverEsperas()
    {
        bool progresso;

        do
        {
            progresso = false;

            // cópia da lista, porque _bloqueadas muda durante o laço
            List<int> copia = new List<int>(_bloqueadas);
            foreach (int t in copia)
            {
                Queue<Operacao> fila = _esperas[t];
                bool executou = false;

                while (fila.Count > 0 && PodeExecutar(fila.Peek()))
                {
                    Operacao prox = fila.Dequeue();
                    Aplicar(prox, true);
                    executou = true;
                    progresso = true;
                }

                if (fila.Count == 0)
                {
                    _bloqueadas.Remove(t);
                    if (executou)
                        Escrever($"  T{t} saiu do delay.");
                }
            }
        } while (progresso);
    }

    // ============================================================
    // Deadlock: grafo de espera + DFS
    // ============================================================

    // Aresta Ti -> Tj: a operação em delay de Ti precisa de um lock que Tj tem.
    private Dictionary<int, List<int>> MontarGrafoEspera()
    {
        Dictionary<int, List<int>> grafo = new Dictionary<int, List<int>>();

        foreach (int t in _bloqueadas)
        {
            Queue<Operacao> fila = _esperas[t];
            if (fila.Count > 0)
                grafo[t] = Conflitantes(fila.Peek());
            else
                grafo[t] = new List<int>();
        }
        return grafo;
    }

    // Procura um ciclo com DFS. Devolve a lista de transações do ciclo (vazia se não houver).
    private List<int> EncontrarCiclo(Dictionary<int, List<int>> grafo)
    {
        _grafo = grafo;
        _estado = new Dictionary<int, int>();
        _pilha = new List<int>();
        _ciclo = new List<int>();

        foreach (int no in grafo.Keys)
        {
            if (!_estado.ContainsKey(no))
            {
                Visitar(no);
                if (_ciclo.Count > 0)
                    return _ciclo;
            }
        }
        return _ciclo;
    }

    private void Visitar(int atual)
    {
        _estado[atual] = 1;       // entrou na pilha
        _pilha.Add(atual);

        if (_grafo.ContainsKey(atual))
        {
            foreach (int prox in _grafo[atual])
            {
                if (!_estado.ContainsKey(prox))
                {
                    Visitar(prox);
                }
                else if (_estado[prox] == 1)
                {
                    // voltou para um nó que ainda está na pilha: achou o ciclo
                    int inicio = _pilha.IndexOf(prox);
                    for (int i = inicio; i < _pilha.Count; i++)
                        _ciclo.Add(_pilha[i]);
                }

                if (_ciclo.Count > 0)
                    return;
            }
        }

        _pilha.RemoveAt(_pilha.Count - 1);
        _estado[atual] = 2;       // terminou
    }

    // Resolve um ciclo por chamada; Estabilizar repete enquanto houver ciclos.
    private bool DetectarEResolverUmDeadlock()
    {
        List<int> ciclo = EncontrarCiclo(MontarGrafoEspera());
        if (ciclo.Count == 0)
            return false;

        _deadlocks++;
        Escrever($"  *** DEADLOCK #{_deadlocks} detectado: o grafo de espera tem um ciclo ***");

        for (int i = 0; i < ciclo.Count; i++)
        {
            int de = ciclo[i];
            int para = ciclo[(i + 1) % ciclo.Count];
            Operacao pendente = _esperas[de].Peek();
            Escrever($"    T{de} espera T{para}  ({pendente} precisa de lock em {pendente.Dado})");
        }

        // Critério determinístico: maior identificador no ciclo, sem inferir idade.
        int vitima = ciclo[0];
        foreach (int t in ciclo)
        {
            if (t > vitima)
                vitima = t;
        }

        Escrever($"  Critério: abortar a transação de maior identificador no ciclo. Vítima: T{vitima}.");
        Abortar(vitima);
        return true;
    }

    private void Abortar(int t)
    {
        Escrever($"  Abortando T{t}:");

        // 1) rollback: desfaz as escritas, da última para a primeira
        for (int i = _undo[t].Count - 1; i >= 0; i--)
        {
            RegistroUndo r = _undo[t][i];
            Escrever($"    rollback: {r.Dado} volta de {_dados[r.Dado]} para {r.ValorAnterior}.");
            _dados[r.Dado] = r.ValorAnterior;
        }
        _undo[t].Clear();

        // 2) retira as operações de T da HF
        int removidas = 0;
        for (int i = _hf.Count - 1; i >= 0; i--)
        {
            if (_hf[i].Transacao == t)
            {
                _hf.RemoveAt(i);
                removidas++;
            }
        }
        Escrever($"    {removidas} operação(ões) de T{t} retirada(s) da HF.");

        // 3) libera os locks e descarta a fila de delay
        LiberarLocks(t);

        // 4) tira da HI o que ainda restava da execução antiga de T (depois da posição atual)
        for (int i = _hi.Count - 1; i > _pos; i--)
        {
            if (_hi[i].Transacao == t)
                _hi.RemoveAt(i);
        }

        // 5) recoloca a transação inteira no final da HI
        _hi.AddRange(_opsTx[t]);
        Escrever($"    T{t} reinserida no final da HI.");
        Escrever("    HI restante: " + string.Join(" ", _hi.GetRange(_pos + 1, _hi.Count - _pos - 1)));
    }

    // ============================================================
    // Saída
    // ============================================================
    private string ListaT(List<int> ids)
    {
        string texto = "";
        foreach (int id in ids)
        {
            if (texto != "")
                texto += ", ";
            texto += "T" + id;
        }
        return texto;
    }

    private void ImprimirEstado()
    {
        // HF
        if (_hf.Count == 0)
            Escrever("  HF: (vazia)");
        else
            Escrever("  HF: " + string.Join(" ", _hf));

        // tabela de locks (só os dados que têm algum lock)
        string tabela = "";
        foreach (KeyValuePair<string, LockInfo> par in _locks)
        {
            LockInfo info = par.Value;
            string item = "";

            if (info.Escritor != NENHUM)
                item = $"{par.Key}: X(T{info.Escritor})";
            else if (info.Leitores.Count > 0)
                item = $"{par.Key}: S({ListaT(info.Leitores)})";

            if (item != "")
            {
                if (tabela != "")
                    tabela += " | ";
                tabela += item;
            }
        }
        if (tabela == "")
            tabela = "(nenhum)";
        Escrever("  Locks: " + tabela);

        // transações em delay, com as operações que aguardam
        string delays = "";
        foreach (int t in _bloqueadas)
        {
            if (delays != "")
                delays += "  ";
            delays += $"T{t}[{string.Join(" ", _esperas[t])}]";
        }
        if (delays == "")
            delays = "(nenhuma)";
        Escrever("  Delay: " + delays);
    }

    // A mesma mensagem aparece no console e no registro do passo da interface.
    private void Escrever(string mensagem = "")
    {
        _saida.WriteLine(mensagem);
        _mensagensPasso.AppendLine(mensagem);
    }

    private ResultadoSimulacao CriarResultado(string? erro)
    {
        var pendentes = new List<int>();
        foreach (int t in _opsTx.Keys)
        {
            if (!_confirmadas.Contains(t))
                pendentes.Add(t);
        }
        pendentes.Sort();

        var locks = new List<string>();
        foreach (var par in _locks)
        {
            LockInfo info = par.Value;
            if (info.Escritor != NENHUM)
                locks.Add($"{par.Key}: X(T{info.Escritor})");
            else if (info.Leitores.Count > 0)
                locks.Add($"{par.Key}: S({ListaT(info.Leitores)})");
        }
        var bloqueadas = new List<int>(_bloqueadas);
        bloqueadas.Sort();
        // As cópias impedem que a interface ou os testes alterem o motor.
        return new ResultadoSimulacao(
            new List<Operacao>(_hf).AsReadOnly(),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_dados)),
            _deadlocks, pendentes.AsReadOnly(), bloqueadas.AsReadOnly(), locks.AsReadOnly(), erro);
    }

    private void ImprimirResultado(ResultadoSimulacao resultado)
    {
        Escrever("=== HISTÓRIA FINAL (HF) =====");
        if (_hf.Count == 0)
            Escrever("(vazia)");
        else
            Escrever(string.Join(" ", _hf));
        Escrever();

        Escrever("=== VALORES FINAIS =====");
        foreach (KeyValuePair<string, int> item in _dados)
            Escrever($"{item.Key} = {item.Value}");
        Escrever();

        Escrever("=== RESUMO =====");
        Escrever($"Deadlocks detectados: {_deadlocks}");
        if (resultado.Concluida)
            Escrever("Simulação concluída: todas as transações confirmadas e nenhum lock ativo.");
        else
        {
            Escrever("AVISO: simulação incompleta; os valores exibidos podem incluir escritas não confirmadas.");
            Escrever("Transações sem commit executado: " + ListaT(resultado.TransacoesPendentes.ToList()));
            Escrever("Transações em delay: " + (resultado.TransacoesEmDelay.Count == 0 ? "(nenhuma)" : ListaT(resultado.TransacoesEmDelay.ToList())));
            Escrever("Locks ainda ativos: " + (resultado.LocksAtivos.Count == 0 ? "(nenhum)" : string.Join(" | ", resultado.LocksAtivos)));
        }
    }
}

// Guarda o que aconteceu em uma chamada de ExecutarProximoPasso.
public class PassoSimulacao
{
    public int Numero { get; }
    public Operacao Chegada { get; }
    public ResultadoSimulacao Estado { get; }
    public IReadOnlyList<Operacao> HistoriaRestante { get; }
    public string Mensagens { get; }
    public bool Finalizado { get; }

    public PassoSimulacao(int numero, Operacao chegada, ResultadoSimulacao estado,
        IReadOnlyList<Operacao> historiaRestante, string mensagens, bool finalizado)
    {
        Numero = numero;
        Chegada = chegada;
        Estado = estado;
        HistoriaRestante = historiaRestante;
        Mensagens = mensagens;
        Finalizado = finalizado;
    }
}

// Guarda uma cópia do resultado para mostrar na tela ou conferir nos testes.
public class ResultadoSimulacao
{
    public IReadOnlyList<Operacao> HistoriaFinal { get; }
    public IReadOnlyDictionary<string, int> Dados { get; }
    public int Deadlocks { get; }
    public IReadOnlyList<int> TransacoesPendentes { get; }
    public IReadOnlyList<int> TransacoesEmDelay { get; }
    public IReadOnlyList<string> LocksAtivos { get; }
    public string? Erro { get; }
    public bool Concluida => Erro == null && TransacoesPendentes.Count == 0
        && TransacoesEmDelay.Count == 0 && LocksAtivos.Count == 0;

    public ResultadoSimulacao(IReadOnlyList<Operacao> historiaFinal,
        IReadOnlyDictionary<string, int> dados, int deadlocks,
        IReadOnlyList<int> transacoesPendentes, IReadOnlyList<int> transacoesEmDelay,
        IReadOnlyList<string> locksAtivos, string? erro)
    {
        HistoriaFinal = historiaFinal;
        Dados = dados;
        Deadlocks = deadlocks;
        TransacoesPendentes = transacoesPendentes;
        TransacoesEmDelay = transacoesEmDelay;
        LocksAtivos = locksAtivos;
        Erro = erro;
    }
}
