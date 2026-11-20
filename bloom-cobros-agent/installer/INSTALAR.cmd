@echo off
setlocal EnableExtensions
title Instalador - Bloom Cobros Agent
color 0A

set "SOURCE=%~dp0app"
set "INSTALL_DIR=%LOCALAPPDATA%\BloomCobrosAgent\App"
set "EXE=%INSTALL_DIR%\BloomCobrosAgent.exe"

echo.
echo ==============================================
echo       BLOOM COBROS AGENT - INSTALADOR
echo ==============================================
echo.

if not exist "%SOURCE%\BloomCobrosAgent.exe" (
  color 0C
  echo ERROR: No se encontro la aplicacion en:
  echo %SOURCE%
  echo.
  pause
  exit /b 1
)

echo Instalando en:
echo %INSTALL_DIR%
echo.

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
robocopy "%SOURCE%" "%INSTALL_DIR%" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  color 0C
  echo ERROR: No se pudieron copiar los archivos.
  pause
  exit /b 2
)

echo Creando accesos directos...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$desktop = [Environment]::GetFolderPath('Desktop');" ^
  "$start = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs';" ^
  "foreach ($path in @((Join-Path $desktop 'Bloom Cobros Agent.lnk'), (Join-Path $start 'Bloom Cobros Agent.lnk'))) {" ^
  "  $shortcut = $ws.CreateShortcut($path);" ^
  "  $shortcut.TargetPath = '%EXE%';" ^
  "  $shortcut.WorkingDirectory = '%INSTALL_DIR%';" ^
  "  $shortcut.Description = 'Bloom Cobros Agent';" ^
  "  $shortcut.Save();" ^
  "}"

if errorlevel 1 (
  color 0E
  echo AVISO: La aplicacion se instalo, pero no se pudieron crear los accesos directos.
)

echo.
echo Instalacion completada correctamente.
echo.
echo Abriendo Bloom Cobros Agent...
start "" "%EXE%"
exit /b 0
