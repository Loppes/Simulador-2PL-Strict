$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project .\Simulador.Tests\Simulador.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes unitários.' }
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }
    $casos = @(
        @{ Nome = 'Leitura, upgrade e escrita'; HI = 'r1[x] w1[x,5] c1'; HF = 'r1[X] w1[X,5] c1'; Dados = @('X = 5'); Deadlocks = 0; Trechos = @('upgrade S -> X') },
        @{ Nome = 'Leitores compartilhados'; HI = 'r1[x] r2[x] c1 c2'; HF = 'r1[X] r2[X] c1 c2'; Dados = @('X = 0'); Deadlocks = 0; Trechos = @('S(T1, T2)') },
        @{ Nome = 'Delay e commit enfileirado'; HI = 'w1[x,7] r2[x] w2[y,4] c2 c1'; HF = 'w1[X,7] c1 r2[X] w2[Y,4] c2'; Dados = @('X = 7', 'Y = 4'); Deadlocks = 0; Trechos = @('executada após delay', 'lê X = 7') },
        @{ Nome = 'Deadlock, rollback e reinício'; HI = 'w1[x,10] w2[y,20] w1[y,11] w2[x,21] c1 c2'; HF = 'w1[X,10] w1[Y,11] c1 w2[Y,20] w2[X,21] c2'; Dados = @('X = 21', 'Y = 20'); Deadlocks = 1; Trechos = @('Vítima: T2', 'rollback: Y volta de 20 para 0', 'reinserida no final da HI') },
        @{ Nome = 'Deadlock em upgrade'; HI = 'r1[x] r2[x] w1[x] w2[x] c1 c2'; HF = 'r1[X] w1[X] c1 r2[X] w2[X] c2'; Dados = @('X = 2'); Deadlocks = 1; Trechos = @('Vítima: T2') },
        @{ Nome = 'Rollback de várias escritas'; HI = 'w1[x] w2[y,3] w2[y,7] w1[y] w2[x] c1 c2'; HF = 'w1[X] w1[Y] c1 w2[Y,3] w2[Y,7] w2[X] c2'; Dados = @('X = 2', 'Y = 7'); Deadlocks = 1; Trechos = @('rollback: Y volta de 7 para 3', 'rollback: Y volta de 3 para 0') },
        @{ Nome = 'Ciclo com três transações'; HI = 'w1[x] w2[y] w3[z] w1[y] w2[z] w3[x] c1 c2 c3'; HF = 'w1[X] w2[Y] w2[Z] c2 w1[Y] c1 w3[Z] w3[X] c3'; Dados = @('X = 2', 'Y = 2', 'Z = 2'); Deadlocks = 1; Trechos = @('Vítima: T3') },
        @{ Nome = 'Parênteses e valor negativo'; HI = 'w1(x, -5) r1(x) c1'; HF = 'w1[X,-5] r1[X] c1'; Dados = @('X = -5'); Deadlocks = 0; Trechos = @('lê X = -5') }
    )
    foreach ($caso in $casos) {
        $saida = ($caso.HI | dotnet .\bin\Debug\net10.0\Desktop.dll) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "Erro ao executar: $($caso.Nome)" }
        $linhas = $saida -split "`n"
        $marcador = [Array]::IndexOf($linhas, '=== HISTÓRIA FINAL (HF) =====')
        if ($marcador -lt 0 -or $linhas[$marcador + 1] -ne $caso.HF) { throw "HF incorreta: $($caso.Nome)`n$saida" }
        foreach ($esperado in ($caso.Dados + $caso.Trechos + @("Deadlocks detectados: $($caso.Deadlocks)"))) {
            if (-not $saida.Contains($esperado)) { throw "Ausente '$esperado': $($caso.Nome)`n$saida" }
        }
        Write-Host "PASSOU: $($caso.Nome)"
    }
    Write-Host "Todos os $($casos.Count) cenários passaram."
} finally {
    Pop-Location
}
