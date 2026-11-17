# Debloat Win

Programa para Windows 10 e 11 que remove apps pré-instalados, reduz telemetria e anúncios e aplica ajustes de desempenho, com **backup e desfazer** de tudo o que dá para desfazer.

- Um único `.exe` de ~190 KB. Roda no .NET Framework 4.8, que já vem no Windows 10 e 11: não precisa instalar nada.
- Pede permissão de administrador ao abrir.
- Catálogo próprio de 119 ajustes em 8 categorias, escrito do zero (não usa código do WinScript nem de outro projeto).

## Como usar

1. Abra o `DebloatWin.exe` e aceite o pedido de administrador.
2. Escolha um **preset** no menu da esquerda ou marque os ajustes um a um.
3. Clique em **Aplicar**. Por padrão, o programa cria um **ponto de restauração** antes.
4. Reinicie o computador no final.

### Tema claro e escuro

O programa segue o tema do Windows (claro ou escuro) e muda junto se você trocar o tema com ele aberto. O botão **Tema** no canto superior direito alterna entre **Sistema**, **Escuro** e **Claro**, e a escolha fica salva. No escuro, a barra de título, as barras de rolagem e os menus do Windows também ficam escuros.

Cada ajuste mostra o **risco** (Seguro, Médio, Alto), avisos, se tem desfazer e em qual Windows funciona. **Ver detalhes** mostra exatamente o que é alterado e o PowerShell equivalente.

### Presets

| Preset | O que entra |
|---|---|
| **Básico** | Só ajustes de risco baixo: anúncios, sugestões, telemetria, Recall, Widgets, Inicialização Rápida, serviços pouco usados, manutenção leve. |
| **Rígido** | Básico + remoção de apps pré-instalados, IA do Windows (Copilot, Paint, Bloco de Notas), mais privacidade e desempenho. |
| **Extremo** | Rígido + ajustes agressivos: Xbox, OneDrive, SysMain, indexação, efeitos visuais, localização, Relatório de Erros. Revise antes. |

Ajustes de **risco alto** que quebram coisas (remover a Store, desativar o Windows Update, bloquear câmera/microfone, resetar rede, remover IE) ficam **fora de todos os presets**: só entram se marcados à mão.

### O que o programa nunca mexe

Os testes garantem que nenhum ajuste altera serviços de **impressão** (Spooler), **cartão inteligente/token A3** (SCardSvr, CertPropSvc, ScDeviceEnum), **Área de Trabalho Remota**, rede básica, hora, firewall e Defender.

### Desfazer

Antes de mudar cada configuração, o valor anterior é guardado. Em **Histórico e desfazer** você escolhe uma sessão e volta tudo ao que era.

- Registro, serviços, tarefas agendadas, planos de energia e recursos do Windows: **voltam pelo programa**.
- Apps removidos e limpezas: **não têm desfazer** (aparecem com a etiqueta "Sem desfazer"). Para esses, use o ponto de restauração ou reinstale pela Store/winget.

Arquivos ficam em `C:\ProgramData\DebloatWin\`:
- `sessoes\` — backups para desfazer (JSON)
- `logs\` — log de cada aplicação, com o resultado de cada ajuste

### Exportar script

**Exportar .ps1** gera um script PowerShell com os ajustes marcados, para rodar em outro PC. O script mostra cada etapa e cada falha (não esconde erros), mas **não guarda backup**.

## Linha de comando (para o instalador de clientes)

```
DebloatWin.exe /preset basico
DebloatWin.exe /preset rigido /ids ui.dark,ui.seconds
DebloatWin.exe /ids tel.diagtrack,priv.adid /semponto
DebloatWin.exe /preset extremo /exportar C:\temp\debloat.ps1
```

- `/preset basico|rigido|extremo` — marca o preset (só o que vale para este Windows)
- `/ids a,b,c` — ajustes específicos (o id aparece em **Ver detalhes**)
- `/semponto` — não cria ponto de restauração. **Sem essa opção, se o ponto de restauração falhar, nada é aplicado.**
- `/exportar arquivo.ps1` — só gera o script, não aplica

Códigos de saída: `0` ok · `1` alguma falha · `2` argumento inválido · `3` sem administrador.
Em um `.bat`, use `start /wait DebloatWin.exe /preset basico` para esperar terminar e ler o `%ERRORLEVEL%`.

## Compilar

**No Windows** (sem instalar nada): rode `build.bat`. Ele usa o compilador do .NET Framework que já vem no Windows. O código é C# 5 justamente para isso.

**No Linux**: `./build.sh` (precisa de `mono-devel`, `binutils-mingw-w64-x86-64` e `python3`). Compila, roda os testes e checa os scripts. `./build.sh preview 26100` também gera imagens da tela simulando um Windows 11.

## Estrutura

```
src/Core/   motor: modelo, ações (registro, serviço, tarefa, app, recurso, comando), backup/desfazer, catálogo, exportação
src/App/    tela (WinForms) e linha de comando
tests/      testes com Windows simulado + checagem estrutural dos scripts PowerShell
res/        ícone, manifesto (administrador + DPI) e informações de versão
```

### Adicionar um ajuste

Em `src/Core/Catalog.cs`, copie um bloco parecido:

```csharp
T("priv.exemplo", c, 2, Risk.Baixo, "Título curto", "O que muda para o usuário.")
  .Dw(@"HKCU\Software\...\Chave", "NomeDoValor", 0)     // DWORD
  .Sz(@"HKLM\SOFTWARE\...\Chave", "Nome", "texto")       // texto
  .Svc("NomeDoServico", StartMode.Manual)                // serviço
  .Tasks(@"\Microsoft\Windows\Pasta\Tarefa")             // tarefa agendada
  .Appx("Microsoft.NomeDoApp")                           // remover app
  .Win11()                                               // só Windows 11
  .Warn("Aviso que aparece em destaque.");
```

O 3º parâmetro é o preset: `0` só manual · `1` Básico em diante · `2` Rígido em diante · `3` só Extremo.
Rode os testes (`./build.sh`): eles conferem ids únicos, presets, avisos em ajustes de risco alto, serviços protegidos, e que aplicar e desfazer devolvem o registro exatamente ao original.

## Situação dos testes

- 1.515 verificações automáticas passando (catálogo, aplicar/desfazer com registro simulado, serviços, leitura da saída do PowerShell, backup em JSON, exportação).
- 59 scripts PowerShell gerados passam na checagem estrutural (chaves, parênteses, aspas).
- Tela testada no Mono com Windows 10 e 11 simulados.
- **Ainda não foi executado num Windows real.** Teste primeiro numa máquina virtual ou num PC de teste antes de usar em clientes.

O executável não tem assinatura digital, então o Windows SmartScreen pode avisar na primeira execução ("Mais informações" → "Executar assim mesmo").
