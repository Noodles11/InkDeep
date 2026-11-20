@echo off
rem Ink Deep: bring the C# build up to date, then start the game full screen at its title.
rem For Steam Link: add this file to Steam as a non-Steam game (see docs/STREAMING.md). Steam follows the game while
rem it runs, so the game is started here and waited for, not detached. Extra arguments go on to Godot.
setlocal
cd /d "%~dp0.."

dotnet build OctoShoots.csproj -nologo -v q
if errorlevel 1 (
  echo The build failed. See the errors above.
  pause
  exit /b 1
)

rem Godot's own executable, where winget installs it (its .NET assemblies lie beside it). Not winget's "godot" link on
rem the PATH: run through the link, Godot looks for them beside the link and stops ("Assemblies not found").
rem Set INKDEEP_GODOT to use another install.
set "GODOT=%INKDEEP_GODOT%"
if "%GODOT%"=="" set "GODOT=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
if not exist "%GODOT%" (
  echo Godot 4.7.2 .NET was not found at "%GODOT%". Set INKDEEP_GODOT to its executable.
  pause
  exit /b 1
)

"%GODOT%" --path "%CD%" --fullscreen %*
