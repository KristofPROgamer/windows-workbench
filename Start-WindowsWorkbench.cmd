@echo off
setlocal
set "APP=%~dp0artifacts\WindowsWorkbench\WindowsWorkbench.exe"
if not exist "%APP%" (
  echo The app has not been built yet. See README.md for the build command.
  pause
  exit /b 1
)
start "Windows Workbench" "%APP%"
