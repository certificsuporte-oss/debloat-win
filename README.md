# Debloat Win

Programa para Windows 10 e 11 que remove apps pré-instalados, reduz telemetria e anúncios e aplica ajustes de desempenho, com **backup e desfazer** de tudo o que dá para desfazer.

- Um único `.exe` de ~190 KB. Roda no .NET Framework 4.8, que já vem no Windows 10 e 11: não precisa instalar nada.
- Pede permissão de administrador ao abrir.
- Catálogo próprio de 119 ajustes em 8 categorias, escrito do zero (não usa código do WinScript nem de outro projeto).

## Baixar

Link direto, sempre a versão mais nova:
**https://github.com/certificsuporte-oss/debloat-win/raw/main/atualizacao/DebloatWin.exe**

## Atualização automática

Ao abrir, o programa consulta `atualizacao/versao.json` neste repositório. Se houver versão mais nova, ele baixa o `.exe`, confere o SHA-256 e a versão do arquivo, troca o executável e reabre já atualizado. Sem internet, ou se algo der errado, ele abre a versão atual normalmente e registra o motivo em `C:\ProgramData\DebloatWin\logs\atualizacao.log`.

- Nunca volta para uma versão anterior e recusa arquivo com SHA-256 ou versão diferentes do anunciado.
- A linha de comando (`/preset` etc.) não se atualiza, para o resultado ser previsível em scripts.
- Para desligar num PC: `HKCU\Software\DebloatWin`, DWORD `AtualizarAuto` = `0`.

### Publicar uma versão nova

1. Suba a versão em `src/Core/Model.cs` (`AppInfo.Version`), `res/app.rc`, `res/app.manifest` e `src/App/AssemblyInfo.cs`.
2. Rode `./publicar.sh "o que mudou"`: compila, testa, grava `atualizacao/DebloatWin.exe` e `atualizacao/versao.json` em dois commits.
3. `git push origin main`. Os programas nos clientes se atualizam na próxima vez que forem abertos.

## Como usar

1. Abra o `DebloatWin.exe` e aceite o pedido de administrador.
2. Clique em **Padrão** no menu da esquerda ou marque os ajustes um a um.
3. Clique em **Aplicar**. Por padrão, o programa cria um **ponto de restauração** antes.
4. Reinicie o computador no final.

### Tema claro e escuro

O programa segue o tema do Windows (claro ou escuro) e muda junto se você trocar o tema com ele aberto. O botão **Tema** no canto superior direito alterna entre **Sistema**, **Escuro** e **Claro**, e a escolha fica salva. No escuro, a barra de título, as barras de rolagem e os menus do Windows também ficam escuros.

Cada ajuste mostra o **risco** (Seguro, Médio, Alto), avisos, se tem desfazer e em qual Windows funciona. **Ver detalhes** mostra exatamente o que é alterado e o PowerShell equivalente.

### O que já está feito neste PC

Ao abrir, o programa verifica cada ajuste na máquina e mostra uma etiqueta: **✓ Já aplicado**, **Parcial 3/5** ou **Nada a fazer neste PC** (quando o que o ajuste mexe não existe ali, como o driver da NVIDIA). Vale também para o que foi feito por outra ferramenta (WinScript, WinUtil) ou à mão, porque ele olha o estado do PC, não o histórico.

- Registro e serviços são verificados na hora; apps, tarefas agendadas, recursos do Windows e plano de energia em segundo plano (as linhas mostram "Verificando…").
- **Esconder já aplicados** mostra só o que falta. **Marcar pendentes** e os presets marcam só o que ainda não foi feito.
- **Ver detalhes** mostra a situação de cada alteração do ajuste.
- Depois de aplicar ou desfazer, o programa verifica de novo.
- Limpezas e reparos (Ferramentas) não deixam rastro, então não têm etiqueta.

### Instalar programas

A categoria **Instalar programas** tem só o básico: **Google Chrome, WinRAR, VLC, AnyDesk e Adobe Acrobat Reader**, instalados em silêncio pelo **winget**, sempre na versão mais nova do fabricante. Depois deles vem **VLC para vídeos e Adobe para PDF**, que deixa os dois como aplicativos padrão.

- Os já instalados aparecem como **✓ Já instalado** (cartão verde).
- Se o winget ainda não estiver ativo (comum num Windows 10 recém-instalado), o programa registra o "Instalador de Aplicativo" antes. Se não houver, avisa para atualizá-lo pela Microsoft Store.
- **Desfazer** desinstala só o que o Debloat Win instalou; um programa que já estava no PC nunca é removido.
- A lista fica em `tools/apps.tsv` (grupo, nome, id do winget, descrição). Para incluir um programa: acrescente a linha e rode `python3 tools/gerar_apps.py`, que gera `src/Core/CatalogApps.cs`. Em `tools/apps-completo.tsv` estão 89 programas com ids já conferidos no repositório oficial do winget: é só copiar a linha desejada.

**Aplicativos padrão (limite do Windows).** Desde fevereiro de 2024 o Windows tem um driver (UCPD.sys) que impede qualquer programa de gravar a escolha do usuário para PDF e para o navegador. O Debloat Win faz o que é permitido:

