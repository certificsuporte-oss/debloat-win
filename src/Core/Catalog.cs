// Catálogo de ajustes do Debloat Win.
// Para adicionar um ajuste: copie um bloco parecido e dê um Id único.
// Para colocar no preset Padrão, inclua o Id em PadraoIds.
using System.Collections.Generic;

namespace DebloatWin.Core
{
    static class TweakExt
    {
        public static Tweak Dw(this Tweak t, string key, string name, uint value)
        {
            t.Actions.Add(new RegSetAction(key, name, RegKind.DWord, value.ToString())); return t;
        }
        public static Tweak DwIf(this Tweak t, string requireKey, string key, string name, uint value)
        {
            RegSetAction a = new RegSetAction(key, name, RegKind.DWord, value.ToString());
            a.RequireKey = requireKey;
            t.Actions.Add(a); return t;
        }
        public static Tweak Sz(this Tweak t, string key, string name, string value)
        {
            t.Actions.Add(new RegSetAction(key, name, RegKind.String, value)); return t;
        }
        public static Tweak Bin(this Tweak t, string key, string name, string hex)
        {
            t.Actions.Add(new RegSetAction(key, name, RegKind.Binary, hex)); return t;
        }
        public static Tweak Svc(this Tweak t, string name, StartMode mode)
        {
            t.Actions.Add(new ServiceStartAction(name, mode, mode == StartMode.Desativado)); return t;
        }
        public static Tweak Tasks(this Tweak t, params string[] tasks)
        {
            t.Actions.Add(new TaskDisableAction(tasks)); return t;
        }
        public static Tweak Appx(this Tweak t, params string[] packages)
        {
            t.Actions.Add(new AppxRemoveAction(packages)); return t;
        }
        public static Tweak Feature(this Tweak t, string feature)
        {
            t.Actions.Add(new FeatureDisableAction(feature)); return t;
        }
        public static Tweak Capability(this Tweak t, string pattern)
        {
            t.Actions.Add(new CapabilityRemoveAction(pattern)); return t;
        }
        public static Tweak Cmd(this Tweak t, string description, string apply)
        {
            t.Actions.Add(new CommandAction(description, apply)); return t;
        }
        public static Tweak Cmd(this Tweak t, string description, string apply, string undo, string undoDescription)
        {
            t.Actions.Add(new CommandAction(description, apply, undo, undoDescription)); return t;
        }
        public static Tweak Cmd(this Tweak t, string description, string apply, int timeoutSeconds)
        {
            CommandAction a = new CommandAction(description, apply);
            a.TimeoutSeconds = timeoutSeconds;
            t.Actions.Add(a); return t;
        }
        /// <summary>Verificação ('1'/'0'/'NA') para o último comando adicionado.</summary>
        public static Tweak Detect(this Tweak t, string ps)
        {
            CommandAction c = t.Actions[t.Actions.Count - 1] as CommandAction;
            if (c == null) throw new System.InvalidOperationException("Detect só vale depois de Cmd: " + t.Id);
            c.DetectPs = ps;
            return t;
        }
        public static Tweak Winget(this Tweak t, string packageId, string appxName)
        {
            t.Actions.Add(new WingetInstallAction(packageId, t.Title, appxName)); return t;
        }
        public static Tweak InGroup(this Tweak t, string group) { t.Group = group; return t; }
        public static Tweak Win10(this Tweak t) { t.Windows = WinVersion.Win10; return t; }
        public static Tweak Win11(this Tweak t) { t.Windows = WinVersion.Win11; return t; }
        public static Tweak Restart(this Tweak t) { t.NeedsRestart = true; return t; }
        public static Tweak Pro(this Tweak t) { t.ProOnly = true; return t; }
        public static Tweak Warn(this Tweak t, string warning) { t.Warning = warning; return t; }
    }

    public static partial class Catalog
    {
        // Chaves usadas com frequência
        const string Adv = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string Cdm = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string Consent = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";
        const string PolWin = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\";
        const string PolEdge = @"HKLM\SOFTWARE\Policies\Microsoft\Edge";
        const string PolWU = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        const string GameCfg = @"HKCU\System\GameConfigStore";

        // Plano "Desempenho Máximo" duplicado com GUID fixo, para não criar cópias a cada uso
        const string UltimateGuid = "6a3c1e2b-9d4f-4b7e-8c21-5f0e7d9a4b13";

        static List<Tweak> list;

        /// <summary>
        /// Preset Padrão (definido com o usuário em 17/11/2026, pensando num Windows recém-instalado).
        /// Para mudar: tire ou coloque o id aqui. Ajustes de um Windows só (10 ou 11) são pulados no outro.
        /// </summary>
        public static readonly string[] PadraoIds = {
            "apps.consumer", "apps.terceiros", "apps.bing", "apps.dicas", "apps.feedbackhub", "apps.officehub",
            "apps.solitaire", "apps.3d", "apps.mapas", "apps.skype", "apps.filmes", "apps.clipchamp",
            "apps.teamschat", "apps.powerautomate", "apps.cortanaapp", "apps.devhome", "apps.family", "apps.outlooknew",
            "apps.onenote", "apps.todo", "apps.mail", "apps.alarmes", "apps.phonelink", "apps.musica", "apps.xbox", "apps.onedrive",
            "apps.copilot", "apps.recall", "apps.iaapps", "apps.widgets", "apps.noticias", "apps.cortana",
            "apps.wmp", "apps.xps", "apps.workfolders", "apps.stepsrec", "apps.math", "priv.adid",
            "priv.sugestoes", "priv.explorerads", "priv.tailored", "priv.activity", "priv.searchhighlights", "priv.websearch",
            "priv.edge", "priv.inking", "priv.speech", "priv.clipboard", "priv.perms", "tel.nivel",
            "tel.diagtrack", "tel.tasks", "tel.feedback", "tel.ceip", "tel.handwriting", "tel.office",
            "tel.nvidia", "tel.devtools", "upd.noreboot", "upd.p2p", "perf.servicos", "perf.faststartup",
            "perf.startupdelay", "perf.menudelay", "perf.transparency", "perf.ultimate", "perf.hibernate", "perf.visual",
            "perf.bgapps", "perf.sysmain", "perf.search", "game.mode", "game.dvr", "game.mouseacc",
            "game.priority", "game.network", "ui.ext", "ui.numlock", "ui.chat", "ui.meetnow",
            "ui.people", "ui.endtask", "ui.hidden", "ui.thispc", "ui.classicmenu", "ui.searchicon",
            "ui.stickykeys", "ui.bsod", "ui.verbose", "ui.taskbarleft", "ui.seconds", "tool.temp",
            "tool.wucache", "tool.recycle",
            // programas (Instalar programas)
            "app.googlechrome", "app.winrar", "app.vlc", "app.anydesk", "app.adobeacrobatreader", "app.padroes",
            // drivers e atualizações (por último)
            "fim.driverswu", "fim.driverssdio", "fim.windowsupdate"
        };

