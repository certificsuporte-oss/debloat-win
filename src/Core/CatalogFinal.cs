// Drivers e atualizações: rodam por último (são os itens mais demorados).
// Ordem: drivers do Windows Update → drivers que ainda faltarem (SDIO) → atualizações do Windows.
namespace DebloatWin.Core
{
    public static partial class Catalog
    {
        // Busca, baixa e instala pelo Windows Update Agent (API do próprio Windows, sem módulos extras).
        const string WuFn = @"
function Get-DbwWuErro($e) {
  $h = '{0:X8}' -f $e.Exception.HResult
  switch ($h) {
    '8024402C' { return 'sem acesso à internet ou ao Windows Update (0x8024402C)' }
    '80072EE7' { return 'sem acesso à internet (0x80072EE7)' }
    '80072EFD' { return 'sem acesso à internet (0x80072EFD)' }
    '8024001E' { return 'o serviço do Windows Update foi interrompido (0x8024001E)' }
    '80240438' { return 'o Windows Update está bloqueado neste PC (0x80240438)' }
  }
  return ($e.Exception.Message + ' (0x' + $h + ')')
}
function Write-DbwProg($pct, [string]$txt) {
  # No app: linha PROGRESS: lida ao vivo. No .ps1 exportado: barra de progresso do PowerShell.
  if ($env:DBW_LIVE) { [Console]::Out.WriteLine('PROGRESS:' + $pct + '|' + $txt); [Console]::Out.Flush() }
  elseif ($pct -ne '') { Write-Progress -Activity 'Windows Update' -Status $txt -PercentComplete ([int]$pct) }
  else { Write-Progress -Activity 'Windows Update' -Status $txt }
}
function Get-DbwCurto([string]$t) {
  if ($t -match 'KB\d+') { return $matches[0] }
  if ($t.Length -gt 60) { return $t.Substring(0, 60) + '…' }
  return $t
}
function New-DbwCallback {
  # Objeto de automação com DISPID 0: é o que o Windows Update aceita como callback (BeginDownload/BeginInstall)
  try {
    if (-not ('DbwWuCallback' -as [type])) {
      Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public class DbwWuCallback
{
    [DispId(0)]
    public void Invoke(object job, object args) { }
}
'@
    }
    return (New-Object DbwWuCallback)
  } catch { return $null }
}
function Wait-DbwJob($job, [string]$fase, $col) {
  $ultimo = ''
  $cem = $null
  while (-not $job.IsCompleted) {
    # Join com tempo (em vez de Start-Sleep) deixa o COM entregar os avisos do Windows Update nesta thread (STA)
    [void][System.Threading.Thread]::CurrentThread.Join(700)
    if ($job.IsCompleted) { break }
    try {
      $pr = $job.GetProgress()
      $n = [int]$pr.CurrentUpdateIndex
      $txt = $fase
      if ($col.Count -gt 1) { $txt += ' ' + ($n + 1) + ' de ' + $col.Count }
      if ($n -ge 0 -and $n -lt $col.Count) { $txt += ' (' + (Get-DbwCurto $col.Item($n).Title) + ')' }
      $linha = [string]$pr.PercentComplete + '|' + $txt
      if ($linha -ne $ultimo) { Write-DbwProg ([string]$pr.PercentComplete) $txt; $ultimo = $linha }
      # segurança: 100% por mais de 3 minutos sem o job encerrar → segue para o End (que espera o fim)
      if ([int]$pr.PercentComplete -ge 100) { if (-not $cem) { $cem = Get-Date } elseif (((Get-Date) - $cem).TotalMinutes -gt 3) { break } }
    } catch { }
  }
}
function Get-DbwPecas($ids) {
  # Dispositivos que respondem a um id de hardware, com o driver que estão usando agora
  $r = @()
  foreach ($d in $script:DbwPnp) {
    $hw = @($d.HardwareID) + @($d.CompatibleID)
    $bate = $false
    foreach ($i in $ids) { if ($i -and ($hw -contains $i)) { $bate = $true } }
    if (-not $bate) { continue }
    $sd = $script:DbwDrv[[string]$d.DeviceID]
    $txt = 'sem driver'
    if ($d.ConfigManagerErrorCode -ne 28 -and $sd -and $sd.DriverVersion) {
      $dt = ''
      if ($sd.DriverDate) { $dt = ', ' + ([datetime]$sd.DriverDate).ToString('dd/MM/yyyy') }
      $txt = '""' + $d.Name + '"" (' + $sd.DriverProviderName + ' ' + $sd.DriverVersion + $dt + ')'
    }
    $r += New-Object PSObject -Property @{ Id = [string]$d.DeviceID; Nome = [string]$d.Name; Driver = $txt }
  }
  return $r
}
function Update-DbwPnp {
  $script:DbwPnp = @(Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue)
  $script:DbwDrv = @{}
  foreach ($x in @(Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue)) { if ($x.DeviceID) { $script:DbwDrv[[string]$x.DeviceID] = $x } }
}
function Invoke-DbwWU([string]$criteria, [string]$nome, [bool]$soObrigatorias, [bool]$drivers) {
  $s = New-Object -ComObject Microsoft.Update.Session
  $s.ClientApplicationID = 'Debloat Win'
  Write-DbwProg '' 'Procurando no Windows Update (pode levar alguns minutos)…'
  try { $r = $s.CreateUpdateSearcher().Search($criteria) } catch { throw ('Falha ao procurar ' + $nome + ': ' + (Get-DbwWuErro $_)) }
  $col = New-Object -ComObject Microsoft.Update.UpdateColl
  foreach ($u in $r.Updates) {
    if ($soObrigatorias -and $u.BrowseOnly) { continue }
    $upg = $false
    foreach ($c in $u.Categories) { if ($c.Name -eq 'Upgrades') { $upg = $true } }
    if ($upg) { continue }
    if (-not $u.EulaAccepted) { $u.AcceptEula() }
    [void]$col.Add($u)
  }
  if ($col.Count -eq 0) { Write-Output ('SKIP:Nenhum(a) ' + $nome + ' pendente no Windows Update'); return }
  Write-Output ([string]$col.Count + ' encontrado(s).')
  # Antes de instalar: qual peça cada driver atende e o que ela usa hoje
  $antes = @{}
  if ($drivers) {
    Update-DbwPnp
    foreach ($u in $col) { $antes[$u.Identity.UpdateID] = @(Get-DbwPecas @([string]$u.DriverHardwareID)) }
  }
  $cb = New-DbwCallback
  $d = $s.CreateUpdateDownloader()
  $d.Updates = $col
  $job = $null
  if ($cb) { try { $job = $d.BeginDownload($cb, $cb, $null) } catch { $job = $null } }
  if ($job) {
    Wait-DbwJob $job 'Baixando' $col
    try { [void]$d.EndDownload($job) } catch { throw ('Falha ao baixar: ' + (Get-DbwWuErro $_)) }
    try { $job.CleanUp() } catch { }
  } else {
    Write-DbwProg '' ('Baixando ' + $col.Count + ' item(ns)…')
    try { [void]$d.Download() } catch { throw ('Falha ao baixar: ' + (Get-DbwWuErro $_)) }
  }
  $inst = New-Object -ComObject Microsoft.Update.UpdateColl
  foreach ($u in $col) { if ($u.IsDownloaded) { [void]$inst.Add($u) } }
  if ($inst.Count -eq 0) { throw 'Nada foi baixado do Windows Update' }
  $i = $s.CreateUpdateInstaller()
  $i.Updates = $inst
  $job = $null
  if ($cb) { try { $job = $i.BeginInstall($cb, $cb, $null) } catch { $job = $null } }
  if ($job) {
    Wait-DbwJob $job 'Instalando' $inst
    try { $ir = $i.EndInstall($job) } catch { throw ('Falha ao instalar: ' + (Get-DbwWuErro $_)) }
    try { $job.CleanUp() } catch { }
  } else {
    Write-DbwProg '' ('Instalando ' + $inst.Count + ' item(ns)…')
    try { $ir = $i.Install() } catch { throw ('Falha ao instalar: ' + (Get-DbwWuErro $_)) }
  }
  Write-DbwProg '100' 'Concluído'
  if ($drivers) { Update-DbwPnp }
  $ok = 0; $falhou = @()
  for ($k = 0; $k -lt $inst.Count; $k++) {
    $u = $inst.Item($k)
    $rc = $ir.GetUpdateResult($k).ResultCode
    if ($rc -ne 2 -and $rc -ne 3) { $falhou += $u.Title; continue }
    $ok++
    if (-not $drivers) { Write-Output ('- ' + $u.Title); continue }
    $novo = $u.Title
    if ($u.DriverVerDate) { $novo += ' (' + ([datetime]$u.DriverVerDate).ToString('dd/MM/yyyy') + ')' }
    $pecas = @($antes[$u.Identity.UpdateID])
    if ($pecas.Count -eq 0) { Write-Output ('- ' + $novo); continue }
    $agora = @{}
    foreach ($x in @(Get-DbwPecas @([string]$u.DriverHardwareID))) { $agora[$x.Id] = $x }
    foreach ($p in $pecas) {
      $depois = 'novo: ' + $novo
      $a = $agora[$p.Id]
      if ($a -and $a.Driver -ne $p.Driver) { $depois = 'agora ' + $a.Driver }
      elseif ($a) { $depois += ' (entra em uso depois de reiniciar)' }
      Write-Output ('- ' + $p.Nome + ': antes ' + $p.Driver + ' -> ' + $depois)
    }
  }
  if ($ok -gt 0) { Write-Output ([string]$ok + ' instalado(s).') }
  if ($falhou.Count -gt 0) { Write-Output ('Não instalou: ' + ($falhou -join '; ')) }
  if ($ir.RebootRequired) { Write-Output 'Reinicie o PC para concluir.' }
  if ($ok -eq 0) { throw ('Nenhum(a) ' + $nome + ' foi instalado(a)') }
}
";