- **Usuários novos do PC:** importa as associações pelo DISM (`/Import-DefaultAppAssociations`), mesclando com as do Windows. Quem entrar no PC pela primeira vez já abre vídeos no VLC e PDF no Adobe.
- **Usuário atual:** se ainda faltar, abre **Configurações > Aplicativos padrão** (no Windows 11, direto na página do VLC/Adobe) para você só confirmar. A situação do ajuste mostra se já está certo.
- **Desfazer** volta o arquivo de associações original do Windows (guardado em `C:\ProgramData\DebloatWin`).

### Drivers e atualizações

Última categoria da lista, e também a última a rodar, porque é a mais demorada:

1. **Instalar drivers pelo Windows Update**: drivers assinados pela Microsoft, pela API do próprio Windows (Windows Update Agent).
2. **Instalar drivers que ainda faltarem (SDIO)**: se algum dispositivo continuar sem driver (códigos 1, 18 ou 28 do Gerenciador de Dispositivos), instala o Snappy Driver Installer Origin pelo winget e roda o script dele com `select missing`. Instala só o que falta e não troca drivers que já funcionam. Se não faltar nada, não baixa nada. Os pacotes baixados são apagados no fim e o log lista o que continuou sem driver.
3. **Instalar atualizações do Windows**: segurança, cumulativas, .NET e Defender. Não instala prévias opcionais nem atualizações de versão.

Nenhum deles tem desfazer. O Windows Update não reinicia o PC sozinho: o log avisa quando precisar.

Enquanto rodam, a janela de progresso mostra a etapa e a porcentagem ao vivo (ex.: "Instalar atualizações do Windows — 45% · Baixando 2 de 5 (KB5034441)"). O log guarda só o resultado: para drivers, o driver antigo e o novo de cada peça.

### Preset Padrão

Um único preset, **Padrão**, com 94 dos 119 ajustes mais os 5 programas (Google Chrome, WinRAR, VLC, AnyDesk e Adobe Acrobat Reader), o VLC/Adobe como padrão e, por último, drivers e Windows Update, escolhidos pensando num Windows recém-instalado e limpo: remoção de apps pré-instalados (inclusive Xbox, OneDrive, Media Player, Email/Calendário e Alarmes), Copilot/Recall/IA, privacidade, telemetria, Windows Update sem reinício forçado e sem P2P, desempenho (Desempenho Máximo, sem hibernação, sem SysMain/indexação, efeitos visuais mínimos), ajustes de jogos e de interface, e limpeza de temporários, cache do Update e Lixeira.

O botão **Padrão** marca só o que ainda falta no PC (o que já está aplicado fica de fora). A lista fica em `Catalog.PadraoIds` (`src/Core/Catalog.cs`).

Ficam fora do Padrão, só manualmente: remover a Store, desativar o Windows Update, travar no Windows 10, bloquear câmera/microfone, localização, remover IE/Fax e Scanner, Relatório de Erros, drivers pelo Windows Update, SFC/DISM, resetar rede, entre outros.

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
DebloatWin.exe /preset padrao
DebloatWin.exe /preset padrao /ids ui.dark,ui.taskview
DebloatWin.exe /ids tel.diagtrack,priv.adid /semponto
DebloatWin.exe /preset padrao /exportar C:\temp\debloat.ps1
```

- `/preset padrao` — aplica o preset Padrão (só o que vale para este Windows)
- `/ids a,b,c` — ajustes específicos (o id aparece em **Ver detalhes**)
- `/semponto` — não cria ponto de restauração. **Sem essa opção, se o ponto de restauração falhar, nada é aplicado.**
- `/exportar arquivo.ps1` — só gera o script, não aplica
- `/status` — lista o que já está aplicado, parcial ou pendente neste PC

Códigos de saída: `0` ok · `1` alguma falha · `2` argumento inválido · `3` sem administrador.
Em um `.bat`, use `start /wait DebloatWin.exe /preset padrao` para esperar terminar e ler o `%ERRORLEVEL%`.

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
T("priv.exemplo", c, Risk.Baixo, "Título curto", "O que muda para o usuário.")
  .Dw(@"HKCU\Software\...\Chave", "NomeDoValor", 0)     // DWORD
  .Sz(@"HKLM\SOFTWARE\...\Chave", "Nome", "texto")       // texto
  .Svc("NomeDoServico", StartMode.Manual)                // serviço
  .Tasks(@"\Microsoft\Windows\Pasta\Tarefa")             // tarefa agendada
  .Appx("Microsoft.NomeDoApp")                           // remover app
  .Win11()                                               // só Windows 11
  .Warn("Aviso que aparece em destaque.");
```

Para colocar no preset Padrão, inclua o id em `Catalog.PadraoIds`.
Rode os testes (`./build.sh`): eles conferem ids únicos, a lista do Padrão, avisos em ajustes de risco alto, serviços protegidos, e que aplicar e desfazer devolvem o registro exatamente ao original.

## Situação dos testes

- 1.605 verificações automáticas passando (catálogo, aplicar/desfazer com registro simulado, serviços, leitura da saída do PowerShell, backup em JSON, exportação, atualização automática, detecção do que já está aplicado).
- 75 scripts PowerShell gerados (incluindo os de detecção e de instalação de programas) passam na checagem estrutural (chaves, parênteses, aspas).
- Tela testada no Mono com Windows 10 e 11 simulados.
- **Ainda não foi executado num Windows real.** Teste primeiro numa máquina virtual ou num PC de teste antes de usar em clientes.

O executável não tem assinatura digital, então o Windows SmartScreen pode avisar na primeira execução ("Mais informações" → "Executar assim mesmo").
