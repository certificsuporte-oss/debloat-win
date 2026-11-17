// GERADO por tools/gerar_apps.py a partir de tools/apps.tsv. Não edite à mão.
namespace DebloatWin.Core
{
    public static partial class Catalog
    {
        static void Programas()
        {
            Category c = Category.Programas;
            T("app.googlechrome", c, Risk.Baixo, "Google Chrome", "Navegador do Google, o mais usado.").Winget("Google.Chrome", null).InGroup("Navegadores");
            T("app.mozillafirefox", c, Risk.Baixo, "Mozilla Firefox", "Navegador aberto da Mozilla, focado em privacidade.").Winget("Mozilla.Firefox", null).InGroup("Navegadores");
            T("app.brave", c, Risk.Baixo, "Brave", "Navegador com bloqueio de anúncios embutido.").Winget("Brave.Brave", null).InGroup("Navegadores");
            T("app.opera", c, Risk.Baixo, "Opera", "Navegador com VPN gratuita e bloqueador de anúncios.").Winget("Opera.Opera", null).InGroup("Navegadores");
            T("app.vivaldi", c, Risk.Baixo, "Vivaldi", "Navegador muito personalizável.").Winget("Vivaldi.Vivaldi", null).InGroup("Navegadores");
            T("app.whatsapp", c, Risk.Baixo, "WhatsApp", "WhatsApp oficial para Windows (pela Microsoft Store).").Winget("msstore:9NKSQGP7F2NH", "5319275A.WhatsAppDesktop").InGroup("Comunicação");
            T("app.telegram", c, Risk.Baixo, "Telegram", "Telegram para computador.").Winget("Telegram.TelegramDesktop", null).InGroup("Comunicação");
            T("app.discord", c, Risk.Baixo, "Discord", "Chat de voz e texto, muito usado por gamers.").Winget("Discord.Discord", null).InGroup("Comunicação");
            T("app.zoom", c, Risk.Baixo, "Zoom", "Videochamadas e reuniões.").Winget("Zoom.Zoom", null).InGroup("Comunicação");
            T("app.microsoftteams", c, Risk.Baixo, "Microsoft Teams", "Reuniões e chat do Microsoft 365.").Winget("Microsoft.Teams", null).InGroup("Comunicação");
            T("app.slack", c, Risk.Baixo, "Slack", "Chat de equipes.").Winget("SlackTechnologies.Slack", null).InGroup("Comunicação");
            T("app.signal", c, Risk.Baixo, "Signal", "Mensageiro com criptografia de ponta a ponta.").Winget("OpenWhisperSystems.Signal", null).InGroup("Comunicação");
            T("app.thunderbird", c, Risk.Baixo, "Thunderbird", "Cliente de e-mail gratuito da Mozilla.").Winget("Mozilla.Thunderbird", null).InGroup("Comunicação");
            T("app.libreoffice", c, Risk.Baixo, "LibreOffice", "Pacote office gratuito (texto, planilha, apresentação).").Winget("TheDocumentFoundation.LibreOffice", null).InGroup("Escritório e PDF");
            T("app.onlyoffice", c, Risk.Baixo, "ONLYOFFICE", "Pacote office gratuito, muito compatível com arquivos do Office.").Winget("ONLYOFFICE.DesktopEditors", null).InGroup("Escritório e PDF");
            T("app.adobeacrobatreader", c, Risk.Baixo, "Adobe Acrobat Reader", "Leitor de PDF da Adobe.").Winget("Adobe.Acrobat.Reader.64-bit", null).InGroup("Escritório e PDF");
            T("app.foxitpdfreader", c, Risk.Baixo, "Foxit PDF Reader", "Leitor de PDF leve.").Winget("Foxit.FoxitReader", null).InGroup("Escritório e PDF");
            T("app.sumatrapdf", c, Risk.Baixo, "Sumatra PDF", "Leitor de PDF muito leve e rápido.").Winget("SumatraPDF.SumatraPDF", null).InGroup("Escritório e PDF");
            T("app.pdf24creator", c, Risk.Baixo, "PDF24 Creator", "Juntar, dividir, comprimir e converter PDF; impressora PDF.").Winget("geeksoftwareGmbH.PDF24Creator", null).InGroup("Escritório e PDF");
            T("app.pdfsambasic", c, Risk.Baixo, "PDFsam Basic", "Juntar, dividir e girar páginas de PDF.").Winget("PDFsam.PDFsam", null).InGroup("Escritório e PDF");
            T("app.naps2", c, Risk.Baixo, "NAPS2", "Digitalizar documentos do scanner direto para PDF.").Winget("Cyanfish.NAPS2", null).InGroup("Escritório e PDF");
            T("app.7zip", c, Risk.Baixo, "7-Zip", "Compactador gratuito (zip, rar, 7z).").Winget("7zip.7zip", null).InGroup("Arquivos");
            T("app.winrar", c, Risk.Baixo, "WinRAR", "Compactador clássico de arquivos RAR e ZIP.").Winget("RARLab.WinRAR", null).InGroup("Arquivos");
            T("app.nanazip", c, Risk.Baixo, "NanaZip", "7-Zip com visual moderno do Windows 11.").Winget("M2Team.NanaZip", null).InGroup("Arquivos");
            T("app.peazip", c, Risk.Baixo, "PeaZip", "Compactador gratuito com muitos formatos.").Winget("Giorgiotani.Peazip", null).InGroup("Arquivos");
            T("app.everything", c, Risk.Baixo, "Everything", "Busca instantânea de arquivos no PC.").Winget("voidtools.Everything", null).InGroup("Arquivos");
            T("app.vlc", c, Risk.Baixo, "VLC", "Toca qualquer vídeo e áudio.").Winget("VideoLAN.VLC", null).InGroup("Multimídia");
            T("app.klitecodecpack", c, Risk.Baixo, "K-Lite Codec Pack", "Codecs para abrir qualquer formato de vídeo.").Winget("CodecGuide.K-LiteCodecPack.Standard", null).InGroup("Multimídia");
            T("app.spotify", c, Risk.Baixo, "Spotify", "Música por streaming.").Winget("Spotify.Spotify", null).InGroup("Multimídia");
            T("app.aimp", c, Risk.Baixo, "AIMP", "Player de música leve.").Winget("AIMP.AIMP", null).InGroup("Multimídia");
            T("app.audacity", c, Risk.Baixo, "Audacity", "Gravar e editar áudio.").Winget("Audacity.Audacity", null).InGroup("Multimídia");
            T("app.gimp", c, Risk.Baixo, "GIMP", "Editor de imagens gratuito.").Winget("GIMP.GIMP.3", null).InGroup("Multimídia");
            T("app.paintnet", c, Risk.Baixo, "Paint.NET", "Editor de imagens simples e rápido.").Winget("dotPDN.PaintDotNet", null).InGroup("Multimídia");
            T("app.irfanview", c, Risk.Baixo, "IrfanView", "Visualizador de imagens leve.").Winget("IrfanSkiljan.IrfanView", null).InGroup("Multimídia");
            T("app.sharex", c, Risk.Baixo, "ShareX", "Captura de tela e gravação com edição.").Winget("ShareX.ShareX", null).InGroup("Multimídia");
            T("app.obsstudio", c, Risk.Baixo, "OBS Studio", "Gravar a tela e fazer lives.").Winget("OBSProject.OBSStudio", null).InGroup("Multimídia");
            T("app.handbrake", c, Risk.Baixo, "HandBrake", "Converter e comprimir vídeos.").Winget("HandBrake.HandBrake", null).InGroup("Multimídia");
            T("app.anydesk", c, Risk.Baixo, "AnyDesk", "Acesso remoto rápido.").Winget("AnyDesk.AnyDesk", null).InGroup("Acesso remoto");
            T("app.teamviewer", c, Risk.Baixo, "TeamViewer", "Acesso remoto e suporte.").Winget("TeamViewer.TeamViewer", null).InGroup("Acesso remoto");
            T("app.tightvnc", c, Risk.Baixo, "TightVNC", "Servidor e visualizador VNC.").Winget("GlavSoft.TightVNC", null).InGroup("Acesso remoto");
            T("app.chromeremotedesktop", c, Risk.Baixo, "Chrome Remote Desktop", "Acesso remoto do Google (host).").Winget("Google.ChromeRemoteDesktopHost", null).InGroup("Acesso remoto");
            T("app.googledrive", c, Risk.Baixo, "Google Drive", "Sincroniza arquivos com o Google Drive.").Winget("Google.GoogleDrive", null).InGroup("Nuvem");
            T("app.dropbox", c, Risk.Baixo, "Dropbox", "Sincroniza arquivos com o Dropbox.").Winget("Dropbox.Dropbox", null).InGroup("Nuvem");
            T("app.mega", c, Risk.Baixo, "MEGA", "Sincroniza arquivos com o MEGA.").Winget("Mega.MEGASync", null).InGroup("Nuvem");
            T("app.nextcloud", c, Risk.Baixo, "Nextcloud", "Sincroniza com um servidor Nextcloud.").Winget("Nextcloud.NextcloudDesktop", null).InGroup("Nuvem");
            T("app.notepadpp", c, Risk.Baixo, "Notepad++", "Editor de texto avançado.").Winget("Notepad++.Notepad++", null).InGroup("Utilitários");
            T("app.powertoys", c, Risk.Baixo, "PowerToys", "Ferramentas extras da Microsoft (atalhos, janelas, renomear).").Winget("Microsoft.PowerToys", null).InGroup("Utilitários");
            T("app.windowsterminal", c, Risk.Baixo, "Windows Terminal", "Terminal moderno da Microsoft.").Winget("Microsoft.WindowsTerminal", null).InGroup("Utilitários");
            T("app.powershell7", c, Risk.Baixo, "PowerShell 7", "Versão nova do PowerShell.").Winget("Microsoft.PowerShell", null).InGroup("Utilitários");
            T("app.crystaldiskinfo", c, Risk.Baixo, "CrystalDiskInfo", "Saúde do HD/SSD (SMART).").Winget("CrystalDewWorld.CrystalDiskInfo", null).InGroup("Utilitários");
            T("app.crystaldiskmark", c, Risk.Baixo, "CrystalDiskMark", "Teste de velocidade do disco.").Winget("CrystalDewWorld.CrystalDiskMark", null).InGroup("Utilitários");
            T("app.cpuz", c, Risk.Baixo, "CPU-Z", "Informações de processador, memória e placa-mãe.").Winget("CPUID.CPU-Z", null).InGroup("Utilitários");
            T("app.hwinfo", c, Risk.Baixo, "HWiNFO", "Informações e sensores completos do hardware.").Winget("REALiX.HWiNFO", null).InGroup("Utilitários");
            T("app.hwmonitor", c, Risk.Baixo, "HWMonitor", "Temperaturas e tensões do hardware.").Winget("CPUID.HWMonitor", null).InGroup("Utilitários");
            T("app.treesizefree", c, Risk.Baixo, "TreeSize Free", "Mostra o que ocupa espaço no disco.").Winget("JAMSoftware.TreeSize.Free", null).InGroup("Utilitários");
            T("app.wiztree", c, Risk.Baixo, "WizTree", "Análise rápida de espaço em disco.").Winget("AntibodySoftware.WizTree", null).InGroup("Utilitários");
            T("app.revouninstaller", c, Risk.Baixo, "Revo Uninstaller", "Desinstala programas e limpa sobras.").Winget("RevoUninstaller.RevoUninstaller", null).InGroup("Utilitários");
            T("app.bulkcrapuninstaller", c, Risk.Baixo, "Bulk Crap Uninstaller", "Desinstala vários programas de uma vez.").Winget("Klocman.BulkCrapUninstaller", null).InGroup("Utilitários");
            T("app.rufus", c, Risk.Baixo, "Rufus", "Cria pendrive de instalação do Windows.").Winget("Rufus.Rufus", null).InGroup("Utilitários");
            T("app.ventoy", c, Risk.Baixo, "Ventoy", "Pendrive com várias ISOs ao mesmo tempo.").Winget("Ventoy.Ventoy", null).InGroup("Utilitários");
            T("app.qbittorrent", c, Risk.Baixo, "qBittorrent", "Cliente de torrent sem anúncios.").Winget("qBittorrent.qBittorrent", null).InGroup("Utilitários");
            T("app.keepassxc", c, Risk.Baixo, "KeePassXC", "Gerenciador de senhas offline.").Winget("KeePassXCTeam.KeePassXC", null).InGroup("Utilitários");
            T("app.bitwarden", c, Risk.Baixo, "Bitwarden", "Gerenciador de senhas na nuvem.").Winget("Bitwarden.Bitwarden", null).InGroup("Utilitários");
            T("app.advancedipscanner", c, Risk.Baixo, "Advanced IP Scanner", "Lista os aparelhos da rede local.").Winget("Famatech.AdvancedIPScanner", null).InGroup("Utilitários");
            T("app.snappydriverinstaller", c, Risk.Baixo, "Snappy Driver Installer", "Instala drivers faltando (offline).").Winget("GlennDelahoy.SnappyDriverInstallerOrigin", null).InGroup("Utilitários");
            T("app.malwarebytes", c, Risk.Baixo, "Malwarebytes", "Verificação extra contra malware.").Winget("Malwarebytes.Malwarebytes", null).InGroup("Utilitários");
            T("app.visualcpp2015202264bits", c, Risk.Baixo, "Visual C++ 2015-2022 (64 bits)", "Biblioteca exigida por muitos programas e jogos.").Winget("Microsoft.VCRedist.2015+.x64", null).InGroup("Bibliotecas");
            T("app.visualcpp2015202232bits", c, Risk.Baixo, "Visual C++ 2015-2022 (32 bits)", "Versão 32 bits; muitos programas antigos precisam.").Winget("Microsoft.VCRedist.2015+.x86", null).InGroup("Bibliotecas");
            T("app.visualcpp201364bits", c, Risk.Baixo, "Visual C++ 2013 (64 bits)", "Biblioteca antiga ainda exigida por alguns programas.").Winget("Microsoft.VCRedist.2013.x64", null).InGroup("Bibliotecas");
            T("app.visualcpp201332bits", c, Risk.Baixo, "Visual C++ 2013 (32 bits)", "Versão 32 bits da biblioteca de 2013.").Winget("Microsoft.VCRedist.2013.x86", null).InGroup("Bibliotecas");
            T("app.visualcpp201064bits", c, Risk.Baixo, "Visual C++ 2010 (64 bits)", "Biblioteca de 2010 para programas antigos.").Winget("Microsoft.VCRedist.2010.x64", null).InGroup("Bibliotecas");
            T("app.visualcpp201032bits", c, Risk.Baixo, "Visual C++ 2010 (32 bits)", "Versão 32 bits da biblioteca de 2010.").Winget("Microsoft.VCRedist.2010.x86", null).InGroup("Bibliotecas");
            T("app.netdesktopruntime8", c, Risk.Baixo, ".NET Desktop Runtime 8", "Necessário para programas feitos em .NET 8.").Winget("Microsoft.DotNet.DesktopRuntime.8", null).InGroup("Bibliotecas");
            T("app.netdesktopruntime9", c, Risk.Baixo, ".NET Desktop Runtime 9", "Necessário para programas feitos em .NET 9.").Winget("Microsoft.DotNet.DesktopRuntime.9", null).InGroup("Bibliotecas");
            T("app.javajre8", c, Risk.Baixo, "Java (JRE 8)", "Java da Oracle para programas e sites antigos.").Winget("Oracle.JavaRuntimeEnvironment", null).InGroup("Bibliotecas");
            T("app.directxpacoteantigo", c, Risk.Baixo, "DirectX (pacote antigo)", "Componentes antigos do DirectX que jogos mais velhos pedem.").Winget("Microsoft.DirectX", null).InGroup("Bibliotecas");
            T("app.steam", c, Risk.Baixo, "Steam", "Loja e biblioteca de jogos da Valve.").Winget("Valve.Steam", null).InGroup("Jogos");
            T("app.epicgameslauncher", c, Risk.Baixo, "Epic Games Launcher", "Loja da Epic (jogos grátis toda semana).").Winget("EpicGames.EpicGamesLauncher", null).InGroup("Jogos");
            T("app.eaapp", c, Risk.Baixo, "EA App", "Jogos da EA.").Winget("ElectronicArts.EADesktop", null).InGroup("Jogos");
            T("app.ubisoftconnect", c, Risk.Baixo, "Ubisoft Connect", "Jogos da Ubisoft.").Winget("Ubisoft.Connect", null).InGroup("Jogos");
            T("app.goggalaxy", c, Risk.Baixo, "GOG Galaxy", "Jogos sem DRM da GOG.").Winget("GOG.Galaxy", null).InGroup("Jogos");
            T("app.battlenet", c, Risk.Baixo, "Battle.net", "Jogos da Blizzard.").Winget("Blizzard.BattleNet", null).InGroup("Jogos");
            T("app.roblox", c, Risk.Baixo, "Roblox", "Plataforma de jogos Roblox.").Winget("Roblox.Roblox", null).InGroup("Jogos");
            T("app.visualstudiocode", c, Risk.Baixo, "Visual Studio Code", "Editor de código da Microsoft.").Winget("Microsoft.VisualStudioCode", null).InGroup("Desenvolvimento");
            T("app.git", c, Risk.Baixo, "Git", "Controle de versão.").Winget("Git.Git", null).InGroup("Desenvolvimento");
            T("app.githubdesktop", c, Risk.Baixo, "GitHub Desktop", "Git com interface gráfica.").Winget("GitHub.GitHubDesktop", null).InGroup("Desenvolvimento");
            T("app.python3", c, Risk.Baixo, "Python 3", "Linguagem Python.").Winget("Python.Python.3.13", null).InGroup("Desenvolvimento");
            T("app.nodejslts", c, Risk.Baixo, "Node.js LTS", "JavaScript fora do navegador (versão estável).").Winget("OpenJS.NodeJS.LTS", null).InGroup("Desenvolvimento");
            T("app.postman", c, Risk.Baixo, "Postman", "Testar APIs.").Winget("Postman.Postman", null).InGroup("Desenvolvimento");
        }
    }
}
