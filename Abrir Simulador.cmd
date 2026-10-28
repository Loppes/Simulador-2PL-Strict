@echo off
cd /d "%~dp0"
if not exist "%~dp0Simulador.Grafico\bin\Debug\net10.0-windows\Simulador2PL.exe" (
    dotnet build "%~dp0Simulador.Grafico\Simulador.Grafico.csproj"
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0Simulador.Grafico\bin\Debug\net10.0-windows\Simulador2PL.exe"