        static Tweak T(string id, Category cat, Risk risk, string title, string description)
        {
            Tweak t = new Tweak();
            t.Id = id; t.Category = cat; t.Risk = risk;
            t.Title = title; t.Description = description;
            list.Add(t);
            return t;
        }

        public static List<Tweak> All()
        {
            list = new List<Tweak>();
            Apps();
            Privacidade();
            Telemetria();
            Atualizacoes();
            Desempenho();
            Jogos();
            Interface();
            Ferramentas();
            Programas();   // CatalogApps.cs (gerado de tools/apps.tsv)
            AppsPadrao();  // CatalogDefaults.cs (depois dos programas: precisa deles instalados)
            Finalizar();   // CatalogFinal.cs (drivers e Windows Update, sempre por último)
            List<Tweak> r = list;
            list = null;
            HashSet<string> padrao = new HashSet<string>(PadraoIds);
            foreach (Tweak t in r) t.Padrao = padrao.Contains(t.Id);
            return r;
        }

        // ==================================================================
        static void Apps()
        {
            Category c = Category.Apps;

            T("apps.consumer", c, Risk.Baixo, "Bloquear instalação automática de apps sugeridos",
              "Impede o Windows de baixar sozinho jogos e apps patrocinados (Candy Crush etc.) para o usuário.")
              .Dw(PolWin + "CloudContent", "DisableWindowsConsumerFeatures", 1)
              .Dw(Cdm, "SilentInstalledAppsEnabled", 0)
              .Dw(Cdm, "OemPreInstalledAppsEnabled", 0)
              .Dw(Cdm, "PreInstalledAppsEnabled", 0)
              .Dw(Cdm, "PreInstalledAppsEverEnabled", 0);

            T("apps.terceiros", c, Risk.Baixo, "Remover apps patrocinados de terceiros",
              "Candy Crush, TikTok, Disney+, Netflix, Spotify, Facebook, Instagram, LinkedIn, Prime Video e Adobe Express que vêm pré-instalados.")
              .Warn("Também remove esses apps se o usuário os instalou pela Microsoft Store.")
              .Appx("king.com.*", "BytedancePte.Ltd.TikTok", "Disney.*", "4DF9E0F8.Netflix", "SpotifyAB.SpotifyMusic",
                    "Facebook.*", "7EE7776C.LinkedInforWindows", "AmazonVideo.PrimeVideo",
                    "AdobeSystemsIncorporated.AdobePhotoshopExpress", "9E2F88E3.Twitter");

            T("apps.bing", c, Risk.Baixo, "Remover Notícias, Clima e apps do Bing",
              "Apps Microsoft Notícias, Clima, Finanças e Esportes.")
              .Appx("Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.BingFinance", "Microsoft.BingSports");

            T("apps.dicas", c, Risk.Baixo, "Remover Dicas, Introdução e Obter Ajuda",
              "Apps de boas-vindas e suporte da Microsoft.")
              .Appx("Microsoft.Getstarted", "Microsoft.GetHelp");

            T("apps.feedbackhub", c, Risk.Baixo, "Remover Hub de Comentários", "App para enviar comentários à Microsoft.")
              .Appx("Microsoft.WindowsFeedbackHub");

            T("apps.officehub", c, Risk.Baixo, "Remover app Microsoft 365 (Office Hub)",
              "Atalho/propaganda do Microsoft 365. Não afeta o Office instalado.")
              .Appx("Microsoft.MicrosoftOfficeHub");

            T("apps.solitaire", c, Risk.Baixo, "Remover Microsoft Solitaire Collection", "Pacote de jogos de cartas com anúncios.")
              .Appx("Microsoft.MicrosoftSolitaireCollection");

            T("apps.3d", c, Risk.Baixo, "Remover Paint 3D, Visualizador 3D e Realidade Misturada",
              "Apps 3D do Windows 10. O Paint clássico continua.")
              .Appx("Microsoft.MSPaint", "Microsoft.Microsoft3DViewer", "Microsoft.Print3D", "Microsoft.MixedReality.Portal");

            T("apps.mapas", c, Risk.Baixo, "Remover Mapas", "App Mapas do Windows.")
              .Appx("Microsoft.WindowsMaps");

            T("apps.skype", c, Risk.Baixo, "Remover Skype (app da Store)", "Versão do Skype que vem com o Windows.")
              .Appx("Microsoft.SkypeApp");

            T("apps.filmes", c, Risk.Baixo, "Remover Filmes e TV", "App Filmes e TV.")
              .Appx("Microsoft.ZuneVideo");

            T("apps.clipchamp", c, Risk.Baixo, "Remover Clipchamp", "Editor de vídeo online da Microsoft.")
              .Appx("Clipchamp.Clipchamp");

            T("apps.teamschat", c, Risk.Baixo, "Remover Chat do Teams (pessoal)",
              "Chat do Teams que vem no Windows 11. Não remove o Teams corporativo.")
              .Appx("MicrosoftTeams").Win11();

            T("apps.powerautomate", c, Risk.Baixo, "Remover Power Automate", "Automação de tarefas da Microsoft.")
              .Appx("Microsoft.PowerAutomateDesktop");

            T("apps.cortanaapp", c, Risk.Baixo, "Remover app Cortana", "Assistente da Microsoft, descontinuada.")
              .Appx("Microsoft.549981C3F5F10");

            T("apps.devhome", c, Risk.Baixo, "Remover Dev Home", "Painel para desenvolvedores do Windows 11.")
              .Appx("Microsoft.Windows.DevHome").Win11();

            T("apps.family", c, Risk.Baixo, "Remover Microsoft Family", "Controle parental da conta Microsoft.")
              .Appx("MicrosoftCorporationII.MicrosoftFamily");

            T("apps.mail", c, Risk.Medio, "Remover Email, Calendário e Pessoas",
              "Apps de e-mail e calendário antigos do Windows.")
              .Warn("Verifique se o usuário não lê e-mails por eles.")
              .Appx("microsoft.windowscommunicationsapps", "Microsoft.People");

            T("apps.outlooknew", c, Risk.Medio, "Remover novo Outlook (app)", "Novo Outlook que substitui o app Email.")
              .Warn("Verifique se o usuário não usa o novo Outlook.")
              .Appx("Microsoft.OutlookForWindows");

            T("apps.onenote", c, Risk.Baixo, "Remover OneNote (app da Store)", "Versão antiga do OneNote para Windows 10.")
              .Appx("Microsoft.Office.OneNote");

            T("apps.musica", c, Risk.Medio, "Remover Groove Música / Media Player",
              "No Windows 10 é o Groove Música; no Windows 11 é o novo Media Player.")
              .Warn("No Windows 11 o PC fica sem o player padrão de música e vídeo.")
              .Appx("Microsoft.ZuneMusic");

            T("apps.phonelink", c, Risk.Baixo, "Remover Vincular ao Celular", "App que conecta o celular ao PC.")
              .Appx("Microsoft.YourPhone");

            T("apps.todo", c, Risk.Baixo, "Remover Microsoft To Do", "App de listas de tarefas.")
              .Appx("Microsoft.Todos");

            T("apps.alarmes", c, Risk.Baixo, "Remover Alarmes e Gravador de Som", "Apps Alarmes e Relógio e Gravador de Som.")
              .Appx("Microsoft.WindowsAlarms", "Microsoft.WindowsSoundRecorder");

            T("apps.xbox", c, Risk.Alto, "Remover apps do Xbox",
              "App Xbox, Game Bar e componentes de login do Xbox.")
              .Warn("Quebra o Game Pass, a Game Bar (Win+G) e o login Xbox em alguns jogos.")
              .Appx("Microsoft.XboxApp", "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay",
                    "Microsoft.XboxSpeechToTextOverlay", "Microsoft.Xbox.TCUI", "Microsoft.XboxIdentityProvider");

            T("apps.store", c, Risk.Alto, "Remover Microsoft Store",
              "Remove a loja de apps do Windows.")
              .Warn("Sem a Store, apps da loja deixam de atualizar e reinstalar é trabalhoso. Use só se tiver certeza.")
              .Appx("Microsoft.WindowsStore", "Microsoft.StorePurchaseApp");

            T("apps.onedrive", c, Risk.Medio, "Remover OneDrive",
              "Desinstala o OneDrive, impede a reinstalação e tira o ícone do Explorer.")
              .Warn("Arquivos que estão só na nuvem deixam de sincronizar. Confirme se o usuário não usa o OneDrive.")
              .Cmd("Desinstalar o OneDrive",
                   "function Get-DbwOneDriveExe { @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\", \"$env:ProgramFiles\\Microsoft OneDrive\\OneDrive.exe\", \"${env:ProgramFiles(x86)}\\Microsoft OneDrive\\OneDrive.exe\") | Where-Object { Test-Path -LiteralPath $_ } }\n" +
                   "if (@(Get-DbwOneDriveExe).Count -eq 0) { Write-Output 'SKIP:OneDrive não está instalado'; return }\n" +
                   "Get-Process OneDrive -ErrorAction SilentlyContinue | Stop-Process -Force\n" +
                   "$setups = @()\n" +
                   "$setups += Get-ChildItem \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\*\\OneDriveSetup.exe\",\"$env:ProgramFiles\\Microsoft OneDrive\\*\\OneDriveSetup.exe\" -ErrorAction SilentlyContinue\n" +
                   "$setups += Get-ChildItem \"$env:SystemRoot\\System32\\OneDriveSetup.exe\",\"$env:SystemRoot\\SysWOW64\\OneDriveSetup.exe\" -ErrorAction SilentlyContinue\n" +
                   "foreach ($s in $setups) {\n" +
                   "  if (@(Get-DbwOneDriveExe).Count -eq 0) { break }\n" +
                   "  # o desinstalador anterior pode ter apagado esta pasta\n" +
                   "  if (-not (Test-Path -LiteralPath $s.FullName)) { continue }\n" +
                   "  $arg = '/uninstall'\n" +
                   "  if ($s.FullName -like \"$env:ProgramFiles*\") { $arg = '/uninstall /allusers' }\n" +
                   "  Start-Process -FilePath $s.FullName -ArgumentList $arg -Wait\n" +
                   "  Start-Sleep -Seconds 2\n" +
                   "}\n" +
                   "if (@(Get-DbwOneDriveExe).Count -gt 0) { throw 'O OneDrive continua instalado (o desinstalador não removeu o OneDrive.exe)' }\n" +
                   "Write-Output 'OneDrive desinstalado'\n")
              .Detect("$p = @(\"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\", \"$env:ProgramFiles\\Microsoft OneDrive\\OneDrive.exe\", \"${env:ProgramFiles(x86)}\\Microsoft OneDrive\\OneDrive.exe\") | Where-Object { Test-Path $_ }\n" +
                      "if (@($p).Count -gt 0) { '0' } else { '1' }")
              .Dw(PolWin + "OneDrive", "DisableFileSyncNGSC", 1)
              .Dw(@"HKCU\Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0);

            T("apps.copilot", c, Risk.Baixo, "Desativar e remover o Copilot",
              "Remove o app Copilot, desliga o Copilot do Windows por política e tira o botão da barra.")
              .Appx("Microsoft.Copilot", "Microsoft.Windows.Ai.Copilot.Provider")
              .Dw(@"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
              .Dw(PolWin + "WindowsCopilot", "TurnOffWindowsCopilot", 1)
              .Dw(Adv, "ShowCopilotButton", 0);

            T("apps.recall", c, Risk.Baixo, "Desativar Recall (capturas de tela da IA)",
              "Impede o Windows de tirar e analisar capturas da tela (Copilot+ PCs).")
              .Dw(PolWin + "WindowsAI", "DisableAIDataAnalysis", 1)
              .Dw(PolWin + "WindowsAI", "AllowRecallEnablement", 0)
              .Dw(@"HKCU\Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1)
              .Win11();

            T("apps.iaapps", c, Risk.Baixo, "Desativar IA no Paint, Bloco de Notas e Clique para Fazer",
              "Desliga Cocreator, Preenchimento generativo e Criador de Imagens no Paint, a IA do Bloco de Notas e o Clique para Fazer.")
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1)
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1)
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1)
              .Dw(@"HKLM\SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1)
              .Dw(PolWin + "WindowsAI", "DisableClickToDo", 1)
              .Win11();

            T("apps.widgets", c, Risk.Baixo, "Desativar Widgets", "Remove o painel de Widgets (notícias e clima) da barra do Windows 11.")
              .Dw(@"HKLM\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)
              .Win11();

            T("apps.noticias", c, Risk.Baixo, "Desativar Notícias e Interesses",
              "Remove o clima e as notícias da barra de tarefas do Windows 10.")
              .Dw(PolWin + "Windows Feeds", "EnableFeeds", 0)
              .Win10();

            T("apps.cortana", c, Risk.Baixo, "Desativar a Cortana", "Desliga a Cortana por política no Windows 10.")
              .Dw(PolWin + "Windows Search", "AllowCortana", 0)
              .Win10();

            T("apps.ie", c, Risk.Alto, "Remover Internet Explorer 11",
              "Remove o IE11 do Windows 10.")
              .Warn("Alguns sistemas antigos de bancos, prefeituras e SEFAZ ainda dependem do IE ou do modo IE do Edge.")
              .Capability("Browser.InternetExplorer~~~~*")
              .Win10();

            T("apps.wmp", c, Risk.Medio, "Remover Windows Media Player (clássico)", "Remove o player antigo do Windows.")
              .Capability("Media.WindowsMediaPlayer~~~~*");

            T("apps.faxscan", c, Risk.Medio, "Remover Fax e Scanner do Windows",
              "Remove o app Fax e Scanner do Windows.")
              .Warn("Quem digitaliza documentos por ele perde o acesso. Os drivers do scanner continuam.")
              .Capability("Print.Fax.Scan~~~~*");

            T("apps.xps", c, Risk.Medio, "Desativar Serviços XPS", "Remove a impressora virtual Microsoft XPS Document Writer.")
              .Feature("Printing-XPSServices-Features").Restart();

            T("apps.workfolders", c, Risk.Baixo, "Desativar Pastas de Trabalho", "Cliente de Pastas de Trabalho corporativas, raramente usado.")
              .Feature("WorkFolders-Client").Restart();

            T("apps.stepsrec", c, Risk.Baixo, "Remover Gravador de Passos", "Ferramenta antiga de gravação de passos.")
              .Capability("App.StepsRecorder~~~~*");

            T("apps.math", c, Risk.Baixo, "Remover Reconhecedor de Matemática", "Painel de entrada de fórmulas matemáticas.")
              .Capability("MathRecognizer~~~~*");
        }

        // ==================================================================
        static void Privacidade()
        {
            Category c = Category.Privacidade;

            T("priv.adid", c, Risk.Baixo, "Desativar ID de publicidade",
              "Impede que apps usem um identificador para anúncios personalizados.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)
              .Dw(PolWin + "AdvertisingInfo", "DisabledByGroupPolicy", 1);

            T("priv.sugestoes", c, Risk.Baixo, "Remover sugestões e anúncios do Windows",
              "Tira sugestões do Iniciar e das Configurações, dicas, \"Vamos concluir a configuração\" e curiosidades da tela de bloqueio.")
              .Dw(Cdm, "SystemPaneSuggestionsEnabled", 0)
              .Dw(Cdm, "SubscribedContent-338388Enabled", 0)
              .Dw(Cdm, "SubscribedContent-338389Enabled", 0)
              .Dw(Cdm, "SubscribedContent-338393Enabled", 0)
              .Dw(Cdm, "SubscribedContent-353694Enabled", 0)
              .Dw(Cdm, "SubscribedContent-353696Enabled", 0)
              .Dw(Cdm, "SubscribedContent-310093Enabled", 0)
              .Dw(Cdm, "SubscribedContent-338387Enabled", 0)
              .Dw(Cdm, "RotatingLockScreenOverlayEnabled", 0)
              .Dw(Cdm, "SoftLandingEnabled", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0)
              .Dw(Adv, "Start_IrisRecommendations", 0);

            T("priv.explorerads", c, Risk.Baixo, "Remover propaganda no Explorer",
              "Desliga as notificações de provedores de sincronização (propaganda do OneDrive/Microsoft 365) no Explorer.")
              .Dw(Adv, "ShowSyncProviderNotifications", 0);

            T("priv.tailored", c, Risk.Baixo, "Desativar experiências personalizadas",
              "A Microsoft deixa de usar os dados de diagnóstico para dicas e anúncios personalizados.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)
              .Dw(@"HKCU\Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1);

            T("priv.activity", c, Risk.Baixo, "Desativar histórico de atividades",
              "Não registra nem envia o histórico de apps e arquivos abertos (Linha do Tempo).")
              .Dw(PolWin + "System", "EnableActivityFeed", 0)
              .Dw(PolWin + "System", "PublishUserActivities", 0)
              .Dw(PolWin + "System", "UploadUserActivities", 0);

            T("priv.searchhighlights", c, Risk.Baixo, "Desativar destaques da pesquisa",
              "Tira as ilustrações e conteúdos do dia da caixa de pesquisa.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0)
              .Dw(PolWin + "Windows Search", "EnableDynamicContentInWSB", 0);

            T("priv.websearch", c, Risk.Baixo, "Desativar pesquisa na web (Bing) no Iniciar",
              "A pesquisa do Iniciar passa a mostrar só apps, arquivos e configurações do PC.")
              .Dw(@"HKCU\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0);

            T("priv.edge", c, Risk.Baixo, "Edge sem anúncios, sugestões e coleta",
              "Desliga primeira execução, recomendações, Rewards, assistente de compras, barra lateral, Startup Boost e envio de dados do Edge.")
              .Warn("O Edge passa a exibir \"Seu navegador é gerenciado pela sua organização\". É normal com políticas.")
              .Dw(PolEdge, "HideFirstRunExperience", 1)
              .Dw(PolEdge, "ShowRecommendationsEnabled", 0)
              .Dw(PolEdge, "PersonalizationReportingEnabled", 0)
              .Dw(PolEdge, "EdgeShoppingAssistantEnabled", 0)
              .Dw(PolEdge, "ShowMicrosoftRewards", 0)
              .Dw(PolEdge, "SpotlightExperiencesAndRecommendationsEnabled", 0)
              .Dw(PolEdge, "PromotionalTabsEnabled", 0)
              .Dw(PolEdge, "HubsSidebarEnabled", 0)
              .Dw(PolEdge, "StartupBoostEnabled", 0)
              .Dw(PolEdge, "BackgroundModeEnabled", 0)
              .Dw(PolEdge, "DiagnosticData", 0)
              .Dw(PolEdge, "NewTabPageContentEnabled", 0);

            T("priv.inking", c, Risk.Baixo, "Desativar personalização de escrita e digitação",
              "Não coleta o que é digitado ou escrito à mão para personalizar o dicionário.")
              .Dw(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1)
              .Dw(@"HKCU\Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1)
              .Dw(@"HKCU\Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0)
              .Dw(@"HKCU\Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0);

            T("priv.speech", c, Risk.Baixo, "Desativar reconhecimento de fala online",
              "A voz não é enviada à Microsoft. Ditado online deixa de funcionar.")
              .Dw(@"HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0);

            T("priv.clipboard", c, Risk.Baixo, "Desativar área de transferência na nuvem",
              "O que é copiado não é sincronizado com outros dispositivos.")
              .Dw(PolWin + "System", "AllowCrossDeviceClipboard", 0);

            T("priv.perms", c, Risk.Baixo, "Negar a apps acesso a contatos, agenda, e-mail e mensagens",
              "Bloqueia o acesso de apps da Store a contatos, calendário, histórico de chamadas, e-mail, mensagens, tarefas, informações da conta e diagnóstico de outros apps.")
              .Sz(Consent + "contacts", "Value", "Deny")
              .Sz(Consent + "appointments", "Value", "Deny")
              .Sz(Consent + "phoneCallHistory", "Value", "Deny")
              .Sz(Consent + "email", "Value", "Deny")
              .Sz(Consent + "chat", "Value", "Deny")
              .Sz(Consent + "userDataTasks", "Value", "Deny")
              .Sz(Consent + "userAccountInformation", "Value", "Deny")
              .Sz(Consent + "appDiagnostics", "Value", "Deny");

            T("priv.location", c, Risk.Medio, "Desativar localização",
              "Desliga o serviço de localização para todo o PC.")
              .Warn("O fuso horário automático, Mapas, Clima e \"Encontrar meu dispositivo\" deixam de funcionar.")
              .Sz(Consent + "location", "Value", "Deny");

            T("priv.camera", c, Risk.Alto, "Bloquear câmera para apps", "Nenhum app consegue usar a webcam.")
              .Warn("Quebra videochamadas (Teams, Zoom, WhatsApp, Meet no navegador).")
              .Sz(Consent + "webcam", "Value", "Deny");

            T("priv.mic", c, Risk.Alto, "Bloquear microfone para apps", "Nenhum app consegue usar o microfone.")
              .Warn("Quebra chamadas de voz e videochamadas.")
              .Sz(Consent + "microphone", "Value", "Deny");
        }

        // ==================================================================
        static void Telemetria()
        {
            Category c = Category.Telemetria;

            T("tel.nivel", c, Risk.Baixo, "Telemetria no mínimo permitido",
              "Reduz os dados de diagnóstico enviados à Microsoft. No Home e no Pro o mínimo é o nível \"Obrigatório\".")
              .Dw(PolWin + "DataCollection", "AllowTelemetry", 0)
              .Dw(PolWin + "DataCollection", "LimitDiagnosticLogCollection", 1)
              .Dw(PolWin + "DataCollection", "LimitDumpCollection", 1);

            T("tel.diagtrack", c, Risk.Baixo, "Desativar serviço de telemetria (DiagTrack)",
              "Para e desativa o serviço \"Experiências do Usuário Conectado e Telemetria\".")
              .Svc("DiagTrack", StartMode.Desativado);

            T("tel.tasks", c, Risk.Baixo, "Desativar tarefas agendadas de telemetria",
              "Avaliador de compatibilidade, Programa de Aperfeiçoamento, coleta de diagnóstico de disco e pesquisas de feedback.")
              .Tasks(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                     @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                     @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                     @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                     @"\Microsoft\Windows\Autochk\Proxy",
                     @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                     @"\Microsoft\Windows\Feedback\Siuf\DmClient",
                     @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload");

            T("tel.feedback", c, Risk.Baixo, "Não pedir feedback",
              "O Windows para de perguntar \"Como está sua experiência?\".")
              .Dw(@"HKCU\Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)
              .Dw(PolWin + "DataCollection", "DoNotShowFeedbackNotifications", 1);

            T("tel.ceip", c, Risk.Baixo, "Desativar Programa de Aperfeiçoamento e inventário",
              "Desliga o CEIP, a telemetria de compatibilidade de apps e o inventário de programas instalados.")
              .Dw(@"HKLM\SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0)
              .Dw(PolWin + "AppCompat", "AITEnable", 0)
              .Dw(PolWin + "AppCompat", "DisableInventory", 1);

            T("tel.handwriting", c, Risk.Baixo, "Não enviar dados de escrita à mão",
              "Bloqueia o envio de amostras e erros de reconhecimento de escrita.")
              .Dw(PolWin + "TabletPC", "PreventHandwritingDataSharing", 1)
              .Dw(PolWin + "HandwritingErrorReports", "PreventHandwritingErrorReports", 1);

            T("tel.office", c, Risk.Baixo, "Desativar telemetria do Office",
              "Desliga a telemetria do cliente e o Programa de Aperfeiçoamento do Office 2016 em diante. Só aplica se o Office estiver instalado.")
              .DwIf(@"HKCU\Software\Microsoft\Office\16.0", @"HKCU\Software\Policies\Microsoft\Office\16.0\Common\ClientTelemetry", "DisableTelemetry", 1)
              .DwIf(@"HKCU\Software\Microsoft\Office\16.0", @"HKCU\Software\Policies\Microsoft\Office\16.0\Common\ClientTelemetry", "SendTelemetry", 3)
              .DwIf(@"HKCU\Software\Microsoft\Office\16.0", @"HKCU\Software\Policies\Microsoft\Office\16.0\Common", "qmenable", 0);

            T("tel.nvidia", c, Risk.Baixo, "Desativar telemetria da NVIDIA",
              "Desliga a coleta de dados do driver NVIDIA. Só aplica se houver driver NVIDIA.")
              .DwIf(@"HKLM\SOFTWARE\NVIDIA Corporation", @"HKLM\SOFTWARE\NVIDIA Corporation\NvControlPanel2\Client", "OptInOrOutPreference", 0)
              .DwIf(@"HKLM\SOFTWARE\NVIDIA Corporation", @"HKLM\SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID44231", 0)
              .DwIf(@"HKLM\SOFTWARE\NVIDIA Corporation", @"HKLM\SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID64640", 0)
              .DwIf(@"HKLM\SOFTWARE\NVIDIA Corporation", @"HKLM\SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID66610", 0)
              .Svc("NvTelemetryContainer", StartMode.Desativado);

            T("tel.devtools", c, Risk.Baixo, "Desativar telemetria do PowerShell 7 e .NET",
              "Define as variáveis de ambiente de opt-out do PowerShell 7 e do .NET SDK.")
              .Sz(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "POWERSHELL_TELEMETRY_OPTOUT", "1")
              .Sz(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "DOTNET_CLI_TELEMETRY_OPTOUT", "1");

            T("tel.wer", c, Risk.Medio, "Desativar Relatório de Erros do Windows",
              "Os travamentos de programas deixam de ser enviados à Microsoft.")
              .Warn("Também deixa de ajudar no diagnóstico de travamentos e telas azuis.")
              .Dw(PolWin + "Windows Error Reporting", "Disabled", 1);
        }

        // ==================================================================
        static void Atualizacoes()
        {
            Category c = Category.Atualizacoes;

            T("upd.noreboot", c, Risk.Baixo, "Não reiniciar sozinho com alguém usando o PC",
              "O Windows Update espera o usuário reiniciar quando há alguém logado.")
              .Dw(PolWU + @"\AU", "NoAutoRebootWithLoggedOnUsers", 1);

            T("upd.p2p", c, Risk.Baixo, "Baixar atualizações só da Microsoft (sem P2P)",
              "Desliga a Otimização de Entrega entre PCs. O PC não usa a internet para enviar atualizações a outros.")
              .Dw(PolWin + "DeliveryOptimization", "DODownloadMode", 0);

            T("upd.defer", c, Risk.Baixo, "Adiar atualizações de recursos por 365 dias",
              "Atualizações de versão (ex.: 23H2 → 24H2) só chegam um ano depois. Atualizações de segurança continuam.")
              .Dw(PolWU, "DeferFeatureUpdates", 1)
              .Dw(PolWU, "DeferFeatureUpdatesPeriodInDays", 365)
              .Pro();

            T("upd.drivers", c, Risk.Medio, "Não instalar drivers pelo Windows Update",
              "O Windows Update deixa de trocar drivers sozinho (útil para placa de vídeo e impressoras específicas).")
              .Warn("Drivers novos passam a ser instalados manualmente.")
              .Dw(PolWU, "ExcludeWUDriversInQualityUpdate", 1);

            T("upd.storeauto", c, Risk.Medio, "Desativar atualização automática da Store",
              "Apps da Microsoft Store deixam de se atualizar sozinhos.")
              .Dw(@"HKLM\SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2);

            T("upd.lockwin10", c, Risk.Medio, "Travar no Windows 10 22H2 (não oferecer o Windows 11)",
              "Impede que o Windows Update ofereça o upgrade para o Windows 11.")
              .Warn("O Windows 10 só recebe atualizações de segurança com o programa ESU desde 14/10/2025.")
              .Dw(PolWU, "TargetReleaseVersion", 1)
              .Sz(PolWU, "ProductVersion", "Windows 10")
              .Sz(PolWU, "TargetReleaseVersionInfo", "22H2")
              .Win10();

            T("upd.disable", c, Risk.Alto, "Desativar o Windows Update por completo",
              "Desliga as atualizações automáticas e o serviço do Windows Update.")
              .Warn("O PC fica sem atualizações de segurança. Use só em máquinas isoladas e reative depois.")
              .Dw(PolWU + @"\AU", "NoAutoUpdate", 1)
              .Svc("wuauserv", StartMode.Desativado);
        }

        // ==================================================================
        static void Desempenho()
        {
            Category c = Category.Desempenho;

            T("perf.servicos", c, Risk.Baixo, "Serviços pouco usados em Manual",
              "Mapas baixados, Assistente de Compatibilidade, Rastreamento de Links e Modo de Demonstração passam para Manual/Desativado. " +
              "Não mexe em impressão, cartão inteligente (token A3), acesso remoto nem rede.")
              .Svc("MapsBroker", StartMode.Manual)
              .Svc("PcaSvc", StartMode.Manual)
              .Svc("TrkWks", StartMode.Manual)
              .Svc("RetailDemo", StartMode.Desativado)
              .Svc("RemoteRegistry", StartMode.Desativado);

            T("perf.faststartup", c, Risk.Baixo, "Desativar Inicialização Rápida",
              "Desligar passa a desligar de verdade. Evita problemas com USB, drivers e atualizações pendentes.")
              .Dw(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);

            T("perf.startupdelay", c, Risk.Baixo, "Remover atraso dos programas na inicialização",
              "Os programas do Iniciar com o Windows abrem sem a espera padrão de alguns segundos.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0);

            T("perf.menudelay", c, Risk.Baixo, "Menus mais rápidos", "Reduz o atraso para abrir submenus de 400 ms para 50 ms.")
              .Sz(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "50");

            T("perf.transparency", c, Risk.Baixo, "Desativar transparência", "Desliga o efeito de vidro na barra e no Iniciar.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);

            T("perf.highperf", c, Risk.Baixo, "Plano de energia Alto desempenho",
              "Ativa o plano Alto desempenho. Se o PC não tiver esse plano, ele é criado.")
              .Warn("Em notebooks a bateria dura menos.")
              .Cmd("Ativar plano Alto desempenho",
                   "$rx = '([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'\n" +
                   "$cur = [regex]::Match((powercfg /getactivescheme | Out-String), $rx).Value\n" +
                   "$hp = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'\n" +
                   "if ($cur -eq $hp) { Write-Output 'SKIP:Já estava em Alto desempenho'; return }\n" +
                   "powercfg /setactive $hp | Out-Null\n" +
                   "if ($LASTEXITCODE -ne 0) {\n" +
                   "  $hp = [regex]::Match((powercfg /duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c | Out-String), $rx).Value\n" +
                   "  if (-not $hp) { throw 'Não foi possível criar o plano Alto desempenho' }\n" +
                   "  powercfg /setactive $hp | Out-Null\n" +
                   "}\n" +
                   "if ($cur) { Write-Output \"UNDO:$cur\" }\n" +
                   "Write-Output 'Plano Alto desempenho ativado'\n",
                   "foreach ($g in {UNDO}) { powercfg /setactive $g | Out-Null }\n",
                   "Voltar ao plano de energia anterior")
              .Detect("$a = powercfg /getactivescheme | Out-String\n" +
                      "if ($a -match '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c' -or $a -match 'Alto desempenho|High performance') { '1' } else { '0' }");

            T("perf.ultimate", c, Risk.Baixo, "Plano de energia Desempenho Máximo",
              "Cria e ativa o plano Desempenho Máximo (Ultimate Performance), que evita economia de energia no processador.")
              .Warn("Mais consumo e calor. Em notebooks a bateria dura bem menos.")
              .Cmd("Ativar plano Desempenho Máximo",
                   "$rx = '([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})'\n" +
                   "$cur = [regex]::Match((powercfg /getactivescheme | Out-String), $rx).Value\n" +
                   "$ul = '" + UltimateGuid + "'\n" +
                   "if ($cur -eq $ul) { Write-Output 'SKIP:Já estava em Desempenho Máximo'; return }\n" +
                   "if (-not ((powercfg /list | Out-String) -match $ul)) {\n" +
                   "  powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 $ul | Out-Null\n" +
                   "  if ($LASTEXITCODE -ne 0) { throw 'Este Windows não tem o plano Desempenho Máximo' }\n" +
                   "}\n" +
                   "powercfg /setactive $ul | Out-Null\n" +
                   "if ($LASTEXITCODE -ne 0) { throw 'Falha ao ativar o plano' }\n" +
                   "if ($cur) { Write-Output \"UNDO:$cur\" }\n" +
                   "Write-Output 'Plano Desempenho Máximo ativado'\n",
                   "foreach ($g in {UNDO}) { powercfg /setactive $g | Out-Null }\n",
                   "Voltar ao plano de energia anterior")
              .Detect("if ((powercfg /getactivescheme | Out-String) -match '" + UltimateGuid + "') { '1' } else { '0' }");

            T("perf.hibernate", c, Risk.Baixo, "Desativar hibernação",
              "Apaga o arquivo hiberfil.sys (libera alguns GB) e desliga a hibernação. Também desliga a Inicialização Rápida.")
              .Warn("Notebooks deixam de hibernar com bateria fraca.")
              .Cmd("Desativar hibernação",
                   "powercfg /hibernate off\nif ($LASTEXITCODE -ne 0) { throw 'powercfg falhou' }\nWrite-Output 'Hibernação desativada'\n",
                   "powercfg /hibernate on | Out-Null\n",
                   "Reativar hibernação")
              .Detect("$v = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name HibernateEnabled -ErrorAction SilentlyContinue).HibernateEnabled\n" +
                      "if ($v -eq 0) { '1' } else { '0' }");

            T("perf.visual", c, Risk.Baixo, "Efeitos visuais para melhor desempenho",
              "Desliga animações e sombras do Windows, mantendo as fontes suavizadas.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2)
              .Bin(@"HKCU\Control Panel\Desktop", "UserPreferencesMask", "9012038010000000")
              .Sz(@"HKCU\Control Panel\Desktop", "FontSmoothing", "2")
              .Sz(@"HKCU\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0")
              .Dw(Adv, "TaskbarAnimations", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0)
              .Restart();

            T("perf.bgapps", c, Risk.Medio, "Impedir apps da Store em segundo plano",
              "Apps da Microsoft Store não rodam quando estão fechados.")
              .Warn("Apps da Store como WhatsApp e Teams podem parar de mostrar notificações.")
              .Dw(PolWin + "AppPrivacy", "LetAppsRunInBackground", 2);

            T("perf.sysmain", c, Risk.Medio, "Desativar SysMain (Superfetch)",
              "Para o pré-carregamento de programas na memória.")
              .Warn("Em PCs com HD (não SSD) os programas podem abrir mais devagar.")
              .Svc("SysMain", StartMode.Desativado);

            T("perf.search", c, Risk.Medio, "Desativar indexação de arquivos (Windows Search)",
              "Para o serviço que indexa arquivos para a pesquisa.")
              .Warn("A busca de arquivos no Iniciar e no Outlook fica bem mais lenta.")
              .Svc("WSearch", StartMode.Desativado);
        }

        // ==================================================================
        static void Jogos()
        {
            Category c = Category.Jogos;

            T("game.mode", c, Risk.Baixo, "Ativar Modo de Jogo", "O Windows prioriza o jogo aberto e segura atualizações durante a partida.")
              .Dw(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
              .Dw(@"HKCU\Software\Microsoft\GameBar", "AllowAutoGameMode", 1);

            T("game.dvr", c, Risk.Baixo, "Desativar gravação em segundo plano (Game DVR)",
              "Desliga a captura automática de jogos, que consome CPU/GPU.")
              .Warn("A gravação pela Game Bar (Win+Alt+R) deixa de funcionar.")
              .Dw(GameCfg, "GameDVR_Enabled", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)
              .Dw(PolWin + "GameDVR", "AllowGameDVR", 0);

            T("game.mouseacc", c, Risk.Baixo, "Desativar aceleração do mouse",
              "Desliga \"Aumentar precisão do ponteiro\": o ponteiro anda igual ao movimento da mão.")
              .Sz(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0")
              .Sz(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0")
              .Sz(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0")
              .Restart();

            T("game.windowed", c, Risk.Baixo, "Otimizações para jogos em janela",
              "Melhora a latência de jogos DirectX 10/11 em janela ou sem borda.")
              .Sz(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=1;")
              .Win11();

            T("game.priority", c, Risk.Baixo, "Mais prioridade de CPU e GPU para jogos",
              "Ajusta o agendador multimídia (MMCSS) para dar prioridade alta a jogos.")
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10)
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8)
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6)
              .Sz(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High")
              .Sz(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "SFIO Priority", "High")
              .Restart();

            T("game.network", c, Risk.Baixo, "Desativar limitação de rede durante multimídia",
              "Remove o limite de pacotes de rede que o Windows aplica enquanto toca áudio ou vídeo.")
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 0xFFFFFFFF)
              .Restart();

            T("game.fso", c, Risk.Medio, "Desativar otimizações de tela cheia",
              "Força a tela cheia exclusiva em jogos antigos. Pode reduzir o input lag.")
              .Warn("Pode atrapalhar HDR e a troca rápida com Alt+Tab.")
              .Dw(GameCfg, "GameDVR_FSEBehaviorMode", 2)
              .Dw(GameCfg, "GameDVR_HonorUserFSEBehaviorMode", 1)
              .Dw(GameCfg, "GameDVR_FSEBehavior", 2)
              .Dw(GameCfg, "GameDVR_DXGIHonorFSEWindowsCompatible", 1);

            T("game.hags", c, Risk.Medio, "Ativar agendamento de GPU acelerado por hardware",
              "Deixa a placa de vídeo gerenciar a própria memória. Ajuda em GPUs recentes (NVIDIA 10xx+, AMD RX 5000+).")
              .Warn("Em algumas placas e drivers causa travamentos. Exige reiniciar.")
              .Dw(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)
              .Restart();
        }

        // ==================================================================
        static void Interface()
        {
            Category c = Category.Interface;

            T("ui.ext", c, Risk.Baixo, "Mostrar extensões dos arquivos", "Exibe .exe, .pdf, .xml etc. no nome dos arquivos. Ajuda a evitar vírus disfarçados.")
              .Dw(Adv, "HideFileExt", 0);

            T("ui.numlock", c, Risk.Baixo, "Num Lock ligado ao iniciar", "O teclado numérico já vem ativo na tela de login e depois dela.")
              .Sz(@"HKU\.DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators", "2")
              .Sz(@"HKCU\Control Panel\Keyboard", "InitialKeyboardIndicators", "2");

            T("ui.chat", c, Risk.Baixo, "Remover ícone do Chat da barra", "Tira o botão do Chat/Teams da barra do Windows 11.")
              .Dw(Adv, "TaskbarMn", 0)
              .Win11();

            T("ui.meetnow", c, Risk.Baixo, "Remover \"Reunir Agora\" da barra", "Tira o ícone do Skype Reunir Agora da área de notificação.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1)
              .Win10();

            T("ui.people", c, Risk.Baixo, "Remover \"Pessoas\" da barra", "Tira o botão Pessoas da barra de tarefas do Windows 10.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People", "PeopleBand", 0)
              .Win10();

            T("ui.endtask", c, Risk.Baixo, "\"Finalizar tarefa\" no clique direito da barra",
              "Permite fechar um programa travado pelo botão direito na barra de tarefas (Windows 11 23H2+).")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1)
              .Win11();

            T("ui.hidden", c, Risk.Baixo, "Mostrar arquivos ocultos", "Exibe pastas e arquivos ocultos no Explorer.")
              .Dw(Adv, "Hidden", 1);

            T("ui.thispc", c, Risk.Baixo, "Explorer abre em \"Este Computador\"", "Em vez do Acesso rápido/Início.")
              .Dw(Adv, "LaunchTo", 1);

            T("ui.classicmenu", c, Risk.Baixo, "Menu de contexto clássico", "Volta o menu do botão direito completo do Windows 10, sem precisar de \"Mostrar mais opções\".")
              .Sz(@"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "")
              .Win11().Restart();

            T("ui.searchicon", c, Risk.Baixo, "Pesquisa da barra só como ícone", "Troca a caixa de pesquisa grande por um ícone.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 1);

            T("ui.stickykeys", c, Risk.Baixo, "Desativar atalho das Teclas de Aderência",
              "Apertar Shift 5 vezes não abre mais o aviso de Teclas de Aderência (nem Filtragem/Alternância).")
              .Sz(@"HKCU\Control Panel\Accessibility\StickyKeys", "Flags", "506")
              .Sz(@"HKCU\Control Panel\Accessibility\ToggleKeys", "Flags", "58")
              .Sz(@"HKCU\Control Panel\Accessibility\Keyboard Response", "Flags", "122");

            T("ui.taskview", c, Risk.Baixo, "Ocultar botão Visão de Tarefas", "Tira o botão de áreas de trabalho da barra.")
              .Dw(Adv, "ShowTaskViewButton", 0);

            T("ui.recent", c, Risk.Baixo, "Não mostrar arquivos recentes", "Esconde arquivos recentes e pastas frequentes no Iniciar e no Explorer.")
              .Dw(Adv, "Start_TrackDocs", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0);

            T("ui.bsod", c, Risk.Baixo, "Tela azul com detalhes técnicos", "Mostra o código e o driver causador na tela azul.")
              .Dw(@"HKLM\SYSTEM\CurrentControlSet\Control\CrashControl", "DisplayParameters", 1);

            T("ui.verbose", c, Risk.Baixo, "Mensagens detalhadas ao ligar e desligar", "Mostra o que o Windows está fazendo em vez de só \"Aguarde\".")
              .Dw(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1);

            T("ui.taskbarleft", c, Risk.Baixo, "Botão Iniciar à esquerda", "Alinha os ícones da barra à esquerda, como no Windows 10.")
              .Dw(Adv, "TaskbarAl", 0)
              .Win11();

            T("ui.dark", c, Risk.Baixo, "Tema escuro", "Ativa o modo escuro no Windows e nos apps.")
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0)
              .Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0);

            T("ui.seconds", c, Risk.Baixo, "Mostrar segundos no relógio", "O relógio da barra mostra os segundos.")
              .Dw(Adv, "ShowSecondsInSystemClock", 1);

            T("ui.snapflyout", c, Risk.Baixo, "Desativar sugestões de layout no botão maximizar",
              "Passar o mouse sobre o maximizar deixa de abrir os layouts de janelas.")
              .Dw(Adv, "EnableSnapAssistFlyout", 0)
              .Win11();
        }

        // ==================================================================
        static void Ferramentas()
        {
            Category c = Category.Ferramentas;

            T("tool.temp", c, Risk.Baixo, "Limpar arquivos temporários", "Apaga o conteúdo das pastas Temp do usuário e do Windows. Arquivos em uso são pulados.")
              .Cmd("Limpar pastas temporárias",
                   "$total = 0\n" +
                   "foreach ($d in @($env:TEMP, \"$env:SystemRoot\\Temp\")) {\n" +
                   "  if (-not (Test-Path $d)) { continue }\n" +
                   "  Get-ChildItem -LiteralPath $d -Force -ErrorAction SilentlyContinue | ForEach-Object {\n" +
                   "    $size = 0\n" +
                   "    try { $size = (Get-ChildItem -LiteralPath $_.FullName -Recurse -Force -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum } catch { }\n" +
                   "    if ($_.PSIsContainer -eq $false) { $size = $_.Length }\n" +
                   "    Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue\n" +
                   "    if (-not (Test-Path -LiteralPath $_.FullName)) { $total += [long]$size }\n" +
                   "  }\n" +
                   "}\n" +
                   "Write-Output ('Liberados {0:N0} MB' -f ($total / 1MB))\n");

            T("tool.wucache", c, Risk.Baixo, "Limpar cache do Windows Update",
              "Apaga instaladores de atualizações já baixados. Resolve atualizações travadas.")
              .Cmd("Limpar SoftwareDistribution\\Download",
                   "$dl = \"$env:SystemRoot\\SoftwareDistribution\\Download\"\n" +
                   "Stop-Service wuauserv, bits -Force -ErrorAction SilentlyContinue\n" +
                   "try { Get-ChildItem -LiteralPath $dl -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }\n" +
                   "finally { Start-Service bits, wuauserv -ErrorAction SilentlyContinue }\n" +
                   "Write-Output 'Cache do Windows Update limpo'\n");

            T("tool.components", c, Risk.Baixo, "Limpeza de componentes do Windows (WinSxS)",
              "Remove versões antigas de componentes substituídos por atualizações. Pode demorar vários minutos.")
              .Cmd("DISM StartComponentCleanup",
                   "Dism.exe /Online /Cleanup-Image /StartComponentCleanup /Quiet\n" +
                   "if ($LASTEXITCODE -ne 0) { throw \"DISM retornou $LASTEXITCODE\" }\n" +
                   "Write-Output 'Limpeza de componentes concluída'\n", 3600);

            T("tool.dns", c, Risk.Baixo, "Limpar cache DNS", "Resolve sites que não abrem depois de mudanças de DNS.")
              .Cmd("ipconfig /flushdns", "ipconfig /flushdns | Out-Null\nWrite-Output 'Cache DNS limpo'\n");

            T("tool.recycle", c, Risk.Medio, "Esvaziar a Lixeira", "Apaga de vez tudo o que está na Lixeira de todos os discos.")
              .Warn("Os arquivos da Lixeira não podem ser recuperados depois.")
              .Cmd("Esvaziar Lixeira", "Clear-RecycleBin -Force -ErrorAction SilentlyContinue\nWrite-Output 'Lixeira esvaziada'\n");

            T("tool.sfc", c, Risk.Baixo, "Verificar arquivos do sistema (SFC)",
              "Procura e corrige arquivos do Windows corrompidos. Leva de 5 a 20 minutos.")
              .Cmd("sfc /scannow",
                   "sfc.exe /scannow | Out-Null\n" +
                   "switch ($LASTEXITCODE) {\n" +
                   "  0 { Write-Output 'SFC concluído (veja CBS.log para detalhes)' }\n" +
                   "  default { throw \"SFC terminou com código $LASTEXITCODE. Rode o reparo de imagem (DISM) e tente de novo.\" }\n" +
                   "}\n", 3600)
              .Restart();

            T("tool.dism", c, Risk.Baixo, "Reparar imagem do Windows (DISM)",
              "Baixa da Microsoft e repara componentes corrompidos do Windows. Precisa de internet e pode demorar.")
              .Cmd("DISM RestoreHealth",
                   "Dism.exe /Online /Cleanup-Image /RestoreHealth /Quiet\n" +
                   "if ($LASTEXITCODE -ne 0) { throw \"DISM retornou $LASTEXITCODE\" }\n" +
                   "Write-Output 'Imagem do Windows reparada'\n", 3600);

            T("tool.netreset", c, Risk.Alto, "Resetar rede (Winsock e TCP/IP)",
              "Recria a configuração de rede do Windows. Resolve internet que não funciona por erro de configuração.")
              .Warn("Apaga IPs fixos e configurações manuais de rede (ex.: impressora fiscal ou PDV em rede). Anote antes. Exige reiniciar.")
              .Cmd("netsh winsock reset / int ip reset",
                   "netsh winsock reset | Out-Null\n" +
                   "netsh int ip reset | Out-Null\n" +
                   "Write-Output 'Rede resetada. Reinicie o computador.'\n")
              .Restart();
        }
    }
}
