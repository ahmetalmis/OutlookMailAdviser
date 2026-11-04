@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo  Outlook Mail Adviser - tepsi sifirlama
echo  ======================================
echo.

echo  [1/3] Tepsi surecleri kapatiliyor...
taskkill /IM pyw.exe /F >nul 2>&1
taskkill /IM pythonw.exe /F >nul 2>&1
taskkill /IM wscript.exe /F >nul 2>&1
if exist "tray\.launch.lock" del /F /Q "tray\.launch.lock"

echo  [2/3] Eski kilit dosyasi temizleniyor...
if exist "tray\.tray.instance.lock" del /F /Q "tray\.tray.instance.lock"

where py >nul 2>&1
if not errorlevel 1 (
    py -3 "%~dp0tray\tray_app.py" --cleanup-stale >nul 2>&1
)

echo  [3/3] API ve addin portlari temizleniyor...
call "%~dp0stop-dev.cmd"

echo.
echo  Temizlik tamam.
echo  Simdi start-tray.cmd calistirabilirsiniz.
echo.
timeout /t 4 /nobreak >nul

endlocal
exit /b 0
