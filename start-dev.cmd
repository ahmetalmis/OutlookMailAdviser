@echo off
setlocal EnableExtensions
cd /d "%~dp0"

title Outlook Mail Adviser - Baslat

echo.
echo  Outlook Mail Adviser - gelistirme modu
echo  ======================================
echo.

if exist "%~dp0start-dev.local.cmd" goto :LoadConfig
echo  [UYARI] start-dev.local.cmd bulunamadi.
echo          Ornek dosyadan kopyalayin: start-dev.local.cmd.example
echo.
goto :AfterConfig

:LoadConfig
call "%~dp0start-dev.local.cmd"

:AfterConfig
if /I "%Ai__Provider%"=="" set "Ai__Provider=OpenAI"

where dotnet >nul 2>&1
if errorlevel 1 goto :NoDotnet

where npm.cmd >nul 2>&1
if errorlevel 1 goto :NoNpm

if /I not "%Ai__Provider%"=="OpenAI" goto :SkipOpenAiCheck
if not "%OPENAI_API_KEY%"=="" goto :SkipOpenAiCheck
echo  [HATA] OPENAI_API_KEY tanimli degil.
echo          start-dev.local.cmd icinde anahtarinizi ayarlayin.
pause
exit /b 1

:SkipOpenAiCheck
if /I not "%Ai__Provider%"=="Ollama" goto :SkipOllamaCheck
where ollama >nul 2>&1
if errorlevel 1 goto :NoOllama
if "%Ai__Ollama__Model%"=="" set "Ai__Ollama__Model=qwen3.5:2b"
goto :AfterProviderCheck

:NoOllama
echo  [HATA] Ollama bulunamadi. https://ollama.com adresinden kurun.
pause
exit /b 1

:SkipOllamaCheck
:AfterProviderCheck

if exist "addin\node_modules\" goto :SkipNpmCi
echo  [BILGI] npm paketleri kuruluyor - ilk sefer, biraz surebilir...
pushd addin
call npm.cmd ci
if errorlevel 1 goto :NpmCiFailed
popd
goto :AfterNpmCi

:NpmCiFailed
popd
pause
exit /b 1

:SkipNpmCi
:AfterNpmCi

if exist "addin\.certs\localhost.pfx" goto :SkipCerts
echo  [BILGI] HTTPS sertifikasi olusturuluyor...
pushd addin
call npm.cmd run certs
if errorlevel 1 goto :CertsFailed
popd
goto :AfterCerts

:CertsFailed
popd
echo  [HATA] Sertifika olusturulamadi.
echo          Once su komutu deneyin: dotnet dev-certs https --trust
pause
exit /b 1

:SkipCerts
:AfterCerts

netstat -ano | findstr /R /C:":7047 .*LISTENING" >nul 2>&1
if not errorlevel 1 goto :Port7047Busy

netstat -ano | findstr /R /C:":3000 .*LISTENING" >nul 2>&1
if not errorlevel 1 goto :Port3000Busy

echo  [BILGI] API baslatiliyor - Provider: %Ai__Provider%
start "Outlook Mail Adviser - API" cmd /k "%~dp0start-dev-api.cmd"

echo  [BILGI] Addin baslatiliyor...
timeout /t 4 /nobreak >nul
start "Outlook Mail Adviser - Addin" cmd /k "%~dp0start-dev-addin.cmd"

echo.
echo  Hazir.
echo    API   : https://localhost:7047
echo    Test  : https://localhost:7047/test
echo    Addin : https://localhost:3000
echo.
echo  Durdurmak icin acilan iki CMD penceresini kapatmaniz yeterli.
echo.
timeout /t 6 /nobreak >nul
goto :End

:NoDotnet
echo  [HATA] dotnet bulunamadi. .NET 8 SDK kurulu ve PATH'te olmali.
pause
exit /b 1

:NoNpm
echo  [HATA] npm bulunamadi. Node.js 22+ kurulu olmali.
pause
exit /b 1

:Port7047Busy
echo  [UYARI] Port 7047 zaten kullanimda. Eski API penceresini kapatip tekrar deneyin.
pause
exit /b 1

:Port3000Busy
echo  [UYARI] Port 3000 zaten kullanimda. Eski addin penceresini kapatip tekrar deneyin.
pause
exit /b 1

:End
endlocal
exit /b 0
