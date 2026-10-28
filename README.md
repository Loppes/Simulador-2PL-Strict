# Simulador Strict 2PL

Trabalho de Banco de Dados em **C# / .NET 10**. Simula transações com locks compartilhados e exclusivos, delay, commit e tratamento de deadlocks.

## Como rodar

É necessário **Windows e SDK .NET 10**.

```powershell
git clone https://github.com/Loppes/Simulador2PL.git
cd Simulador2PL
dotnet run --project .\Simulador.Grafico\Simulador.Grafico.csproj
```

Também é possível abrir **Abrir Simulador.cmd** com dois cliques.

Na interface, digite a história e clique em **Iniciar / Reiniciar**. Use **Próximo passo** ou **Executar tudo** para acompanhar HI restante, HF, dados, locks e transações em espera.

Para executar pelo console:

```powershell
dotnet run --project .\Desktop.csproj
```

## Entrada

Separe as operações por espaços. Cada transação deve terminar com um único commit.

| Operação | Significado |
| --- | --- |
| `r1[x]` | T1 lê X. |
| `w1[x,5]` | T1 escreve 5 em X. |
| `w1[x]` | T1 incrementa X em 1. |
| `c1` | T1 confirma e libera seus locks. |

Os dados começam em zero. Locks e unlocks são inseridos pelo simulador.

Exemplo com deadlock:

```text
w1[x,10] w2[y,20] w1[y,11] w2[x,21] c1 c2
```

## Funcionamento

- Leitura solicita lock S; escrita solicita X. Os locks são mantidos até commit ou abort.
- Operações sem lock disponível ficam em delay, preservando a ordem da transação.
- Deadlocks são detectados por ciclos no grafo de espera.
- A vítima é a transação de maior identificador no ciclo. Suas escritas são desfeitas, suas operações são removidas da HF e ela reinicia ao final da HI.
- A saída mostra a execução passo a passo, a HF, os valores dos dados e a quantidade de deadlocks.

Os dados ficam somente em memória. A interface usa o mesmo motor do console.

## Código

- **Program.cs:** console, operações, leitura da HI, locks, delay e tratamento de deadlocks.
- **Simulador.Grafico/FormSimulador.cs:** janela e botões que chamam o simulador.
- **Simulador.Tests/:** testes separados do código da aplicação.

## Testes

Feche a interface antes de executar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Testes.ps1
```

A suíte contém **85 testes automatizados e 8 cenários de console**, com executor próprio e sem pacotes externos.
