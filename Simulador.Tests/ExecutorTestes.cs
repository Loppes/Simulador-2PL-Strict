using System.Collections;
using System.Reflection;

// Executor pequeno e independente de NuGet. Cada Caso gera uma execução isolada.
// Falhas retornam código 1, permitindo usar a suíte em scripts e integração contínua.
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TesteAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class CasoAttribute(params object?[]? dados) : Attribute
{
    public object?[] Dados { get; } = dados ?? [null];
}

internal static class ExecutorTestes
{
    [STAThread]
    private static int Main()
    {
        int total = 0;
        int falhas = 0;
        foreach (var tipo in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.Name))
        foreach (var metodo in tipo.GetMethods().OrderBy(m => m.Name))
        {
            if (metodo.GetCustomAttribute<TesteAttribute>() == null)
                continue;
            var casos = metodo.GetCustomAttributes<CasoAttribute>().Select(c => c.Dados).ToArray();
            if (casos.Length == 0)
                casos = [Array.Empty<object?>()];
            foreach (var dados in casos)
            {
                total++;
                string nome = $"{tipo.Name}.{metodo.Name}({string.Join(", ", dados.Select(d => d ?? "null"))})";
                try
                {
                    metodo.Invoke(Activator.CreateInstance(tipo), dados);
                    Console.WriteLine("PASSOU: " + nome);
                }
                catch (Exception erro)
                {
                    falhas++;
                    var causa = erro is TargetInvocationException ? erro.InnerException! : erro;
                    Console.Error.WriteLine($"FALHOU: {nome}\n{causa}");
                }
            }
        }
        Console.WriteLine($"Testes unitários: {total - falhas}/{total} aprovados; {falhas} falhas.");
        return total > 0 && falhas == 0 ? 0 : 1;
    }
}

internal static class Verificar
{
    public static void Verdadeiro(bool valor)
    {
        if (!valor) throw new Exception("Esperado verdadeiro.");
    }

    public static void Falso(bool valor) => Verdadeiro(!valor);

    // Compara sequências pelo conteúdo, inclusive quando suas classes são diferentes.
    public static void Igual(object? esperado, object? obtido)
    {
        if (esperado is IEnumerable a && obtido is IEnumerable b && esperado is not string && obtido is not string)
        {
            object?[] itensA = a.Cast<object?>().ToArray();
            object?[] itensB = b.Cast<object?>().ToArray();
            Igual(itensA.Length, itensB.Length);
            for (int i = 0; i < itensA.Length; i++) Igual(itensA[i], itensB[i]);
        }
        else if (!Equals(esperado, obtido))
            throw new Exception($"Esperado: {esperado ?? "null"}; obtido: {obtido ?? "null"}.");
    }

    public static T Lanca<T>(Action acao) where T : Exception
    {
        try { acao(); }
        catch (T erro) { return erro; }
        throw new Exception($"Esperada exceção {typeof(T).Name}.");
    }

    public static void Contem(string trecho, string texto)
    {
        if (!texto.Contains(trecho, StringComparison.Ordinal))
            throw new Exception($"Texto não contém: {trecho}.");
    }

    public static void NaoContem(string trecho, string texto)
    {
        if (texto.Contains(trecho, StringComparison.Ordinal))
            throw new Exception($"Texto contém trecho inesperado: {trecho}.");
    }

    public static void Vazio(IEnumerable itens) => Igual(0, itens.Cast<object?>().Count());
    public static void Unico(IEnumerable itens) => Igual(1, itens.Cast<object?>().Count());
    public static void Nulo(object? valor) => Igual(null, valor);
    public static void NaoNulo(object? valor) => Verdadeiro(valor != null);
}
