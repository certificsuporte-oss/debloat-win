// Página de Configurações: tema, ponto de restauração, atualização automática, histórico e logs.
// Só preferências do próprio app (HKCU\Software\DebloatWin); nada muda no Windows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public class SettingsView : Panel
    {
        readonly MainForm owner;
        readonly FilterChip themeDark, themeLight, themeSystem;
        readonly ToggleSwitch restoreSwitch, updateSwitch;
        readonly FlatButton histBtn, logsBtn;
        readonly List<KeyValuePair<Rectangle, string>> cards = new List<KeyValuePair<Rectangle, string>>();
        bool loading;

        int Pad { get { return Theme.S(30); } }

        public SettingsView(MainForm owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Back;
            AutoScroll = true;

            themeDark = ThemeChip("Escuro", ThemeMode.Escuro);
            themeLight = ThemeChip("Claro", ThemeMode.Claro);
            themeSystem = ThemeChip("Seguir o Windows", ThemeMode.Sistema);

            restoreSwitch = new ToggleSwitch("Criar ponto de restauração antes de aplicar",
                "Recomendado: é o que permite voltar apps removidos, limpezas e instalações.");
            restoreSwitch.CheckedChanged += delegate { if (!loading) Theme.SetPref("PontoRestauracao", restoreSwitch.Checked); };
            Controls.Add(restoreSwitch);

            updateSwitch = new ToggleSwitch("Atualizar automaticamente ao abrir",
                "Procura versão nova no canal oficial e se atualiza sozinho. A linha de comando nunca se atualiza.");
            updateSwitch.CheckedChanged += delegate { if (!loading) Theme.SetPref("AtualizarAuto", updateSwitch.Checked); };
            Controls.Add(updateSwitch);

            histBtn = new FlatButton("Histórico e desfazer", ButtonKind.Secondary, "history");
            histBtn.Click += delegate { owner.ShowHistory(); };
            logsBtn = new FlatButton("Pasta de logs", ButtonKind.Secondary, "folder");
            logsBtn.Click += delegate { owner.OpenLogs(); };
            Controls.Add(histBtn);
            Controls.Add(logsBtn);

            Resize += delegate { LayoutAll(); };
            Reload();
        }

        FilterChip ThemeChip(string text, ThemeMode mode)
        {
            FilterChip c = new FilterChip(text, mode);
            c.Click += delegate { owner.SetTheme(mode); Reload(); };
            Controls.Add(c);
            return c;
        }

        public void Reload()
        {
            loading = true;
            themeDark.Selected = Theme.Mode == ThemeMode.Escuro;
            themeLight.Selected = Theme.Mode == ThemeMode.Claro;
            themeSystem.Selected = Theme.Mode == ThemeMode.Sistema;
            restoreSwitch.Checked = Theme.GetPref("PontoRestauracao", true);
            updateSwitch.Checked = AutoUpdate.Enabled;
            loading = false;
            LayoutAll();
            Invalidate();
        }

        public void ApplyColors()
        {
            BackColor = Theme.Back;
            LayoutAll();
        }

        void LayoutAll()
        {
            cards.Clear();
            int w = Math.Min(ClientSize.Width - Pad * 2, Theme.S(820));
            if (w < Theme.S(300)) return;
            int x = Pad + AutoScrollPosition.X, y = Theme.S(100) + AutoScrollPosition.Y;
            int inner = w - Theme.S(40);

            // Aparência
            Rectangle r = new Rectangle(x, y, w, Theme.S(118));
            cards.Add(new KeyValuePair<Rectangle, string>(r, "Aparência|O escuro é o tema principal do Debloat Win."));
            int cx = r.X + Theme.S(20), cy = r.Y + Theme.S(68);
            foreach (FilterChip c in new FilterChip[] { themeDark, themeLight, themeSystem })
            {
                c.Location = new Point(cx, cy);
                cx += c.Width + Theme.S(8);
            }
            y = r.Bottom + Theme.S(14);

            // Ao aplicar
            r = new Rectangle(x, y, w, Theme.S(130));
            cards.Add(new KeyValuePair<Rectangle, string>(r, "Ao aplicar|O Padrão e os itens marcados usam esta escolha."));
            restoreSwitch.SetBounds(r.X + Theme.S(20), r.Y + Theme.S(64), inner, restoreSwitch.Height);
            y = r.Bottom + Theme.S(14);

            // Atualização
            r = new Rectangle(x, y, w, Theme.S(130));
            cards.Add(new KeyValuePair<Rectangle, string>(r, "Atualização|Versão instalada: " + AppInfo.Version));
            updateSwitch.SetBounds(r.X + Theme.S(20), r.Y + Theme.S(64), inner, updateSwitch.Height);
            y = r.Bottom + Theme.S(14);

            // Histórico e dados
            r = new Rectangle(x, y, w, Theme.S(118));
            cards.Add(new KeyValuePair<Rectangle, string>(r, "Histórico e dados|Sessões, backups e logs ficam em " + Engine.DefaultDataDir()));
            histBtn.Location = new Point(r.X + Theme.S(20), r.Y + Theme.S(64));
            logsBtn.Location = new Point(histBtn.Right + Theme.S(10), r.Y + Theme.S(64));
            y = r.Bottom + Theme.S(14);

            // Sobre
            r = new Rectangle(x, y, w, Theme.S(92));
            cards.Add(new KeyValuePair<Rectangle, string>(r, "Sobre|" + AppInfo.Name + " " + AppInfo.Version +
                " · Windows 10 e 11 · github.com/certificsuporte-oss/debloat-win"));
            y = r.Bottom + Theme.S(30);

            AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y);
            foreach (Control c in new Control[] { restoreSwitch, updateSwitch, themeDark, themeLight, themeSystem }) c.BackColor = Theme.Card;
            histBtn.SurfaceColor = logsBtn.SurfaceColor = Theme.Card;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Back);
            TextRenderer.DrawText(g, "Configurações", Theme.Title, new Point(Pad - Theme.S(2) + AutoScrollPosition.X, Theme.S(24) + AutoScrollPosition.Y), Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Preferências do próprio programa. Nada aqui muda o Windows.", Theme.Body,
                new Point(Pad + AutoScrollPosition.X, Theme.S(62) + AutoScrollPosition.Y), Theme.Muted, TextFormatFlags.NoPadding);
            foreach (KeyValuePair<Rectangle, string> kv in cards)
            {
                Rectangle r = kv.Key;
                Theme.FillRounded(g, r, Theme.S(12), Theme.Card, Theme.Border);
                string[] parts = kv.Value.Split('|');
                TextRenderer.DrawText(g, parts[0], Theme.Subtitle, new Point(r.X + Theme.S(20), r.Y + Theme.S(16)), Theme.Text, TextFormatFlags.NoPadding);
                if (parts.Length > 1 && parts[1].Length > 0)
                    TextRenderer.DrawText(g, parts[1], Theme.Small, new Rectangle(r.X + Theme.S(20), r.Y + Theme.S(42), r.Width - Theme.S(40), Theme.S(18)),
                        Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            LayoutAll();
        }
    }
}
