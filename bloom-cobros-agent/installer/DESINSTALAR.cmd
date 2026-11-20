@echo off
setlocal EnableExtensions
title Desinstalar - Bloom Cobros Agent

set "INSTALL_ROOT=%LOCALAPPDATA%\BloomCobrosAgent"
set "DESKTOP_LINK=%USERPROFILE%\Desktop\Bloom Cobros Agent.lnk"
set "START_LINK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Bloom Cobros Agent.lnk"

echo Este proceso eliminara la aplicacion y sus datos locales.
choice /C SN /N /M "Deseas continuar? [S/N]: "
if errorlevel 2 exit /b 0

taskkill /IM BloomCobrosAgent.exe /F >nul 2>&1
if exist "%DESKTOP_LINK%" del /F /Q "%DESKTOP_LINK%"
if exist "%START_LINK%" del /F /Q "%START_LINK%"
if exist "%INSTALL_ROOT%" rmdir /S /Q "%INSTALL_ROOT%"

echo.
echo Bloom Cobros Agent fue desinstalado.
pause
