// Gera um .ps1 equivalente aos ajustes marcados, para quem prefere rodar script.
using System;
using System.Collections.Generic;
using System.Text;

namespace DebloatWin.Core
{
    public static class ScriptExporter
    {
        public static string Build(IList<Tweak> tweaks, bool restorePoint)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Script gerado pelo " + AppInfo.Name + " " + AppInfo.Version + " em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine("# Execute em um PowerShell aberto como Administrador.");
            sb.AppendLine("# Ao contrário do app, o script NÃO guarda backup para desfazer.");
            sb.AppendLine();
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            sb.AppendLine("if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {");
            sb.AppendLine("    Write-Host 'Abra o PowerShell como Administrador e rode de novo.' -ForegroundColor Red; return");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("function Set-Reg([string]$Path, [string]$Name, [string]$Type, $Value) {");
            sb.AppendLine("    if (-not (Test-Path -LiteralPath $Path)) { New-Item -Path $Path -Force | Out-Null }");
            sb.AppendLine("    if ($Name -eq '') { Set-Item -LiteralPath $Path -Value $Value }");
            sb.AppendLine("    else { New-ItemProperty -LiteralPath $Path -Name $Name -PropertyType $Type -Value $Value -Force | Out-Null }");
            sb.AppendLine("}");
            sb.AppendLine("function Set-ServiceStart([string]$Name, [int]$Start) {");
            sb.AppendLine("    $p = \"Registry::HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\$Name\"");
            sb.AppendLine("    if (Test-Path -LiteralPath $p) { Set-ItemProperty -LiteralPath $p -Name Start -Value $Start -Type DWord }");
            sb.AppendLine("    else { Write-Output \"SKIP:serviço $Name não existe\" }");
            sb.AppendLine("}");
            sb.AppendLine("$script:ok = 0; $script:falhas = 0");
            sb.AppendLine("function Invoke-Step([string]$Title, [scriptblock]$Body) {");
            sb.AppendLine("    Write-Host \"-- $Title\" -ForegroundColor Cyan");
            sb.AppendLine("    try {");
            sb.AppendLine("        $ErrorActionPreference = 'Stop'");
            sb.AppendLine("        & $Body | ForEach-Object {");
            sb.AppendLine("            $l = [string]$_");
            sb.AppendLine("            if ($l.StartsWith('UNDO:')) { return }");
            sb.AppendLine("            if ($l.StartsWith('SKIP:')) { Write-Host ('   ignorado: ' + $l.Substring(5)) -ForegroundColor DarkGray }");
            sb.AppendLine("            else { Write-Host ('   ' + $l) }");
            sb.AppendLine("        }");
            sb.AppendLine("        $script:ok++");
            sb.AppendLine("    } catch {");
            sb.AppendLine("        $script:falhas++");
            sb.AppendLine("        Write-Host ('   FALHA: ' + $_.Exception.Message) -ForegroundColor Red");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            if (restorePoint)
            {
                sb.AppendLine("Invoke-Step 'Criando ponto de restauração' {");
                sb.AppendLine("    try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" } catch { }");
                sb.AppendLine("    $k = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'");
                sb.AppendLine("    $old = (Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency");
                sb.AppendLine("    Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord");
                sb.AppendLine("    try { Checkpoint-Computer -Description " + Ps.Q(AppInfo.Name) + " -RestorePointType 'MODIFY_SETTINGS' }");
                sb.AppendLine("    finally { if ($null -eq $old) { Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue } else { Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old -Type DWord } }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            foreach (Tweak t in tweaks)
            {
                sb.AppendLine("# [" + CategoryInfo.Title(t.Category) + "] " + t.Id);
                sb.AppendLine("Invoke-Step " + Ps.Q(t.Title) + " {");
                foreach (TweakAction a in t.Actions)
                {
                    // Ações em PowerShell usam 'return' para "não se aplica":
                    // cada uma roda no seu bloco para não pular as seguintes.
                    bool block = a is PsAction;
                    string indent = block ? "        " : "    ";
                    if (block) sb.AppendLine("    & {");
                    foreach (string line in a.ToPowerShell().Replace("\r", "").TrimEnd('\n').Split('\n'))
                        sb.AppendLine(indent + line);
                    if (block) sb.AppendLine("    }");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine("Write-Host ''");
            sb.AppendLine("Write-Host (\"Concluído: $script:ok etapas ok, $script:falhas com falha.\") -ForegroundColor Green");
            sb.AppendLine("Write-Host 'Reinicie o computador para todas as mudanças valerem.'");
            return sb.ToString();
        }
    }
}
