#!/usr/bin/env python3
"""Gera src/Core/CatalogApps.cs a partir de tools/apps.tsv.

Formato do apps.tsv (separado por TAB):
  grupo  nome  id_do_winget  descrição  [nome_appx_para_apps_da_store]

Para incluir um programa: acrescente uma linha (confira o id em github.com/microsoft/winget-pkgs)
e rode:  python3 tools/gerar_apps.py
"""
import pathlib
import re
import unicodedata

ROOT = pathlib.Path(__file__).resolve().parent.parent


def slug(name: str) -> str:
    s = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode().lower()
    s = s.replace("+", "p")
    return "app." + re.sub(r"[^a-z0-9]", "", s)


def cs(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


rows = []
for line in (ROOT / "tools" / "apps.tsv").read_text(encoding="utf-8").splitlines():
    if not line.strip() or line.startswith("#"):
        continue
    parts = line.split("\t")
    group, name, pid, desc = parts[:4]
    appx = parts[4] if len(parts) > 4 and parts[4].strip() else None
    rows.append((group, name, pid, desc, appx))

ids = [slug(r[1]) for r in rows]
dups = {i for i in ids if ids.count(i) > 1}
if dups:
    raise SystemExit(f"ids repetidos: {dups}")

out = [
    "// GERADO por tools/gerar_apps.py a partir de tools/apps.tsv. Não edite à mão.",
    "namespace DebloatWin.Core",
    "{",
    "    public static partial class Catalog",
    "    {",
    "        static void Programas()",
    "        {",
    "            Category c = Category.Programas;",
]
for (group, name, pid, desc, appx), tid in zip(rows, ids):
    out.append(
        f"            T({cs(tid)}, c, Risk.Baixo, {cs(name)}, {cs(desc)})"
        f".Winget({cs(pid)}, {cs(appx) if appx else 'null'}).InGroup({cs(group)});"
    )
out += ["        }", "    }", "}", ""]
(ROOT / "src" / "Core" / "CatalogApps.cs").write_text("\n".join(out), encoding="utf-8")
print(f"{len(rows)} programas gerados em src/Core/CatalogApps.cs")
for (group, name, pid, desc, appx), tid in zip(rows, ids):
    if name in ("Google Chrome", "WinRAR", "VLC", "AnyDesk") or "NET Desktop" in name:
        print("  ", tid, "=", name)
