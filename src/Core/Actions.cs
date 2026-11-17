// Tipos de ação que um ajuste pode executar, cada um com seu desfazer.
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace DebloatWin.Core
{
    /// <summary>
    /// Registro do que é preciso para desfazer uma ação. Fica salvo em JSON.
    /// </summary>
    [DataContract]
    public class UndoRecord
    {
        [DataMember] public string Type;          // "reg", "regnew", "ps"
        [DataMember] public string Key;
        [DataMember] public string Name;
        [DataMember] public bool Existed;
        [DataMember] public string Kind;          // RegKind como texto
        [DataMember] public string Data;
        [DataMember] public string Script;        // para "ps"
        [DataMember] public string Description;

        public static UndoRecord RestoreValue(string key, string name, RegValue previous)
        {
            UndoRecord u = new UndoRecord();
            u.Type = "reg"; u.Key = key; u.Name = name;
            u.Existed = previous != null;
            if (previous != null) { u.Kind = previous.Kind.ToString(); u.Data = previous.Data; }
            u.Description = string.Format("Restaurar {0}\\{1}", key, name.Length == 0 ? "(Padrão)" : name);
            return u;
        }

        /// <summary>Valor que não existia, numa chave que nós criamos (createdTop = primeira chave criada).</summary>
        public static UndoRecord RemoveCreated(string key, string name, string createdTop)
        {
            UndoRecord u = new UndoRecord();
            u.Type = "regnew"; u.Key = key; u.Name = name; u.Data = createdTop;
            u.Description = string.Format("Remover {0}\\{1} e chaves criadas vazias", key, name.Length == 0 ? "(Padrão)" : name);
            return u;
        }

        public static UndoRecord PowerShell(string description, string script)
        {
            UndoRecord u = new UndoRecord();
            u.Type = "ps"; u.Script = script; u.Description = description;
            return u;
        }
    }

    // ------------------------------------------------------------------
    // Registro
    // ------------------------------------------------------------------
    public class RegSetAction : TweakAction
    {
        public readonly string Key;
        public readonly string Name;
        public readonly RegKind Kind;
        public readonly string Data;

        public RegSetAction(string key, string name, RegKind kind, string data)
        {
            Key = RegPath.Normalize(key); Name = name ?? ""; Kind = kind; Data = data;
            // valida já na construção, para erros de catálogo aparecerem nos testes
            if (kind == RegKind.DWord) uint.Parse(data);
            if (kind == RegKind.QWord) ulong.Parse(data);
            if (kind == RegKind.Binary) Hex.ToBytes(data);
        }

        public override bool CanUndo { get { return true; } }

        public override string Describe()
        {
            return string.Format("Registro: {0} → {1} = {2} ({3})", Key, Name.Length == 0 ? "(Padrão)" : Name, Data, Kind);
        }

        /// <summary>Se definido, só aplica quando esta chave existe (ex.: programa instalado).</summary>
        public string RequireKey;

        public override ActionResult Apply(ISystem sys)
        {
            try
            {
                if (RequireKey != null && !sys.KeyExists(RequireKey))
                    return ActionResult.Skip("programa não encontrado neste PC");
                RegValue before = sys.ReadValue(Key, Name);
                if (before != null && before.SameAs(Kind, Data))
                    return ActionResult.Ok("já estava assim", null);

                // Se a chave não existe, guardamos a primeira chave que vamos criar,
                // para o desfazer apagar só o que nós criamos.
                string created = null;
                if (!sys.KeyExists(Key))
                {
                    string probe = Key;
                    while (probe != null && !sys.KeyExists(probe))
                    {
                        created = probe;
                        probe = RegPath.Parent(probe);
                    }
                }

                sys.WriteValue(Key, Name, Kind, Data);

                UndoRecord u = created != null
                    ? UndoRecord.RemoveCreated(Key, Name, created)
                    : UndoRecord.RestoreValue(Key, Name, before);
                return ActionResult.Ok(before == null ? "criado" : "alterado de " + before.Data, u);
            }
            catch (Exception ex)
            {
                return ActionResult.Fail(ex.Message);
            }
        }

        public override DetectState Detect(ISystem sys)
        {
            try
            {
                if (RequireKey != null && !sys.KeyExists(RequireKey)) return DetectState.NotApplicable;
                RegValue v = sys.ReadValue(Key, Name);
                return v != null && v.SameAs(Kind, Data) ? DetectState.Applied : DetectState.NotApplied;
            }
            catch { return DetectState.Unknown; }
        }

        public override string ToPowerShell()
        {
            string path = Ps.Q(RegPath.ToPowerShellPath(Key));
            string type;
            string value;
            switch (Kind)
            {
                case RegKind.DWord: type = "DWord"; value = Data; break;
                case RegKind.QWord: type = "QWord"; value = Data; break;
                case RegKind.ExpandString: type = "ExpandString"; value = Ps.Q(Data); break;
                case RegKind.MultiString: type = "MultiString"; value = Ps.List(Data.Split('\n')); break;
                case RegKind.Binary:
                    type = "Binary";
                    byte[] b = Hex.ToBytes(Data);
                    List<string> parts = new List<string>();
                    foreach (byte x in b) parts.Add("0x" + x.ToString("x2"));
                    value = "([byte[]](" + string.Join(",", parts.ToArray()) + "))";
                    break;
                default: type = "String"; value = Ps.Q(Data); break;
            }
            string line = string.Format("Set-Reg {0} {1} {2} {3}", path, Ps.Q(Name), type, value);
            if (RequireKey != null)
                line = string.Format("if (Test-Path -LiteralPath {0}) {{ {1} }}", Ps.Q(RegPath.ToPowerShellPath(RequireKey)), line);
            return line;
        }
    }

    // ------------------------------------------------------------------
    // Serviços (tipo de inicialização via registro)
    // ------------------------------------------------------------------
    public enum StartMode
    {
        Automatico = 2,
        Manual = 3,
        Desativado = 4
    }

    public class ServiceStartAction : TweakAction
    {
        public readonly string Service;
        public readonly StartMode Mode;
        public readonly bool StopNow;

        public ServiceStartAction(string service, StartMode mode, bool stopNow)
        {
            Service = service; Mode = mode; StopNow = stopNow;
        }

        string Key { get { return @"HKLM\SYSTEM\CurrentControlSet\Services\" + Service; } }

        public override bool CanUndo { get { return true; } }

        public override string Describe()
        {
            return string.Format("Serviço {0} → {1}", Service, Mode);
        }

        public override ActionResult Apply(ISystem sys)
        {
            try
            {
                RegValue before = sys.ReadValue(Key, "Start");
                if (before == null)
                    return ActionResult.Skip("serviço não existe neste PC");
                string target = ((int)Mode).ToString();
                UndoRecord u = null;
                string msg = "já estava assim";
                if (!before.SameAs(RegKind.DWord, target))
                {
                    sys.WriteValue(Key, "Start", RegKind.DWord, target);
                    u = UndoRecord.RestoreValue(Key, "Start", before);
                    u.Description = string.Format("Restaurar serviço {0} para Start={1}", Service, before.Data);
                    msg = "Start " + before.Data + " → " + target;
                }
                if (StopNow && Mode == StartMode.Desativado)
                {
                    sys.RunPowerShell("Stop-Service -Name " + Ps.Q(Service) + " -Force -ErrorAction SilentlyContinue", 60);
                }
                return ActionResult.Ok(msg, u);
            }
            catch (Exception ex)
            {
                return ActionResult.Fail(ex.Message);
            }
        }

        public override DetectState Detect(ISystem sys)
        {
            try
            {
                RegValue v = sys.ReadValue(Key, "Start");
                if (v == null) return DetectState.NotApplicable;
                int cur;
                if (!int.TryParse(v.Data, out cur)) return DetectState.Unknown;
                // Manual ou Desativado: já estar "mais desligado" também conta como aplicado
                if (Mode == StartMode.Automatico) return cur == 2 ? DetectState.Applied : DetectState.NotApplied;
                return cur >= (int)Mode ? DetectState.Applied : DetectState.NotApplied;
            }
            catch { return DetectState.Unknown; }
        }

        public override string ToPowerShell()
        {
            string s = string.Format("Set-ServiceStart {0} {1}", Ps.Q(Service), (int)Mode);
            if (StopNow && Mode == StartMode.Desativado)
                s += "\nStop-Service -Name " + Ps.Q(Service) + " -Force -ErrorAction SilentlyContinue";
            return s;
        }
    }

    // ------------------------------------------------------------------
    // Ações via PowerShell
    // Convenção dos scripts (funciona igual no app e no .ps1 exportado):
    //   Write-Output 'SKIP:motivo'; return   → não se aplica neste PC
    //   throw 'mensagem'                      → falhou
    //   Write-Output 'UNDO:valor'             → valor usado no desfazer ({UNDO})
    // ------------------------------------------------------------------
    public abstract class PsAction : TweakAction
    {
        public int TimeoutSeconds = 300;

        protected abstract string ApplyScript();
        /// <summary>Script de desfazer. Pode usar {UNDO} (lista de valores capturados) ou ser null.</summary>
        protected abstract string UndoScript();
        protected virtual string UndoDescription() { return "Desfazer: " + Describe(); }

        public override bool CanUndo { get { return UndoScript() != null; } }

        /// <summary>Envolve um script para rodar isolado e reportar falhas como FAIL:.</summary>
        public static string Wrap(string script)
        {
            return "$ErrorActionPreference = 'Stop'\n" +
                   "try {\n& {\n" + script + "\n}\n} catch { Write-Output ('FAIL:' + $_.Exception.Message); exit 1 }\n";
        }

        public override ActionResult Apply(ISystem sys)
        {
            ProcResult r;
            try { r = sys.RunPowerShell(Wrap(ApplyScript()), TimeoutSeconds); }
            catch (Exception ex) { return ActionResult.Fail(ex.Message); }

            List<string> undoValues = new List<string>();
            List<string> skips = new List<string>();
            List<string> fails = new List<string>();
            StringBuilder visible = new StringBuilder();
            foreach (string raw in (r.Output ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("UNDO:")) undoValues.Add(line.Substring(5));
                else if (line.StartsWith("SKIP:")) skips.Add(line.Substring(5).Trim());
                else if (line.StartsWith("FAIL:")) fails.Add(line.Substring(5).Trim());
                else if (line.Trim().Length > 0) visible.AppendLine(line.Trim());
            }
            string output = visible.ToString().Trim();
            if (output.Length > 600) output = output.Substring(0, 600) + "…";

            if (r.TimedOut || fails.Count > 0 || r.ExitCode != 0)
            {
                string msg = r.TimedOut ? "tempo esgotado (" + TimeoutSeconds + "s)"
                    : fails.Count > 0 ? string.Join(" | ", fails.ToArray()) : "código de saída " + r.ExitCode;
                if (output.Length > 0) msg += " — " + output;
                ActionResult fail = ActionResult.Fail(msg);
                // O que chegou a mudar antes da falha continua desfazível
                string partialUndo = UndoScript();
                if (partialUndo != null && partialUndo.Contains("{UNDO}") && undoValues.Count > 0)
                    fail.Undo = UndoRecord.PowerShell(UndoDescription(), partialUndo.Replace("{UNDO}", Ps.List(undoValues)));
                return fail;
            }
            if (skips.Count > 0 && undoValues.Count == 0)
                return ActionResult.Skip(string.Join(" | ", skips.ToArray()));

            UndoRecord u = null;
            string undo = UndoScript();
            if (undo != null)
            {
                if (undo.Contains("{UNDO}"))
                {
                    if (undoValues.Count > 0)
                        u = UndoRecord.PowerShell(UndoDescription(), undo.Replace("{UNDO}", Ps.List(undoValues)));
                }
                else
                {
                    u = UndoRecord.PowerShell(UndoDescription(), undo);
                }
            }
            return ActionResult.Ok(output.Length > 0 ? output : "ok", u);
        }

        public override string ToPowerShell()
        {
            return ApplyScript();
        }
    }

    /// <summary>Desativa tarefas agendadas. Guarda quais estavam ativas para reativar.</summary>
    public class TaskDisableAction : PsAction
    {
        public readonly string[] Tasks; // caminho completo: \Microsoft\Windows\Pasta\Nome

        public TaskDisableAction(params string[] tasks) { Tasks = tasks; }

        public override string Describe()
        {
            return "Desativar tarefas agendadas: " + string.Join(", ", Tasks);
        }

        protected override string ApplyScript()
        {
            return "$found = 0; $errors = @()\n" +
                   "foreach ($full in " + Ps.List(Tasks) + ") {\n" +
                   "  $i = $full.LastIndexOf('\\')\n" +
                   "  $path = $full.Substring(0, $i + 1); $name = $full.Substring($i + 1)\n" +
                   "  $t = Get-ScheduledTask -TaskPath $path -TaskName $name -ErrorAction SilentlyContinue\n" +
                   "  if (-not $t) { continue }\n" +
                   "  $found++\n" +
                   "  if ([string]$t.State -ne 'Disabled') {\n" +
                   "    try {\n" +
                   "      Disable-ScheduledTask -TaskPath $path -TaskName $name -ErrorAction Stop | Out-Null\n" +
                   "      Write-Output \"UNDO:$full\"\n" +
                   "      Write-Output \"Desativada: $full\"\n" +
                   "    } catch { $errors += \"${name}: $($_.Exception.Message)\" }\n" +
                   "  }\n" +
                   "}\n" +
                   "if ($errors.Count -gt 0) { throw ($errors -join ' | ') }\n" +
                   "if ($found -eq 0) { Write-Output 'SKIP:Nenhuma das tarefas existe neste PC'; return }\n";
        }

        protected override string UndoScript()
        {
            return "foreach ($full in {UNDO}) {\n" +
                   "  $i = $full.LastIndexOf('\\')\n" +
                   "  Enable-ScheduledTask -TaskPath $full.Substring(0, $i + 1) -TaskName $full.Substring($i + 1) -ErrorAction SilentlyContinue | Out-Null\n" +
                   "}\n";
        }

        protected override string UndoDescription() { return "Reativar tarefas agendadas"; }

        public override string DetectScript()
        {
            return "$n = 0; $off = 0\n" +
                   "foreach ($full in " + Ps.List(Tasks) + ") {\n" +
                   "  $i = $full.LastIndexOf('\\')\n" +
                   "  $t = Get-ScheduledTask -TaskPath $full.Substring(0, $i + 1) -TaskName $full.Substring($i + 1) -ErrorAction SilentlyContinue\n" +
                   "  if ($t) { $n++; if ([string]$t.State -eq 'Disabled') { $off++ } }\n" +
                   "}\n" +
                   "if ($n -eq 0) { 'NA' } elseif ($off -eq $n) { '1' } else { '0' }";
        }
    }

    /// <summary>Remove apps da Store (da imagem e dos usuários). Não tem desfazer.</summary>
    public class AppxRemoveAction : PsAction
    {
        public readonly string[] Packages; // nomes ou padrões com *, ex.: Microsoft.BingNews

        public AppxRemoveAction(params string[] packages) { Packages = packages; TimeoutSeconds = 600; }

        public override string Describe()
        {
            return "Remover apps: " + string.Join(", ", Packages);
        }

        // Primeira linha da mensagem + código HRESULT (as mensagens do Windows são enormes)
        internal const string ShortErrorFn =
            "function Get-DbwShortError($e) {\n" +
            "  if (-not $e) { return 'continua instalado' }\n" +
            "  $m = ([string]$e.Exception.Message -split \"`r?`n\" | Where-Object { $_.Trim() } | Select-Object -First 1)\n" +
            "  $c = $e.Exception.HResult\n" +
            "  if ($c) { $m = $m + ' (0x' + ('{0:X8}' -f $c) + ')' }\n" +
            "  return $m\n" +
            "}\n";

        protected override string ApplyScript()
        {
            // Ordem: 1) tira da imagem (novos usuários não recebem), 2) tenta remover de todos os usuários,
            // 3) se o Windows recusar (comum no Win10 22H2: 0x80070002), remove do usuário atual e de cada usuário.
            // Só conta como removido se o app sumiu do usuário atual.
            return ShortErrorFn +
                   "$removed = 0; $errors = @()\n" +
                   "foreach ($p in " + Ps.List(Packages) + ") {\n" +
                   "  foreach ($x in @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $p })) {\n" +
                   "    try { Remove-AppxProvisionedPackage -Online -PackageName $x.PackageName -ErrorAction Stop | Out-Null; $removed++; Write-Output \"Removido da imagem: $($x.DisplayName)\" }\n" +
                   "    catch { $errors += \"$($x.DisplayName) (imagem): $(Get-DbwShortError $_)\" }\n" +
                   "  }\n" +
                   "  foreach ($x in @(Get-AppxPackage -AllUsers -Name $p -ErrorAction SilentlyContinue)) {\n" +
                   "    $full = $x.PackageFullName; $last = $null; $all = $false\n" +
                   "    try { Remove-AppxPackage -Package $full -AllUsers -ErrorAction Stop; $all = $true } catch { $last = $_ }\n" +
                   "    if (-not $all) {\n" +
                   "      try { Remove-AppxPackage -Package $full -ErrorAction Stop } catch { $last = $_ }\n" +
                   "      foreach ($u in @($x.PackageUserInformation)) {\n" +
                   "        try { Remove-AppxPackage -Package $full -User $u.UserSecurityId.Sid -ErrorAction Stop } catch { }\n" +
                   "      }\n" +
                   "    }\n" +
                   "    $mine = @(Get-AppxPackage -Name $x.Name -ErrorAction SilentlyContinue | Where-Object { $_.PackageFullName -eq $full })\n" +
                   "    if ($mine.Count -gt 0) { $errors += \"$($x.Name): $(Get-DbwShortError $last)\"; continue }\n" +
                   "    $removed++\n" +
                   "    Write-Output \"Removido: $($x.Name)\"\n" +
                   "    if (-not $all) {\n" +
                   "      $others = @(Get-AppxPackage -AllUsers -Name $x.Name -ErrorAction SilentlyContinue | Where-Object {\n" +
                   "        $_.PackageFullName -eq $full -and @($_.PackageUserInformation | Where-Object { [string]$_.InstallState -eq 'Installed' }).Count -gt 0 })\n" +
                   "      if ($others.Count -gt 0) { Write-Output \"Aviso: $($x.Name) ainda está instalado para outro usuário deste PC\" }\n" +
                   "    }\n" +
                   "  }\n" +
                   "}\n" +
                   "if ($errors.Count -gt 0) { throw ($errors -join ' | ') }\n" +
                   "if ($removed -eq 0) { Write-Output 'SKIP:Nenhum desses apps está instalado'; return }\n";
        }

        protected override string UndoScript() { return null; }

        public override string DetectScript()
        {
            // Get-DbwAppx: apps do usuário atual e da imagem, em cache (ver Detector)
            return "$all = Get-DbwAppx; $found = $false\n" +
                   "foreach ($p in " + Ps.List(Packages) + ") { if (@($all | Where-Object { $_ -like $p }).Count -gt 0) { $found = $true } }\n" +
                   "if ($found) { '0' } else { '1' }";
        }
    }

    /// <summary>Desativa um recurso opcional do Windows (Ativar ou desativar recursos do Windows).</summary>
    public class FeatureDisableAction : PsAction
    {
        public readonly string Feature;

        public FeatureDisableAction(string feature) { Feature = feature; TimeoutSeconds = 900; }

        public override string Describe() { return "Desativar recurso do Windows: " + Feature; }

        protected override string ApplyScript()
        {
            return "$f = Get-WindowsOptionalFeature -Online -FeatureName " + Ps.Q(Feature) + " -ErrorAction SilentlyContinue\n" +
                   "if (-not $f) { Write-Output 'SKIP:Recurso não existe nesta versão'; return }\n" +
                   "if ([string]$f.State -notlike 'Enabled*') { Write-Output 'SKIP:Já estava desativado'; return }\n" +
                   "Disable-WindowsOptionalFeature -Online -FeatureName " + Ps.Q(Feature) + " -NoRestart -ErrorAction Stop | Out-Null\n" +
                   "Write-Output 'UNDO:1'\nWrite-Output 'Desativado (pode pedir reinício)'\n";
        }

        protected override string UndoScript()
        {
            return "if ({UNDO}) { Enable-WindowsOptionalFeature -Online -FeatureName " + Ps.Q(Feature) + " -NoRestart -All -ErrorAction Stop | Out-Null }\n";
        }

        protected override string UndoDescription() { return "Reativar recurso " + Feature; }

        public override string DetectScript()
        {
            return "$f = Get-WindowsOptionalFeature -Online -FeatureName " + Ps.Q(Feature) + " -ErrorAction SilentlyContinue\n" +
                   "if (-not $f) { 'NA' } elseif ([string]$f.State -like 'Enabled*') { '0' } else { '1' }";
        }
    }

    /// <summary>Remove um recurso sob demanda (Capability). O desfazer reinstala pela internet.</summary>
    public class CapabilityRemoveAction : PsAction
    {
        public readonly string Pattern; // ex.: Browser.InternetExplorer~~~~*

        public CapabilityRemoveAction(string pattern) { Pattern = pattern; TimeoutSeconds = 900; }

        public override string Describe() { return "Remover recurso opcional: " + Pattern; }

        protected override string ApplyScript()
        {
            return "$c = @(Get-WindowsCapability -Online -ErrorAction Stop | Where-Object { $_.Name -like " + Ps.Q(Pattern) + " -and [string]$_.State -eq 'Installed' })\n" +
                   "if ($c.Count -eq 0) { Write-Output 'SKIP:Não está instalado'; return }\n" +
                   "foreach ($x in $c) { Remove-WindowsCapability -Online -Name $x.Name -ErrorAction Stop | Out-Null; Write-Output \"UNDO:$($x.Name)\"; Write-Output \"Removido: $($x.Name)\" }\n";
        }

        protected override string UndoScript()
        {
            return "foreach ($n in {UNDO}) { Add-WindowsCapability -Online -Name $n -ErrorAction Stop | Out-Null }\n";
        }

        protected override string UndoDescription() { return "Reinstalar recurso " + Pattern + " (precisa de internet)"; }

        public override string DetectScript()
        {
            // Get-DbwCaps: lista em cache dos recursos sob demanda instalados (ver Detector)
            return "if (@(Get-DbwCaps | Where-Object { $_ -like " + Ps.Q(Pattern) + " }).Count -gt 0) { '0' } else { '1' }";
        }
    }

    /// <summary>Comando livre em PowerShell, com desfazer opcional.</summary>
    public class CommandAction : PsAction
    {
        readonly string description;
        readonly string apply;
        readonly string undo;
        readonly string undoDescription;

        public CommandAction(string description, string apply, string undo, string undoDescription)
        {
            this.description = description; this.apply = apply; this.undo = undo; this.undoDescription = undoDescription;
        }

        public CommandAction(string description, string apply) : this(description, apply, null, null) { }

        /// <summary>Trecho de verificação opcional (ver TweakAction.DetectScript).</summary>
        public string DetectPs;

        public override string DetectScript() { return DetectPs; }
        public override string Describe() { return description; }
        protected override string ApplyScript() { return apply; }
        protected override string UndoScript() { return undo; }
        protected override string UndoDescription() { return undoDescription ?? ("Desfazer: " + description); }
    }
}

namespace DebloatWin.Core
{
    /// <summary>
    /// Instala um programa pelo winget. Se já estava instalado, não faz nada (e o desfazer não o remove).
    /// Ids "msstore:XXXX" instalam pela Microsoft Store.
    /// </summary>
    public class WingetInstallAction : PsAction
    {
        public readonly string PackageId;   // ex.: Google.Chrome ou msstore:9NKSQGP7F2NH
        public readonly string Name;
        /// <summary>Para apps da Store: nome do pacote Appx usado para saber se já está instalado.</summary>
        public readonly string AppxName;

        public WingetInstallAction(string packageId, string name, string appxName)
        {
            PackageId = packageId; Name = name; AppxName = appxName;
            TimeoutSeconds = 1800;
        }

        bool IsStore { get { return PackageId.StartsWith("msstore:"); } }
        string Id { get { return IsStore ? PackageId.Substring(8) : PackageId; } }
        string Source { get { return IsStore ? "msstore" : "winget"; } }

        // Acha o winget; num Windows recém-instalado ele às vezes ainda não foi registrado para o usuário.
        public const string WingetFn =
            "function Get-DbwWinget {\n" +
            "  $c = Get-Command winget.exe -ErrorAction SilentlyContinue\n" +
            "  if (-not $c) {\n" +
            "    try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction Stop } catch { }\n" +
            "    $c = Get-Command winget.exe -ErrorAction SilentlyContinue\n" +
            "  }\n" +
            "  if ($c) { return $c.Source }\n" +
            "  $p = Get-ChildItem \"$env:ProgramFiles\\WindowsApps\\Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe\\winget.exe\" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1\n" +
            "  if ($p) { return $p.FullName }\n" +
            "  throw 'winget não encontrado. Atualize o \"Instalador de Aplicativo\" pela Microsoft Store e tente de novo.'\n" +
            "}\n";

        string InstalledCheck()
        {
            if (IsStore && AppxName != null)
                return "(@(Get-AppxPackage -Name " + Ps.Q(AppxName) + " -ErrorAction SilentlyContinue).Count -gt 0)";
            return "$(& $w list --id " + Ps.Q(Id) + " -e --accept-source-agreements | Out-Null; $LASTEXITCODE -eq 0)";
        }

        public override string Describe()
        {
            return string.Format("Instalar {0} pelo winget ({1}{2})", Name, Id, IsStore ? ", Microsoft Store" : "");
        }

        protected override string ApplyScript()
        {
            return "$ErrorActionPreference = 'Continue'\n" + WingetFn +
                   "$w = Get-DbwWinget\n" +
                   "if (" + InstalledCheck() + ") { Write-Output 'SKIP:Já estava instalado'; return }\n" +
                   "& $w install --id " + Ps.Q(Id) + " -e --source " + Source + " --silent --accept-package-agreements --accept-source-agreements | Out-Null\n" +
                   "$code = $LASTEXITCODE\n" +
                   // 0 = ok; 0x8A150109/0x8A15010D = instalado, falta reiniciar
                   "if ($code -eq 0 -or $code -eq -1978334967 -or $code -eq -1978334963) {\n" +
                   "  Write-Output 'UNDO:1'\n" +
                   "  if ($code -eq 0) { Write-Output 'Instalado' } else { Write-Output 'Instalado (reinicie para concluir)' }\n" +
                   "  return\n" +
                   "}\n" +
                   // 0x8A150061 = já instalado; 0x8A15002B = sem atualização aplicável (já está na última)
                   "if ($code -eq -1978335135 -or $code -eq -1978335189) { Write-Output 'SKIP:Já estava instalado'; return }\n" +
                   "throw ('winget terminou com código ' + $code + ' (0x' + ('{0:X8}' -f $code) + ')')\n";
        }

        protected override string UndoScript()
        {
            if (IsStore && AppxName != null)
                return "if ({UNDO}) { Get-AppxPackage -Name " + Ps.Q(AppxName) + " | Remove-AppxPackage -ErrorAction Stop }\n";
            return "$ErrorActionPreference = 'Continue'\n" + WingetFn +
                   "if ({UNDO}) {\n" +
                   "  $w = Get-DbwWinget\n" +
                   "  & $w uninstall --id " + Ps.Q(Id) + " -e --silent --accept-source-agreements | Out-Null\n" +
                   "  if ($LASTEXITCODE -ne 0) { throw ('winget uninstall terminou com código ' + $LASTEXITCODE) }\n" +
                   "}\n";
        }

        protected override string UndoDescription() { return "Desinstalar " + Name; }

        public override string DetectScript()
        {
            if (IsStore && AppxName != null)
                return "if (@(Get-DbwAppx) -contains " + Ps.Q(AppxName) + ") { '1' } else { '0' }";
            // Get-DbwWingetIds: lista em cache de tudo o que o winget reconhece como instalado (ver Detector)
            return "$ids = Get-DbwWingetIds\n" +
                   "if ($null -eq $ids) { '?' } elseif ($ids -contains " + Ps.Q(Id) + ") { '1' } else { '0' }";
        }
    }
}
