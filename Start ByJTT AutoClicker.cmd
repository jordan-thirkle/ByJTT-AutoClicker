@echo off
setlocal
set "ROOT=%~dp0"
set "APP=%ROOT%src\ByJTTAutoClicker\bin\Release\net8.0-windows\ByJTTAutoClicker.exe"
if exist "%APP%" (
  start "" "%APP%"
  exit /b 0
)
set "APPDBG=%ROOT%src\ByJTTAutoClicker\bin\Debug\net8.0-windows\ByJTTAutoClicker.exe"
if exist "%APPDBG%" (
  start "" "%APPDBG%"
  exit /b 0
)
echo ByJTT AutoClicker is not built yet.
echo.
echo Build it with:
echo   dotnet build src\ByJTTAutoClicker\ByJTTAutoClicker.csproj -c Release
echo.
echo Requires Windows 10/11 and the .NET 8 Desktop Runtime (or SDK to build).
pause
exit /b 1
