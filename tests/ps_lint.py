#!/usr/bin/env python3
"""Checagem estrutural de scripts PowerShell gerados pelo app.

Não substitui o parser do PowerShell, mas pega o tipo de erro mais comum em
scripts montados por concatenação: chaves/parênteses/colchetes desbalanceados,
aspas sem fechar e 'exit' que encerraria o script exportado inteiro.
"""
import pathlib
import re
import sys

PAIRS = {"(": ")", "{": "}", "[": "]"}


def lint(text: str) -> list[str]:
    errors = []
    stack = []  # (char, line)
    i, line, n = 0, 1, len(text)
    mode = ["code"]  # pilha de modos: code | dq (string com aspas duplas)

    while i < n:
        c = text[i]
        if c == "\n":
            line += 1
        m = mode[-1]
        if m == "code":
            if c == "#":
                while i < n and text[i] != "\n":
                    i += 1
                continue
            if c == "'":
                j = i + 1
                while True:
                    if j >= n:
                        errors.append(f"linha {line}: aspas simples sem fechar")
                        return errors
                    if text[j] == "'":
                        if j + 1 < n and text[j + 1] == "'":
                            j += 2
                            continue
                        break
                    if text[j] == "\n":
                        line += 1
                    j += 1
                i = j + 1
                continue
            if c == '"':
                mode.append("dq")
                stack.append(('"', line))
                i += 1
                continue
            if c in PAIRS:
                stack.append((c, line))
            elif c in PAIRS.values():
                if not stack or PAIRS.get(stack[-1][0]) != c:
                    errors.append(f"linha {line}: '{c}' sem abertura correspondente")
                    return errors
                opener = stack.pop()
                if opener[0] == "$(":
                    mode.pop()
            i += 1
            continue
        # dentro de string com aspas duplas
        if c == "`":
            i += 2
            continue
        if c == '"':
            if i + 1 < n and text[i + 1] == '"':
                i += 2
                continue
            mode.pop()
            if not stack or stack[-1][0] != '"':
                errors.append(f"linha {line}: aspas duplas fora de ordem")
                return errors
            stack.pop()
            i += 1
            continue
        if c == "$" and i + 1 < n and text[i + 1] == "(":
            # subexpressão $( ... ) dentro da string: volta ao modo código
            stack.append(("(", line))
            mode.append("code")
            # marca que este '(' fecha voltando para dq
            stack[-1] = ("$(", line)
            i += 2
            continue
        i += 1

    for ch, ln in stack:
        errors.append(f"linha {ln}: '{ch}' aberto e nunca fechado")
    return errors


PAIRS["$("] = ")"


def main() -> int:
    folder = pathlib.Path(sys.argv[1])
    files = sorted(folder.glob("*.ps1"))
    bad = 0
    for f in files:
        text = f.read_text(encoding="utf-8-sig")
        errs = lint(text)
        # 'exit' só é permitido no embrulho do app (catch final), nunca no corpo das ações
        if f.name.startswith("_exportado"):
            for k, ln in enumerate(text.splitlines(), 1):
                if re.match(r"^\s*exit\b", ln):
                    errs.append(f"linha {k}: 'exit' no script exportado encerraria tudo")
        else:
            body = text.split("} catch { Write-Output ('FAIL:'")[0]
            for k, ln in enumerate(body.splitlines(), 1):
                if re.search(r"(^|[;{]\s*)exit\b", ln):
                    errs.append(f"linha {k}: 'exit' dentro do corpo da ação")
        if errs:
            bad += 1
            print(f"ERRO {f.name}:")
            for e in errs:
                print("   ", e)
    print(f"{len(files)} scripts verificados, {bad} com problema")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
