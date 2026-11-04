@echo off
setlocal EnableExtensions

echo Outlook Mail Adviser - durduruluyor...
echo.

for /f "tokens=5" %%a in ('netstat -ano ^| findstr /R /C:":7047 .*LISTENING"') do (
    echo API port 7047 - PID %%a
    taskkill /PID %%a /F >nul 2>&1
)

for /f "tokens=5" %%a in ('netstat -ano ^| findstr /R /C:":3000 .*LISTENING"') do (
    echo Addin port 3000 - PID %%a
    taskkill /PID %%a /F >nul 2>&1
)

echo.
echo Tamamlandi.
timeout /t 3 /nobreak >nul

endlocal
exit /b 0
