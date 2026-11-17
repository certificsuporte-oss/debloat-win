#!/usr/bin/env bash
# Compila no Linux com o Mono (mcs) contra as bibliotecas do .NET Framework 4.8.
# O .exe gerado roda no Windows 10/11 sem instalar nada (o .NET 4.8 já vem no Windows).
set -euo pipefail
cd "$(dirname "$0")"
A=/usr/lib/mono/4.8-api
REFS="-r:$A/mscorlib.dll -r:$A/System.dll -r:$A/System.Core.dll -r:$A/System.Drawing.dll -r:$A/System.Windows.Forms.dll -r:$A/System.Runtime.Serialization.dll -r:$A/System.Xml.dll"
OPTS="-langversion:5 -codepage:utf8 -nostdlib -noconfig -optimize+ -warn:4 -platform:anycpu"
mkdir -p dist build
x86_64-w64-mingw32-windres --preprocessor=cpp -O res -i res/app.rc -o build/app.res --include-dir=res
mcs $OPTS $REFS -target:winexe -win32res:build/app.res -out:dist/DebloatWin.exe src/Core/*.cs src/App/*.cs
mcs $OPTS $REFS -target:exe -out:build/Tests.exe src/Core/*.cs tests/FakeSystem.cs tests/Tests.cs
echo "OK: dist/DebloatWin.exe"

# Testes (registro e PowerShell simulados) e checagem estrutural dos scripts gerados
rm -rf build/ps-check
mono build/Tests.exe build/ps-check
python3 tests/ps_lint.py build/ps-check

# Prévia da tela (opcional): ./build.sh preview  → gera PNGs em build/preview usando tela virtual
if [ "${1:-}" = "preview" ]; then
  mcs $OPTS $REFS -target:winexe -out:build/Preview.exe -main:PreviewProgram src/Core/*.cs src/App/*.cs tests/FakeSystem.cs tests/preview/Preview.cs
  mkdir -p build/preview
  xvfb-run -a -s "-screen 0 1280x860x24" mono build/Preview.exe build/preview "${2:-19045}"
fi
