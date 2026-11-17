@echo off
rem Compila o Debloat Win no proprio Windows, com o compilador que ja vem no .NET Framework.
rem Nao precisa instalar Visual Studio nem SDK. Gera dist\DebloatWin.exe
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Compilador do .NET Framework 4 nao encontrado.
  exit /b 1
)
if not exist dist mkdir dist
"%CSC%" /nologo /codepage:65001 /langversion:5 /target:winexe /optimize+ /platform:anycpu ^
  /win32manifest:res\app.manifest /win32icon:res\app.ico /out:dist\DebloatWin.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Runtime.Serialization.dll /r:System.Xml.dll ^
  src\Core\*.cs src\App\*.cs
if errorlevel 1 (
  echo.
  echo Falha na compilacao.
  exit /b 1
)
echo.
echo OK: dist\DebloatWin.exe
