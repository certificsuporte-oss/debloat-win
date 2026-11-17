// Atualização automática ao abrir o programa.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public static class AutoUpdate
    {
        const string PrefKey = @"Software\" + AppInfo.DataFolderName;

        /// <summary>Desligável em HKCU\Software\DebloatWin, valor AtualizarAuto = 0.</summary>
        public static bool Enabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(PrefKey))
                    {
                        object v = k == null ? null : k.GetValue("AtualizarAuto");
                        return !(v is int && (int)v == 0);
                    }
                }
                catch { return true; }
            }
        }

        public static void Log(string line)
        {
            try
            {
                string dir = Path.Combine(Engine.DefaultDataDir(), "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "atualizacao.log"),
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "  " + line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>Apaga o .old da atualização anterior (o processo antigo pode levar um instante para fechar).</summary>
        public static void CleanupAfterUpdate(string exePath)
        {
            for (int i = 0; i < 10; i++)
            {
                Updater.CleanupOld(exePath);
                if (!File.Exists(Updater.OldPath(exePath))) return;
                Thread.Sleep(300);
            }
        }

        /// <summary>
        /// Verifica e aplica atualização. Devolve true se a versão nova foi iniciada
        /// (o processo atual deve encerrar). Qualquer falha é registrada e o programa abre normalmente.
        /// </summary>
        public static bool Run(string exePath, out string failure)
        {
            failure = null;
            UpdateInfo info;
            try
            {
                info = Updater.Check(3500);
            }
            catch (Exception ex)
            {
                Log("Verificação falhou (sem internet?): " + ex.Message);
                return false;
            }
            if (info == null) return false;

            Log("Versão nova encontrada: " + info.Version + " (atual " + AppInfo.Version + ")");
            using (UpdateForm f = new UpdateForm(info, exePath))
            {
                f.ShowDialog();
                if (f.Error != null)
                {
                    failure = "Não foi possível atualizar para a versão " + info.Version + ": " + f.Error;
                    Log(failure);
                    return false;
                }
            }

            try
            {
                Process.Start(exePath, "/atualizado " + AppInfo.Version);
                Log("Atualizado para " + info.Version + "; reiniciando.");
                return true;
            }
            catch (Exception ex)
            {
                failure = "Atualizado, mas não foi possível reabrir: " + ex.Message;
                Log(failure);
                return false;
            }
        }
    }

    /// <summary>Janelinha de progresso do download da atualização.</summary>
    public class UpdateForm : ThemedForm
    {
        readonly UpdateInfo info;
        readonly string exePath;
        readonly Label status;
        readonly ProgressBar bar;
        public string Error;

        public UpdateForm(UpdateInfo info, string exePath)
        {
            this.info = info;
            this.exePath = exePath;
            Text = AppInfo.Name;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(Theme.S(440), Theme.S(130));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label title = new Label();
            title.Text = "Atualizando para a versão " + info.Version;
            title.Font = Theme.Subtitle;
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.SetBounds(Theme.S(20), Theme.S(16), Theme.S(400), Theme.S(26));
            Controls.Add(title);

            status = new Label();
            status.Text = "Baixando…";
            status.ForeColor = Theme.Muted;
            status.AutoSize = false;
            status.SetBounds(Theme.S(20), Theme.S(46), Theme.S(400), Theme.S(22));
            Controls.Add(status);

            bar = new ProgressBar();
            bar.Style = ProgressBarStyle.Continuous;
            bar.SetBounds(Theme.S(20), Theme.S(80), Theme.S(400), Theme.S(18));
            Controls.Add(bar);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                string tmp = Updater.NewPath(exePath);
                try
                {
                    Updater.Download(info, tmp, 60000, delegate(long got, long total)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (total > 0) { bar.Maximum = 1000; bar.Value = (int)Math.Min(1000, got * 1000 / total); }
                            status.Text = string.Format("Baixando… {0:N0} KB", got / 1024);
                        });
                    });
                    BeginInvoke((MethodInvoker)delegate { status.Text = "Conferindo o arquivo…"; });
                    err = Updater.Verify(tmp, info);
                    if (err == null)
                    {
                        BeginInvoke((MethodInvoker)delegate { status.Text = "Instalando…"; });
                        Updater.Swap(exePath, tmp);
                    }
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
                if (err != null)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
                BeginInvoke((MethodInvoker)delegate { Error = err; Close(); });
            });
        }
    }
}
