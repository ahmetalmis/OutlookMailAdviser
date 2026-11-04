@echo off
setlocal EnableExtensions
cd /d "%~dp0addin"

echo Addin: https://localhost:3000
echo.

call npm.cmd run dev

echo.
echo Addin durdu. Pencereyi kapatabilirsiniz.
pause

endlocal
