// Ícones vetoriais desenhados à mão (traço fino, grade de 24×24, estilo Lucide/Fluent).
// Não dependem de fonte de ícones: ficam iguais no Windows 10 e no 11, em qualquer DPI.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DebloatWin.App
{
    public static class Icons
    {
        /// <summary>Desenha o ícone <paramref name="name"/> dentro de <paramref name="r"/>. Traço em unidades da grade (2 = normal).</summary>
        public static void Draw(Graphics g, string name, Rectangle r, Color color, float stroke)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = Math.Min(r.Width, r.Height) / 24f;
            g.TranslateTransform(r.X + (r.Width - 24f * k) / 2f, r.Y + (r.Height - 24f * k) / 2f);
            g.ScaleTransform(k, k);
            using (Pen p = new Pen(color, stroke))
            using (SolidBrush b = new SolidBrush(color))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                Paint(g, name, p, b);
            }
            g.Restore(st);
        }

        public static void Draw(Graphics g, string name, Rectangle r, Color color)
        {
            Draw(g, name, r, color, 2f);
        }

        static void Line(Graphics g, Pen p, float x1, float y1, float x2, float y2) { g.DrawLine(p, x1, y1, x2, y2); }
        static void Poly(Graphics g, Pen p, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            g.DrawLines(p, pts);
        }
        static void Closed(Graphics g, Pen p, params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            g.DrawPolygon(p, pts);
        }
        static void RRect(Graphics g, Pen p, float x, float y, float w, float h, float rad)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                float d = rad * 2;
                gp.AddArc(x, y, d, d, 180, 90);
                gp.AddArc(x + w - d, y, d, d, 270, 90);
                gp.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                gp.AddArc(x, y + h - d, d, d, 90, 90);
                gp.CloseFigure();
                g.DrawPath(p, gp);
            }
        }
        static void Circle(Graphics g, Pen p, float cx, float cy, float rad) { g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2); }
        static void Dot(Graphics g, Brush b, float cx, float cy, float rad) { g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2); }

        static GraphicsPath ShieldPath()
        {
            GraphicsPath gp = new GraphicsPath();
            gp.AddLine(12f, 3f, 20f, 6f);
            gp.AddLine(20f, 6f, 20f, 12f);
            gp.AddBezier(20f, 12f, 20f, 16.5f, 16.6f, 20.3f, 12f, 21f);
            gp.AddBezier(12f, 21f, 7.4f, 20.3f, 4f, 16.5f, 4f, 12f);
            gp.AddLine(4f, 12f, 4f, 6f);
            gp.CloseFigure();
            return gp;
        }

        static void Paint(Graphics g, string name, Pen p, Brush b)
        {
            switch (name)
            {
                case "dashboard":
                    RRect(g, p, 3, 3, 7, 9, 1.5f); RRect(g, p, 14, 3, 7, 5, 1.5f);
                    RRect(g, p, 14, 12, 7, 9, 1.5f); RRect(g, p, 3, 16, 7, 5, 1.5f);
                    break;
                case "debloat":
                    Line(g, p, 3, 6, 21, 6);
                    Poly(g, p, 8, 6, 8, 4, 16, 4, 16, 6);
                    Poly(g, p, 5, 6, 6, 20, 18, 20, 19, 6);
                    Line(g, p, 10, 11, 10, 16.5f); Line(g, p, 14, 11, 14, 16.5f);
                    break;
                case "apps":
                    Closed(g, p, 12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f);
                    Line(g, p, 12, 12, 20, 7.5f); Line(g, p, 12, 12, 12, 21); Line(g, p, 12, 12, 4, 7.5f);
                    break;
                case "services":
                    RRect(g, p, 3, 4, 18, 6, 1.5f); RRect(g, p, 3, 14, 18, 6, 1.5f);
                    Dot(g, b, 7, 7, 1.1f); Dot(g, b, 7, 17, 1.1f);
                    Line(g, p, 11, 7, 17, 7); Line(g, p, 11, 17, 17, 17);
                    break;
                case "clean":
                    Line(g, p, 20, 3, 12.5f, 10.5f);
                    Closed(g, p, 12.5f, 10.5f, 15, 13, 12, 20.5f, 3.5f, 20.5f, 5, 13);
                    Line(g, p, 7.5f, 20.5f, 8.5f, 16.5f); Line(g, p, 10.5f, 20.5f, 11.2f, 17.5f);
                    break;
                case "optimize":
                    g.DrawArc(p, 3, 5, 18, 18, 180, 180);
                    Line(g, p, 12, 14, 16.5f, 9.5f);
                    Dot(g, b, 12, 14, 1.4f);
                    Line(g, p, 3, 14, 5, 14); Line(g, p, 19, 14, 21, 14);
                    break;
                case "tools":
                    g.DrawArc(p, 12, 3, 9, 9, 80, 290);
                    Line(g, p, 13.4f, 10.6f, 4, 20);
                    Line(g, p, 16.5f, 7.5f, 20.5f, 3.5f);
                    break;
                case "settings":
                    Circle(g, p, 12, 12, 3);
                    Circle(g, p, 12, 12, 7);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI / 4 * i;
                        Line(g, p, 12 + (float)Math.Cos(a) * 7, 12 + (float)Math.Sin(a) * 7, 12 + (float)Math.Cos(a) * 9.5f, 12 + (float)Math.Sin(a) * 9.5f);
                    }
                    break;
                case "check":
                    Poly(g, p, 5, 12.5f, 9.5f, 17, 19, 7);
                    break;
                case "x":
                    Line(g, p, 6, 6, 18, 18); Line(g, p, 18, 6, 6, 18);
                    break;
                case "minus":
                    Line(g, p, 6, 12, 18, 12);
                    break;
                case "clock":
                    Circle(g, p, 12, 12, 9); Poly(g, p, 12, 7, 12, 12, 15, 14);
                    break;
                case "alert":
                    Closed(g, p, 12, 3.5f, 21.5f, 20, 2.5f, 20);
                    Line(g, p, 12, 10, 12, 14); Dot(g, b, 12, 17, 1.2f);
                    break;
                case "info":
                    Circle(g, p, 12, 12, 9); Line(g, p, 12, 11, 12, 16.5f); Dot(g, b, 12, 7.8f, 1.2f);
                    break;
                case "shield":
                    using (GraphicsPath gp = ShieldPath()) g.DrawPath(p, gp);
                    Poly(g, p, 9, 12, 11, 14, 15, 10);
                    break;
                case "logo":
                    using (GraphicsPath gp = ShieldPath()) g.DrawPath(p, gp);
                    Poly(g, p, 8.5f, 12.5f, 11, 15, 15.5f, 10);
                    break;
                case "search":
                    Circle(g, p, 11, 11, 7); Line(g, p, 16.2f, 16.2f, 20.5f, 20.5f);
                    break;
                case "play":
                    Closed(g, p, 7, 4.5f, 19.5f, 12, 7, 19.5f);
                    break;
                case "history":
                    g.DrawArc(p, 3, 3, 18, 18, 200, 300);
                    Poly(g, p, 3, 4, 3.5f, 9, 8.5f, 8);
                    Poly(g, p, 12, 7.5f, 12, 12, 15, 14);
                    break;
                case "undo":
                    Poly(g, p, 8, 5, 4, 9, 8, 13);
                    g.DrawArc(p, 4, 9, 16, 11, 270, 180);
                    Line(g, p, 4, 9, 12, 9); Line(g, p, 12, 20, 9, 20);
                    break;
                case "cpu":
                    RRect(g, p, 6, 6, 12, 12, 2); RRect(g, p, 9.5f, 9.5f, 5, 5, 0.8f);
                    Line(g, p, 9.5f, 2, 9.5f, 6); Line(g, p, 14.5f, 2, 14.5f, 6); Line(g, p, 9.5f, 18, 9.5f, 22); Line(g, p, 14.5f, 18, 14.5f, 22);
                    Line(g, p, 2, 9.5f, 6, 9.5f); Line(g, p, 2, 14.5f, 6, 14.5f); Line(g, p, 18, 9.5f, 22, 9.5f); Line(g, p, 18, 14.5f, 22, 14.5f);
                    break;
                case "memory":
                    RRect(g, p, 2.5f, 7, 19, 9, 1.5f);
                    Line(g, p, 6.5f, 16, 6.5f, 19.5f); Line(g, p, 10.5f, 16, 10.5f, 19.5f); Line(g, p, 14.5f, 16, 14.5f, 19.5f); Line(g, p, 18.5f, 16, 18.5f, 19.5f);
                    Line(g, p, 7, 10.5f, 7, 12.5f); Line(g, p, 12, 10.5f, 12, 12.5f); Line(g, p, 17, 10.5f, 17, 12.5f);
                    break;
                case "drive":
                    RRect(g, p, 3, 13, 18, 7, 2);
                    Poly(g, p, 4.5f, 13, 7, 5, 17, 5, 19.5f, 13);
                    Dot(g, b, 7, 16.5f, 1.1f);
                    break;
                case "windows":
                    g.FillPolygon(b, new PointF[] { new PointF(3, 5.6f), new PointF(10.5f, 4.6f), new PointF(10.5f, 11.4f), new PointF(3, 11.4f) });
                    g.FillPolygon(b, new PointF[] { new PointF(12, 4.4f), new PointF(21, 3), new PointF(21, 11.4f), new PointF(12, 11.4f) });
                    g.FillPolygon(b, new PointF[] { new PointF(3, 12.8f), new PointF(10.5f, 12.8f), new PointF(10.5f, 19.6f), new PointF(3, 18.6f) });
                    g.FillPolygon(b, new PointF[] { new PointF(12, 12.8f), new PointF(21, 12.8f), new PointF(21, 21), new PointF(12, 19.8f) });
                    break;
                case "download":
                    Line(g, p, 12, 3, 12, 15); Poly(g, p, 7, 10, 12, 15, 17, 10); Line(g, p, 4, 20.5f, 20, 20.5f);
                    break;
                case "file":
                    Poly(g, p, 14, 3, 6, 3, 4.5f, 4.5f, 4.5f, 19.5f, 6, 21, 18, 21, 19.5f, 19.5f, 19.5f, 8.5f, 14, 3);
                    Poly(g, p, 14, 3, 14, 8.5f, 19.5f, 8.5f);
                    Line(g, p, 8.5f, 13, 15.5f, 13); Line(g, p, 8.5f, 17, 13, 17);
                    break;
                case "folder":
                    Poly(g, p, 3, 7, 3, 18.5f, 4.5f, 20, 19.5f, 20, 21, 18.5f, 21, 9.5f, 19.5f, 8, 12, 8, 10, 5, 4.5f, 5, 3, 6.5f, 3, 7);
                    break;
                case "sun":
                    Circle(g, p, 12, 12, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI / 4 * i;
                        Line(g, p, 12 + (float)Math.Cos(a) * 7, 12 + (float)Math.Sin(a) * 7, 12 + (float)Math.Cos(a) * 9.5f, 12 + (float)Math.Sin(a) * 9.5f);
                    }
                    break;
                case "moon":
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddArc(3, 3, 18, 18, 300, 290);
                        gp.AddArc(9, 2.5f, 13, 13, 165, -125);
                        g.DrawPath(p, gp);
                    }
                    break;
                case "refresh":
                    g.DrawArc(p, 4, 4, 16, 16, 300, 290);
                    Poly(g, p, 20, 3.5f, 20, 8.5f, 15, 8.5f);
                    break;
                case "chevron":
                    Poly(g, p, 9, 6, 15, 12, 9, 18);
                    break;
                case "export":
                    Poly(g, p, 8, 21, 6, 21, 4.5f, 19.5f, 4.5f, 4.5f, 6, 3, 18, 3, 19.5f, 4.5f, 19.5f, 9);
                    Line(g, p, 13, 16, 21, 16); Poly(g, p, 18, 13, 21, 16, 18, 19);
                    break;
                case "restart":
                    g.DrawArc(p, 4, 4, 16, 16, 120, 320);
                    Poly(g, p, 4.2f, 15.5f, 5.5f, 20.5f, 10.2f, 18.6f);
                    break;
                default:
                    Circle(g, p, 12, 12, 8);
                    break;
            }
        }
    }
}
