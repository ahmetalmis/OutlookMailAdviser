@echo off
setlocal EnableExtensions

set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "STARTUP_LNK=%STARTUP%\Outlook Mail Adviser Tray.lnk"

echo Tepsi durduruluyor...
taskkill /IM pyw.exe /F >nul 2>&1
taskkill /IM pythonw.exe /F >nul 2>&1
taskkill /IM wscript.exe /F >nul 2>&1
if exist "%~dp0tray\.launch.lock" del /F /Q "%~dp0tray\.launch.lock"
if exist "%~dp0tray\.tray.instance.lock" del /F /Q "%~dp0tray\.tray.instance.lock"

call "%~dp0stop-dev.cmd"

if exist "%STARTUP_LNK%" (
    del /F /Q "%STARTUP_LNK%"
    echo Baslangic kisayolu silindi.
) else (
    echo Baslangic kisayolu bulunamadi.
)

echo Tamamlandi.
timeout /t 3 /nobreak >nul

endlocal
exit /b 0
