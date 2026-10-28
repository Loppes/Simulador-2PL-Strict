# Referência do trabalho — simulador Strict 2PL

Este arquivo registra o enunciado e deve orientar futuras alterações. Não confundir requisitos com funcionalidades já verificadas.

## Organização e entrega

- Trabalho em grupos de 2 ou 3 participantes; trabalhos individuais não são aceitos. Inscrever o grupo somente após definir os participantes.
- Apresentação: 20 ou 27/10/2026.
- Entrega no AVA: relatório, cópia do código-fonte e executável, se aplicável.

## Objetivo e comportamento obrigatório

- Simular um escalonador de transações com protocolo Strict 2PL.
- Receber uma história inicial (HI), com operações identificadas por transação: leitura (`r`), escrita (`w`) e commit.
- Representar os dados por variáveis em memória; persistência em arquivo não é exigida.
- Submeter as operações ao escalonador na ordem da HI, preservando a ordem interna de cada transação.
- Para leitura, solicitar lock compartilhado (S); para escrita, exclusivo (X). Permitir upgrade S → X quando compatível.
- Conceder locks compatíveis, executar a operação e adicioná-la à história final (HF). Escritas devem atualizar os dados.
- Quando houver conflito, colocar a operação e sua transação em delay; operações posteriores dessa transação devem aguardar.
- No commit, liberar os locks da transação e reavaliar as transações em delay.
- O simulador deve inserir locks e unlocks; essas operações não pertencem à entrada HI.
- Detectar deadlocks e explicar sua detecção e tratamento. Definir um critério de escolha da vítima.
- Abortar a vítima, desfazer suas escritas, remover suas operações anteriores da HF e liberar seus locks.
- Recolocar a transação abortada inteira no final da HI para nova execução.
- Exibir a HF, preferencialmente mostrando sua construção passo a passo, com operações executadas, bloqueadas e removidas.

## Conteúdo obrigatório do relatório

- Pequena descrição do trabalho realizado.
- Características implementadas e não implementadas.
- Detecção e tratamento de deadlocks, incluindo critério da vítima.
- Formato de entrada e saída produzida.
- Estruturas para transações, histórias, controle de execução e dados.
- Tabela de locks e gerenciamento de concessão e liberação.

## Convenções da implementação atual

- Aplicação de console C# / .NET 10; execução com `dotnet run`.
- HI em uma linha: `r1[x] w1[x,5] c1`; também aceita parênteses.
- Dados começam em zero; `w1[x]` incrementa o valor atual em 1.
- Mantém locks S e X até commit ou abort (regra mais restritiva, também chamada rigorous 2PL, que satisfaz Strict 2PL).
- Grafo de espera com busca em profundidade detecta ciclos.
- A vítima é a transação com maior identificador no ciclo. Isso não implica ser a mais nova cronologicamente; é uma convenção numérica.
- Rollback usa log de valores anteriores em ordem inversa.
- A entrada é validada integralmente antes de executar: tipos r/w/c, delimitadores, argumentos, nomes de dados e números inteiros de 32 bits. IDs de transação são positivos.
- Cada transação precisa de um único commit final; operações posteriores ao commit e histórias sem commits são rejeitadas pelo console.
- O resultado informa conclusão ou execução incompleta, com transações pendentes, delay e locks ativos. Incremento que excede o limite de inteiro interrompe a simulação sem aplicar a escrita.

## Organização do código

- `Program.cs`: entrada do console, operações, leitura e validação da HI, tabela de locks, delay, deadlocks, rollback e resultados. As classes estão juntas no mesmo arquivo.
- `Simulador.Grafico/FormSimulador.cs`: tela Windows Forms simples e sua inicialização. Chama o simulador diretamente.
- `Simulador.Tests/`: testes unitários com executor local, sem dependências externas.

A entrada é lida com índices, `Substring` e laços, sem expressões regulares. As mensagens do console e da interface passam pelo método `Escrever`. Os resultados usam classes comuns com construtores. As estruturas continuam sendo listas, dicionários, filas e o grafo de espera.

## Como verificar

Executar `powershell -ExecutionPolicy Bypass -File .\Testes.ps1`.
O script executa os testes unitários e os oito cenários de console. Para apenas os unitários, usar `dotnet run --project .\Simulador.Tests\Simulador.Tests.csproj` (não usar `dotnet test`: o executor é próprio).
Consultar `RESULTADO_TESTES.md` para evidências e pendências da revisão.

## Interface para apresentação

No Windows, abrir `Abrir Simulador.cmd` ou executar `dotnet run --project .\Simulador.Grafico\Simulador.Grafico.csproj`.

1. Digitar a HI ou clicar em **Exemplo de deadlock**.
2. Clicar em **Iniciar / Reiniciar**: valida a entrada e mostra os dados zerados, sem executar operações.
3. Usar **Próximo passo** para enviar uma operação da HI por clique ou **Executar tudo** para concluir a história.
4. Acompanhar as caixas de HI restante, HF, dados, locks e delay. A caixa de execução registra todos os passos, incluindo detecção do ciclo, rollback e reinserção da vítima.
5. Clicar em **Iniciar / Reiniciar** para repetir. Editar a HI descarta a execução anterior.

Não há reprodução com temporizador, painéis de métricas ou abas. `ExecutarProximoPasso()` controla um passo do motor com um índice na HI; o console repete essa chamada até terminar.

Interface e testes de controles exigem Windows com .NET 10 Desktop Runtime; para compilar, usar o SDK .NET 10. O console continua disponível com `dotnet run` na pasta principal.