        // Dispositivos sem driver (códigos 1, 18 e 28 do Gerenciador de Dispositivos)
        const string FaltandoFn = @"
function Get-DbwSemDriver {
  @(Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -eq 28 -or $_.ConfigManagerErrorCode -eq 1 -or $_.ConfigManagerErrorCode -eq 18 })
}
function Get-DbwNomes($l) {
  (@($l | ForEach-Object { if ($_.Name) { $_.Name } else { $_.PNPDeviceID } }) -join '; ')
}
";

        const string SdioFn = @"
function Write-DbwProg($pct, [string]$txt) {
  if ($env:DBW_LIVE) { [Console]::Out.WriteLine('PROGRESS:' + $pct + '|' + $txt); [Console]::Out.Flush() }
}
function Find-DbwSdio {
  $nome = 'SDIO_R*.exe'
  if ([Environment]::Is64BitOperatingSystem) { $nome = 'SDIO_x64_R*.exe' }
  foreach ($r in @((Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'), (Join-Path $env:ProgramFiles 'WinGet\Packages'), (Join-Path ${env:ProgramFiles(x86)} 'WinGet\Packages'))) {
    if (-not (Test-Path $r)) { continue }
    foreach ($x in @(Get-ChildItem $r -Directory -Filter 'GlennDelahoy.SnappyDriverInstallerOrigin_*' -ErrorAction SilentlyContinue)) {
      $e = Get-ChildItem $x.FullName -Filter $nome -Recurse -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
      if ($e) { return $e.FullName }
    }
  }
  return $null
}
function Invoke-DbwSdio([string]$exe, [string]$script, [int]$minutos) {
  $p = Start-Process -FilePath $exe -ArgumentList @('-license', '-hidepatreon', ('-script:' + $script)) -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
  if (-not $p.WaitForExit($minutos * 60000)) {
    try { $p.Kill() } catch { }
    throw ('o SDIO passou de ' + $minutos + ' minutos e foi encerrado')
  }
}
";

        static void Finalizar()
        {
            Category c = Category.Finalizar;

            T("fim.driverswu", c, Risk.Baixo, "Instalar drivers pelo Windows Update",
              "Procura e instala os drivers que a Microsoft tem para este PC (assinados e testados). Resolve a maioria das peças.")
              .Cmd("Procurar e instalar drivers pelo Windows Update",
                   "$ErrorActionPreference = 'Stop'\n" + WuFn +
                   "$pol = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate' -ErrorAction SilentlyContinue).ExcludeWUDriversInQualityUpdate\n" +
                   "if ($pol -eq 1) { Write-Output 'Aviso: o ajuste \"Não instalar drivers pelo Windows Update\" está ligado e pode esconder drivers.' }\n" +
                   "Invoke-DbwWU \"IsInstalled=0 and Type='Driver' and IsHidden=0\" 'driver' $false $true\n",
                   3600);

            T("fim.driverssdio", c, Risk.Baixo, "Instalar drivers que ainda faltarem (SDIO)",
              "Se algum dispositivo continuar sem driver, usa o Snappy Driver Installer Origin (gratuito e aberto) para baixar e instalar só o que falta. " +
              "Não troca drivers que já funcionam. Se não faltar nada, não baixa nada.")
              .Cmd("Instalar com o SDIO os drivers que faltam",
                   "$ErrorActionPreference = 'Continue'\n" + FaltandoFn + WingetInstallAction.WingetFn + SdioFn + @"
$antes = Get-DbwSemDriver
if ($antes.Count -eq 0) { Write-Output 'SKIP:Nenhum dispositivo sem driver'; return }
Write-Output ('Sem driver: ' + (Get-DbwNomes $antes))
$exe = Find-DbwSdio
if (-not $exe) {
  Write-DbwProg '' 'Instalando o SDIO pelo winget…'
  $w = Get-DbwWinget
  & $w install --id GlennDelahoy.SnappyDriverInstallerOrigin -e --source winget --silent --accept-package-agreements --accept-source-agreements | Out-Null
  $exe = Find-DbwSdio
}
if (-not $exe) { throw 'Não consegui instalar o SDIO pelo winget' }
$dir = Join-Path $env:ProgramData 'DebloatWin'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$s1 = Join-Path $dir 'sdio-indices.txt'
$s2 = Join-Path $dir 'sdio-instalar.txt'
Set-Content -Path $s1 -Encoding ASCII -Value @('verbose 384', 'logging on', 'enableinstall on', 'keeptempfiles off', 'init', 'checkupdates', 'onerror goto :end', 'get indexes', ':end', 'end')
Set-Content -Path $s2 -Encoding ASCII -Value @('verbose 384', 'logging on', 'enableinstall on', 'keeptempfiles off', 'init', 'checkupdates', 'onerror goto :end', 'select missing', 'install', ':end', 'end')
$regra = 'Debloat Win - SDIO'
try { New-NetFirewallRule -DisplayName $regra -Direction Inbound -Program $exe -Action Allow -Profile Any -ErrorAction Stop | Out-Null } catch { }
try {
  Write-DbwProg '' 'SDIO: atualizando a lista de drivers…'
  Invoke-DbwSdio $exe $s1 20
  Write-DbwProg '' ('SDIO: baixando e instalando drivers para ' + $antes.Count + ' dispositivo(s)…')
  Invoke-DbwSdio $exe $s2 90
} finally {
  Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
  # os pacotes baixados podem ter gigabytes: não ficam no PC do cliente
  $pac = Join-Path (Split-Path $exe) 'drivers'
  if (Test-Path $pac) { Remove-Item $pac -Recurse -Force -ErrorAction SilentlyContinue }
}
$depois = Get-DbwSemDriver
$feitos = $antes.Count - $depois.Count
if ($feitos -gt 0) { Write-Output ([string]$feitos + ' dispositivo(s) passaram a ter driver.') }
if ($depois.Count -eq 0) { Write-Output 'Todos os dispositivos estão com driver.' }
else { Write-Output ('Ainda sem driver (veja no Gerenciador de Dispositivos ou no site do fabricante): ' + (Get-DbwNomes $depois)) }
Write-Output 'Se algum driver pedir, reinicie o PC.'
",
                   7200)
              .Detect(FaltandoFn + "if ((Get-DbwSemDriver).Count -eq 0) { '1' } else { '0' }");

            T("fim.windowsupdate", c, Risk.Baixo, "Instalar atualizações do Windows",
              "Procura, baixa e instala as atualizações pendentes (segurança, cumulativas, .NET, Defender). " +
              "Não instala prévias opcionais nem troca a versão do Windows. Fica por último porque é o mais demorado (10 a 40 min).")
              .Restart()
              .Cmd("Procurar e instalar atualizações pelo Windows Update",
                   "$ErrorActionPreference = 'Stop'\n" + WuFn +
                   "Invoke-DbwWU \"IsInstalled=0 and Type='Software' and IsHidden=0\" 'atualização' $true $false\n",
                   10800);
        }
    }
}
