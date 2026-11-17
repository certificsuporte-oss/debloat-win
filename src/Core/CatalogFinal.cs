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
function Invoke-DbwWU([string]$criteria, [string]$nome, [bool]$soObrigatorias) {
  $s = New-Object -ComObject Microsoft.Update.Session
  $s.ClientApplicationID = 'Debloat Win'
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
  Write-Output ([string]$col.Count + ' encontrado(s); baixando…')
  $d = $s.CreateUpdateDownloader()
  $d.Updates = $col
  try { [void]$d.Download() } catch { throw ('Falha ao baixar: ' + (Get-DbwWuErro $_)) }
  $inst = New-Object -ComObject Microsoft.Update.UpdateColl
  foreach ($u in $col) { if ($u.IsDownloaded) { [void]$inst.Add($u) } }
  if ($inst.Count -eq 0) { throw 'Nada foi baixado do Windows Update' }
  $i = $s.CreateUpdateInstaller()
  $i.Updates = $inst
  try { $ir = $i.Install() } catch { throw ('Falha ao instalar: ' + (Get-DbwWuErro $_)) }
  $ok = @(); $falhou = @()
  for ($k = 0; $k -lt $inst.Count; $k++) {
    $rc = $ir.GetUpdateResult($k).ResultCode
    if ($rc -eq 2 -or $rc -eq 3) { $ok += $inst.Item($k).Title } else { $falhou += $inst.Item($k).Title }
  }
  if ($ok.Count -gt 0) { Write-Output ([string]$ok.Count + ' instalado(s): ' + ($ok -join '; ')) }
  if ($falhou.Count -gt 0) { Write-Output ('Não instalou: ' + ($falhou -join '; ')) }
  if ($ir.RebootRequired) { Write-Output 'Reinicie o PC para concluir.' }
  if ($ok.Count -eq 0) { throw ('Nenhum(a) ' + $nome + ' foi instalado(a)') }
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
                   "Invoke-DbwWU \"IsInstalled=0 and Type='Driver' and IsHidden=0\" 'driver' $false\n",
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
  Invoke-DbwSdio $exe $s1 20
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
                   "Invoke-DbwWU \"IsInstalled=0 and Type='Software' and IsHidden=0\" 'atualização' $true\n",
                   10800);
        }
    }
}
