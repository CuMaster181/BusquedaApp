using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace BusquedaApp
{
    public class SerieBarras
    {
        public string Nombre;
        public double[] Valores;
        public Color Color;
    }

    /// <summary>Gráfica de barras agrupadas (GDI+), con escala lineal o logarítmica.</summary>
    public class GraficaBarras : Control
    {
        public string Titulo { get; set; }
        public string EjeY { get; set; }
        public List<string> Categorias { get; set; }
        public List<SerieBarras> Series { get; set; }
        public bool Logaritmica { get; set; }

        public GraficaBarras()
        {
            Titulo = "";
            EjeY = "Valor";
            Categorias = new List<string>();
            Series = new List<SerieBarras>();
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Dibujar(e.Graphics, Width, Height);
        }

        private static readonly Color[] Paleta = new Color[]
        {
            Color.FromArgb(214, 39, 40),   // 0 secuencial
            Color.FromArgb(31, 119, 180),  // 1 binaria
            Color.FromArgb(44, 160, 44),   Color.FromArgb(98, 200, 98),   Color.FromArgb(0, 100, 0),     Color.FromArgb(170, 220, 120),
            Color.FromArgb(255, 127, 14),  Color.FromArgb(255, 180, 90),  Color.FromArgb(180, 90, 0),    Color.FromArgb(255, 215, 150),
            Color.FromArgb(148, 103, 189), Color.FromArgb(190, 160, 220), Color.FromArgb(90, 50, 140),   Color.FromArgb(220, 200, 240)
        };

        public static Color ColorDe(int orden)
        {
            return Paleta[Math.Abs(orden) % Paleta.Length];
        }

        public void Dibujar(Graphics g, int ancho, int alto)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);

            using (var fTitulo = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var fNormal = new Font("Segoe UI", 9f))
            using (var fLeyenda = new Font("Segoe UI", 8f))
            using (var negro = new SolidBrush(Color.Black))
            using (var plumaEje = new Pen(Color.Black, 1.5f))
            using (var plumaGrid = new Pen(Color.Gainsboro, 1f))
            using (var sfDer = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (var sfCentro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Titulo ?? "", fTitulo, negro, 12, 8);

                Rectangle area = new Rectangle(90, 50, ancho - 90 - 230, alto - 50 - 75);
                if (area.Width < 60 || area.Height < 60) return;

                List<double> valores = Series.SelectMany(s => s.Valores).Where(v => !double.IsNaN(v) && v > 0).ToList();
                if (Categorias.Count == 0 || Series.Count == 0 || valores.Count == 0)
                {
                    g.DrawString("Sin datos. Ejecute el experimento para ver la gráfica.", fNormal, negro, area.X + 10, area.Y + 10);
                    return;
                }

                double max = valores.Max();
                double min = valores.Min();
                var marcas = new List<double>();
                Func<double, float> mapY;

                if (Logaritmica)
                {
                    int eMin = (int)Math.Floor(Math.Log10(min)) - 1;
                    int eMax = (int)Math.Ceiling(Math.Log10(max));
                    if (eMax <= eMin) eMax = eMin + 1;
                    for (int e = eMin; e <= eMax; e++) marcas.Add(Math.Pow(10, e));
                    double piso = Math.Pow(10, eMin);
                    mapY = v => area.Bottom - (float)((Math.Log10(Math.Max(v, piso)) - eMin) / (eMax - eMin) * area.Height);
                }
                else
                {
                    double tope = NiceMax(max);
                    for (int i = 0; i <= 5; i++) marcas.Add(tope * i / 5.0);
                    mapY = v => area.Bottom - (float)(v / tope * area.Height);
                }

                foreach (double m in marcas)
                {
                    float y = mapY(m);
                    g.DrawLine(plumaGrid, area.Left, y, area.Right, y);
                    g.DrawString(FormatoMarca(m), fNormal, negro, new RectangleF(0, y - 10, area.Left - 6, 20), sfDer);
                }

                int nc = Categorias.Count;
                int ns = Series.Count;
                float anchoGrupo = (float)area.Width / nc;
                float anchoBarra = anchoGrupo * 0.85f / ns;

                for (int c = 0; c < nc; c++)
                {
                    float x0 = area.Left + c * anchoGrupo + anchoGrupo * 0.075f;
                    for (int s = 0; s < ns; s++)
                    {
                        if (c >= Series[s].Valores.Length) continue;
                        double v = Series[s].Valores[c];
                        if (double.IsNaN(v)) continue;

                        float y = mapY(v);
                        float alturaBarra = Math.Max(area.Bottom - y, 1f);
                        using (var pincel = new SolidBrush(Series[s].Color))
                        {
                            g.FillRectangle(pincel, x0 + s * anchoBarra, area.Bottom - alturaBarra, Math.Max(anchoBarra - 1, 1f), alturaBarra);
                        }
                    }
                    g.DrawString(Categorias[c], fNormal, negro,
                        new RectangleF(area.Left + c * anchoGrupo, area.Bottom + 4, anchoGrupo, 20), sfCentro);
                }

                g.DrawLine(plumaEje, area.Left, area.Top, area.Left, area.Bottom);
                g.DrawLine(plumaEje, area.Left, area.Bottom, area.Right, area.Bottom);

                g.DrawString("Tamaño del conjunto (N datos)", fNormal, negro,
                    new RectangleF(area.Left, area.Bottom + 28, area.Width, 20), sfCentro);

                g.TranslateTransform(18, area.Top + area.Height / 2f);
                g.RotateTransform(-90);
                g.DrawString((EjeY ?? "Valor") + (Logaritmica ? " (escala log)" : ""), fNormal, negro, 0, 0, sfCentro);
                g.ResetTransform();

                float ly = area.Top;
                foreach (SerieBarras s in Series)
                {
                    using (var pincel = new SolidBrush(s.Color))
                        g.FillRectangle(pincel, area.Right + 18, ly, 13, 13);
                    g.DrawString(s.Nombre, fLeyenda, negro, area.Right + 36, ly - 1);
                    ly += 19;
                }
            }
        }

        private static double NiceMax(double v)
        {
            double exp = Math.Floor(Math.Log10(v));
            double f = v / Math.Pow(10, exp);
            double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
            return nice * Math.Pow(10, exp);
        }

        private static string FormatoMarca(double v)
        {
            if (v >= 1000) return v.ToString("N0");
            if (v >= 1) return v.ToString("0.##");
            return v.ToString("0.####");
        }
    }
}
