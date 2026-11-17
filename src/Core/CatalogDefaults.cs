// Aplicativos padrão (VLC para vídeos, Adobe para PDF).
//
// Limite do Windows: desde fev/2024 (KB5034763 no 10, KB5034765 no 11) o driver UCPD.sys bloqueia
// qualquer programa de gravar a escolha do usuário para .pdf, http e https. A única forma suportada
// fora de domínio é o DISM (/Import-DefaultAppAssociations), que vale para usuários NOVOS do PC.
// Para o usuário atual, o Windows exige a confirmação na tela de Configurações; por isso o ajuste
// abre essa tela quando ainda falta algo.
namespace DebloatWin.Core
{
    public static partial class Catalog
    {
        /// <summary>Extensões de vídeo que vão para o VLC.</summary>
        public static readonly string[] VideoExts = {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mpg", ".mpeg", ".3gp", ".ts", ".m2ts", ".mts", ".vob", ".divx"
        };

        const string DefaultsHelpers = @"
function Get-DbwRegApps {
  $r = @()
  foreach ($k in @('HKLM:\SOFTWARE\RegisteredApplications', 'HKLM:\SOFTWARE\WOW6432Node\RegisteredApplications')) {
    $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
    if (-not $p) { continue }
    foreach ($q in $p.PSObject.Properties) {
      if ($q.Name -like 'PS*') { continue }
      $cap = [string]$q.Value
      foreach ($c in @(('HKLM:\' + $cap), ('HKLM:\' + ($cap -replace '^(?i)software\\', 'SOFTWARE\WOW6432Node\')))) {
        $fa = Join-Path $c 'FileAssociations'
        if (Test-Path $fa) { $r += New-Object PSObject -Property @{ Name = $q.Name; Assoc = (Get-ItemProperty $fa) }; break }
      }
    }
  }
  return $r
}
";

        // Escolha do usuário atual (UserChoiceLatest é a chave nova do Windows 11 de 2025)
        const string ChoiceHelper = @"
function Get-DbwChoice([string]$ext) {
  $b = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\' + $ext
  foreach ($k in @(($b + '\UserChoiceLatest\ProgId'), ($b + '\UserChoiceLatest'), ($b + '\UserChoice'))) {
    $v = (Get-ItemProperty $k -ErrorAction SilentlyContinue).ProgId
    if ($v) { return [string]$v }
  }
  return $null
}
";

        static void AppsPadrao()
        {
            Category c = Category.Programas;

            string exts = Ps.List(VideoExts);
            string apply = DefaultsHelpers + ChoiceHelper + @"
$exts = " + exts + @"
$assoc = @()
$apps = Get-DbwRegApps
$vlc = $apps | Where-Object { $_.Name -like '*VLC*' } | Select-Object -First 1
$acro = $apps | Where-Object { $_.Name -like '*Acrobat*' -and $_.Assoc.'.pdf' } | Select-Object -First 1
if ($vlc) {
  foreach ($e in $exts) { $prog = $vlc.Assoc.$e; if ($prog) { $assoc += ,@($e, [string]$prog, 'VLC media player') } }
} elseif (Test-Path 'Registry::HKEY_CLASSES_ROOT\VLC.mp4') {
  foreach ($e in $exts) { if (Test-Path ('Registry::HKEY_CLASSES_ROOT\VLC' + $e)) { $assoc += ,@($e, ('VLC' + $e), 'VLC media player') } }
}
$pdfProg = $null
if ($acro) { $pdfProg = [string]$acro.Assoc.'.pdf' }
elseif (Test-Path 'Registry::HKEY_CLASSES_ROOT\Acrobat.Document.DC') { $pdfProg = 'Acrobat.Document.DC' }
elseif (Test-Path 'Registry::HKEY_CLASSES_ROOT\AcroExch.Document.DC') { $pdfProg = 'AcroExch.Document.DC' }
if ($pdfProg) { $assoc += ,@('.pdf', $pdfProg, 'Adobe Acrobat') }
$temVlc = @($assoc | Where-Object { $_[0] -ne '.pdf' }).Count -gt 0
if (-not $temVlc -and -not $pdfProg) { Write-Output 'SKIP:VLC e Adobe Reader não estão instalados (instale-os primeiro)'; return }

# 1) Novos usuários do PC: mescla no OEMDefaultAssociations.xml e importa pelo DISM
$oem = Join-Path $env:windir 'System32\OEMDefaultAssociations.xml'
$dir = Join-Path $env:ProgramData 'DebloatWin'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$bak = Join-Path $dir 'OEMDefaultAssociations.original.xml'
$x = New-Object System.Xml.XmlDocument
if (Test-Path $oem) {
  if (-not (Test-Path $bak)) { Copy-Item $oem $bak -Force }
  $x.Load($oem)
} else {
  $x.LoadXml('<?xml version=""1.0"" encoding=""UTF-8""?><DefaultAssociations></DefaultAssociations>')
}
$root = $x.DocumentElement
foreach ($a in $assoc) {
  foreach ($n in @($root.SelectNodes('Association'))) { if ($n.GetAttribute('Identifier') -ieq $a[0]) { [void]$root.RemoveChild($n) } }
  $el = $x.CreateElement('Association')
  $el.SetAttribute('Identifier', $a[0]); $el.SetAttribute('ProgId', $a[1]); $el.SetAttribute('ApplicationName', $a[2])
  [void]$root.AppendChild($el)
}
$out = Join-Path $dir 'associacoes-padrao.xml'
$x.Save($out)
$ErrorActionPreference = 'Continue'
dism.exe /Online /NoRestart ('/Import-DefaultAppAssociations:' + $out) | Out-Null
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { throw ('DISM falhou (código ' + $code + ')') }
if (Test-Path $bak) { Write-Output ('UNDO:' + $bak) } else { Write-Output 'UNDO:NONE' }
$feito = @()
if ($temVlc) { $feito += 'VLC para vídeos' }
if ($pdfProg) { $feito += 'Adobe para PDF' }
Write-Output ('Novos usuários do PC: ' + ($feito -join ' e ') + '.')

# 2) Usuário atual: o Windows só aceita a escolha feita na tela de Configurações
$faltaVlc = $temVlc -and -not ((Get-DbwChoice '.mp4') -like 'VLC*')
$faltaPdf = $pdfProg -and -not ((Get-DbwChoice '.pdf') -like 'Acro*')
if ($faltaVlc -or $faltaPdf) {
  $win11 = [Environment]::OSVersion.Version.Build -ge 22000
  $url = 'ms-settings:defaultapps'
  if ($win11 -and $faltaVlc -and $vlc) { $url += '?registeredAppMachine=' + [uri]::EscapeDataString($vlc.Name) }
  elseif ($win11 -and $faltaPdf -and $acro) { $url += '?registeredAppMachine=' + [uri]::EscapeDataString($acro.Name) }
  try { Start-Process $url } catch { }
  $quais = @()
  if ($faltaVlc) { $quais += 'VLC para vídeos' }
  if ($faltaPdf) { $quais += 'Adobe para PDF' }
  Write-Output ('Usuário atual: abri Configurações > Aplicativos padrão; confirme ' + ($quais -join ' e ') + ' (o Windows não deixa programa nenhum fazer isso sozinho).')
} else {
  Write-Output 'Usuário atual: já está com VLC e Adobe como padrão.'
}
";

            string undo = @"
$b = " + "{UNDO}" + @"[0]
$ErrorActionPreference = 'Continue'
if ($b -ne 'NONE' -and (Test-Path $b)) { dism.exe /Online /NoRestart ('/Import-DefaultAppAssociations:' + $b) | Out-Null }
else { dism.exe /Online /NoRestart /Remove-DefaultAppAssociations | Out-Null }
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { throw ('DISM falhou (código ' + $code + ')') }
";

            string detect = ChoiceHelper + @"
$v = Test-Path 'Registry::HKEY_CLASSES_ROOT\VLC.mp4'
$p = (Test-Path 'Registry::HKEY_CLASSES_ROOT\Acrobat.Document.DC') -or (Test-Path 'Registry::HKEY_CLASSES_ROOT\AcroExch.Document.DC')
if (-not $v -and -not $p) { '0' }
elseif ((-not $v -or ((Get-DbwChoice '.mp4') -like 'VLC*')) -and (-not $p -or ((Get-DbwChoice '.pdf') -like 'Acro*'))) { '1' }
else { '0' }
";

            T("app.padroes", c, Risk.Baixo, "VLC para vídeos e Adobe para PDF",
              "Deixa o VLC como programa padrão dos vídeos (.mp4, .mkv, .avi…) e o Adobe Acrobat Reader como padrão dos PDFs. " +
              "Rode depois de instalar os dois (na ordem da lista isso já acontece).")
              .Warn("O Windows bloqueia programas de mudar o padrão do usuário atual (principalmente de PDF). " +
                    "Para usuários novos do PC fica automático; para o usuário atual, a tela de Aplicativos padrão abre para você só confirmar.")
              .Cmd("Definir VLC (vídeos) e Adobe (PDF) como aplicativos padrão", apply, undo,
                   "Voltar as associações padrão originais do Windows para novos usuários")
              .Detect(detect)
              .InGroup("Aplicativos padrão");
        }
    }
}
