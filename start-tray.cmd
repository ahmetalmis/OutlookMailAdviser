@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "LAUNCH_LOCK=%~dp0tray\.launch.lock"
set "TRAY_APP=%~dp0tray\tray_app.py"

where pyw >nul 2>&1
if errorlevel 1 (
    echo  [HATA] pyw.exe bulunamadi. Python kurulumunu kontrol edin.
    pause
    exit /b 1
)

if exist "%LAUNCH_LOCK%" (
    echo  Tepsi zaten acik veya baslatiliyor.
    echo  Sistem saati yanindaki gizli simgeler alanina bakin.
    timeout /t 3 /nobreak >nul
    exit /b 0
)

echo starting> "%LAUNCH_LOCK%"

for /f "delims=" %%P in ('where pyw 2^>nul') do (
    start "" "%%P" "%TRAY_APP%"
    goto :Started
)

echo  [HATA] pyw.exe yolu cozulemedi.
del "%LAUNCH_LOCK%" 2>nul
pause
exit /b 1

:Started
timeout /t 2 /nobreak >nul
del "%LAUNCH_LOCK%" 2>nul

echo.
echo  Tepsi baslatildi.
echo  Sistem saati yanindaki gizli simgeler alanina bakin.
echo.
echo  Sorun olursa: reset-tray.cmd
echo.
timeout /t 3 /nobreak >nul

endlocal
exit /b 0
