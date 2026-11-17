#!/usr/bin/env bash
# Publica uma versão nova no canal de atualização automática (pasta atualizacao/).
#
#   ./publicar.sh "O que mudou nesta versão"
#
# Antes: suba a versão em src/Core/Model.cs (AppInfo.Version), res/app.rc,
# res/app.manifest e src/App/AssemblyInfo.cs.
#
# Faz dois commits:
#   1. atualizacao/DebloatWin.exe (o programa compilado)
#   2. atualizacao/versao.json apontando para o .exe pelo endereço do commit 1
#      (endereço fixo: o cache do GitHub nunca entrega um .exe antigo no lugar do novo)
# (Opcional: RODAPE="..." acrescenta um rodapé às mensagens de commit.)
# Depois é só dar git push. Os programas abertos nos clientes se atualizam na próxima abertura.
set -euo pipefail
cd "$(dirname "$0")"

NOTAS="${1:-}"
VERSAO=$(grep -oP 'Version = "\K[0-9.]+' src/Core/Model.cs)
REPO="certificsuporte-oss/debloat-win"

if [ -f atualizacao/versao.json ] && grep -q "\"versao\": *\"$VERSAO\"" atualizacao/versao.json; then
  echo "A versão $VERSAO já está publicada. Suba a versão antes."; exit 1
fi
grep -q "FILEVERSION ${VERSAO//./,},0" res/app.rc || { echo "res/app.rc não está na versão $VERSAO"; exit 1; }
grep -q "AssemblyVersion(\"$VERSAO.0\")" src/App/AssemblyInfo.cs || { echo "AssemblyInfo.cs não está na versão $VERSAO"; exit 1; }

./build.sh
mkdir -p atualizacao
cp dist/DebloatWin.exe atualizacao/DebloatWin.exe
SHA=$(sha256sum atualizacao/DebloatWin.exe | cut -d' ' -f1)

git add -A
git commit -q -m "Debloat Win $VERSAO: executável" -m "SHA-256: $SHA" ${RODAPE:+-m "$RODAPE"}
COMMIT=$(git rev-parse HEAD)

python3 - "$VERSAO" "$SHA" "$COMMIT" "$REPO" "$NOTAS" <<'EOF'
import json, sys
versao, sha, commit, repo, notas = sys.argv[1:6]
data = {
    "versao": versao,
    "url": f"https://raw.githubusercontent.com/{repo}/{commit}/atualizacao/DebloatWin.exe",
    "sha256": sha,
    "notas": notas,
}
with open("atualizacao/versao.json", "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
EOF
git add atualizacao/versao.json
git commit -q -m "Debloat Win $VERSAO: publicar no canal de atualização" ${RODAPE:+-m "$RODAPE"}
echo "Pronto: versão $VERSAO (SHA-256 $SHA). Agora: git push origin main"
