@echo off
setlocal EnableExtensions
set "EXE=%LOCALAPPDATA%\BloomCobrosAgent\App\BloomCobrosAgent.exe"

if not exist "%EXE%" (
  echo Bloom Cobros Agent no esta instalado.
  echo Ejecuta primero INSTALAR.cmd
  echo.
  pause
  exit /b 1
)

start "" "%EXE%"
exit /b 0
