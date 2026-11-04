@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo.
echo  Outlook Mail Adviser - Tepsi Kurulumu
echo  =====================================
echo.

where py >nul 2>&1
if errorlevel 1 goto :NoPython

where pyw >nul 2>&1
if errorlevel 1 goto :NoPython

if not exist "start-dev.local.cmd" (
    echo  [UYARI] start-dev.local.cmd yok.
    echo          OpenAI anahtarinizi start-dev.local.cmd icine yazin.
    echo.
)

echo  [1/3] Python paketleri kuruluyor...
py -3 -m pip install --user -r tray\requirements.txt
if errorlevel 1 goto :PipFailed

echo  [2/3] Windows acilis kisayolu olusturuluyor...

set "ROOT=%~dp0"
set "TRAY_PY=%ROOT%tray\tray_app.py"
set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "STARTUP_LNK=%STARTUP%\Outlook Mail Adviser Tray.lnk"
set "SHORTCUT_VBS=%ROOT%tray\create-startup-shortcut.vbs"

for /f "delims=" %%P in ('where pyw 2^>nul') do set "PYW_PATH=%%P"

if not defined PYW_PATH goto :ShortcutFailed

> "%SHORTCUT_VBS%" echo Set ws = CreateObject("WScript.Shell")
>> "%SHORTCUT_VBS%" echo Set sc = ws.CreateShortcut("%STARTUP_LNK%")
>> "%SHORTCUT_VBS%" echo sc.TargetPath = "%PYW_PATH%"
>> "%SHORTCUT_VBS%" echo sc.Arguments = """%TRAY_PY%"" --autostart"
>> "%SHORTCUT_VBS%" echo sc.WorkingDirectory = "%ROOT%"
>> "%SHORTCUT_VBS%" echo sc.WindowStyle = 7
>> "%SHORTCUT_VBS%" echo sc.Description = "Outlook Mail Adviser tepsi kontrolu"
>> "%SHORTCUT_VBS%" echo sc.Save

wscript.exe //Nologo "%SHORTCUT_VBS%"
if errorlevel 1 goto :ShortcutFailed

if not exist "%STARTUP_LNK%" goto :ShortcutFailed

echo  [3/3] Tepsi uygulamasi simdi baslatiliyor...
call "%ROOT%start-tray.cmd"
goto :Done

:NoPython
echo  [HATA] Python / pyw bulunamadi. https://www.python.org/downloads/
pause
exit /b 1

:PipFailed
echo  [HATA] pip kurulumu basarisiz.
pause
exit /b 1

:ShortcutFailed
echo  [HATA] Baslangic kisayolu olusturulamadi.
pause
exit /b 1

:Done
echo.
echo  Kurulum tamam.
echo    - Simge: gizli simgeler alaninda
echo    - Sag tik: Baslat / Durdur / Tamamen kapat
echo    - Windows acilisinda otomatik acilir
echo.
echo  Elle baslatmak icin: start-tray.cmd
echo  Sifirlamak icin: reset-tray.cmd
echo.
pause

endlocal
exit /b 0
