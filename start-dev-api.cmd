@echo off
setlocal EnableExtensions
cd /d "%~dp0"

if exist "%~dp0start-dev.local.cmd" call "%~dp0start-dev.local.cmd"
if /I "%Ai__Provider%"=="" set "Ai__Provider=OpenAI"

echo Provider: %Ai__Provider%
if /I not "%Ai__Provider%"=="OpenAI" if not "%Ai__Ollama__Model%"=="" echo Model: %Ai__Ollama__Model%
if /I "%Ai__Provider%"=="OpenAI" if not "%Ai__OpenAI__Model%"=="" echo Model: %Ai__OpenAI__Model%
echo.

dotnet run --project backend\src\OutlookMailAdviser.Api --launch-profile https

echo.
echo API durdu. Pencereyi kapatabilirsiniz.
pause

endlocal
