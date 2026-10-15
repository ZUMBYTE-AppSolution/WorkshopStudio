@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 10 SDK fehlt. Visual Studio 2026 mit .NET-Desktopentwicklung installieren.
  pause
  exit /b 1
)
dotnet run --project src\WorkshopStudio.Desktop\WorkshopStudio.Desktop.csproj -c Debug
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" pause
exit /b %RESULT%
