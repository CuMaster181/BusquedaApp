using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BusquedaApp
{
    public class MainForm : Form
    {
        private readonly BindingList<ResultadoBusqueda> _resultados = new BindingList<ResultadoBusqueda>();
        private CancellationTokenSource _cts;

        private static readonly string[] Metricas =
        {
            "Comparaciones promedio", "Comparaciones máximas", "Iteraciones promedio",
            "Iteraciones máximas", "Tiempo total de consultas (ms)", "Colisiones al insertar (solo hash)"
        };

        // Configuración
        private CheckBox chk1k, chk10k, chk100k, chk1M, chkCustom, chkSec, chkBin;
        private NumericUpDown nudCustom, nudConsultas, nudSemilla, nudAlfa;
        private CheckedListBox clbFunciones, clbColisiones;
        private Button btnEjecutar, btnCancelar, btnCsv, btnPng;

        // Salida
        private ProgressBar barra;
        private Label lblEstado;
        private TextBox txtLog;
        private DataGridView dgvResultados, dgvResumen;
        private ComboBox cboBusqueda, cboMetrica, cboFiltro;
        private CheckBox chkLog;
        private GraficaBarras grafica;

        // Demostración
        private NumericUpDown nudDemoN, nudClaveDemo;
        private ComboBox cboFDemo, cboCDemo;
        private TextBox txtDemo;
        private int[] _demoClaves, _demoOrden;
        private TablaHash _demoTabla;

        public MainForm()
        {
            Text = "Métodos de búsqueda (Unidad 6) - Mario Alejandro Martinez Alonso - I25050368";
            Size = new Size(1300, 840);
            MinimumSize = new Size(1050, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            ConstruirInterfaz();
            FormClosing += (s, e) => { if (_cts != null) _cts.Cancel(); };
        }

        // ==================================================================
        //  INTERFAZ
        // ==================================================================
        private void ConstruirInterfaz()
        {
            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(raiz);

            var panelSuperior = new Panel { Dock = DockStyle.Top, AutoSize = true };
            var lblAlumno = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "  Alumno: Mario Alejandro Martinez Alonso   |   Matrícula: I25050368   |   Docente: Rubén Miguel Riojas",
                BackColor = Color.FromArgb(230, 238, 247)
            };

            var flujo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4) };

            chk1k = Chequeo("1,000", true);
            chk10k = Chequeo("10,000", true);
            chk100k = Chequeo("100,000", true);
            chk1M = Chequeo("1,000,000", false);
            chkCustom = Chequeo("Otro:", false);
            nudCustom = Numerico(0, 5000000, 0, 1000, 90);
            flujo.Controls.Add(Grupo("Tamaños (N datos)", chk1k, chk10k, chk100k, chk1M, FilaHorizontal(chkCustom, nudCustom)));

            chkSec = Chequeo("Búsqueda secuencial", true);
            chkBin = Chequeo("Búsqueda binaria", true);
            flujo.Controls.Add(Grupo("Técnicas de búsqueda", chkSec, chkBin,
                new Label { Text = "Tabla hash (ver listas →)", AutoSize = true, ForeColor = Color.DimGray }));

            clbFunciones = new CheckedListBox { CheckOnClick = true, Width = 150, Height = 70, IntegralHeight = false };
            foreach (FuncionHash f in Enum.GetValues(typeof(FuncionHash))) clbFunciones.Items.Add(Nombres.Funcion(f), true);
            flujo.Controls.Add(Grupo("Función de dispersión", clbFunciones));

            clbColisiones = new CheckedListBox { CheckOnClick = true, Width = 160, Height = 86, IntegralHeight = false };
            foreach (Colision c in Enum.GetValues(typeof(Colision))) clbColisiones.Items.Add(Nombres.Metodo(c), true);
            flujo.Controls.Add(Grupo("Resolución de colisiones", clbColisiones));

            nudConsultas = Numerico(10, 100000, 1000, 100, 90);
            nudSemilla = Numerico(0, 100000000, 2026, 1, 90);
            nudAlfa = new NumericUpDown { Minimum = 0.10m, Maximum = 2.00m, Value = 0.50m, Increment = 0.05m, DecimalPlaces = 2, Width = 90 };
            flujo.Controls.Add(Grupo("Parámetros",
                FilaEtiquetada("Búsquedas por tipo:", nudConsultas),
                FilaEtiquetada("Semilla:", nudSemilla),
                FilaEtiquetada("Factor de carga α:", nudAlfa),
                new Label { Text = "Abierto: α < 1; cuadrático: usar α ≤ 0.5", AutoSize = true, ForeColor = Color.DimGray }));

            btnEjecutar = Boton("▶ Ejecutar experimento", BtnEjecutar_Click);
            btnCancelar = Boton("■ Cancelar", (s, e) => { if (_cts != null) _cts.Cancel(); });
            btnCancelar.Enabled = false;
            btnCsv = Boton("Exportar CSV", BtnCsv_Click);
            btnPng = Boton("Exportar gráficas PNG", BtnPng_Click);
            flujo.Controls.Add(Grupo("Acciones", btnEjecutar, btnCancelar, btnCsv, btnPng));

            panelSuperior.Controls.Add(flujo);
            panelSuperior.Controls.Add(lblAlumno);
            raiz.Controls.Add(panelSuperior, 0, 0);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // ---- Resultados ----
            var tabRes = new TabPage("Resultados");
            dgvResultados = CrearGrid();
            dgvResultados.AutoGenerateColumns = false;
            dgvResultados.Columns.Add(Columna("Técnica", "Tecnica", 230));
            dgvResultados.Columns.Add(Columna("Búsqueda", "Busqueda", 80));
            dgvResultados.Columns.Add(Columna("N", "N", 80, "N0"));
            dgvResultados.Columns.Add(Columna("Comp. prom.", "PromComp", 90, "N2"));
            dgvResultados.Columns.Add(Columna("Comp. máx.", "MaxComp", 85, "N0"));
            dgvResultados.Columns.Add(Columna("Iter. prom.", "PromIter", 90, "N2"));
            dgvResultados.Columns.Add(Columna("Iter. máx.", "MaxIter", 80, "N0"));
            dgvResultados.Columns.Add(Columna("Colisiones", "Colisiones", 85, "N0"));
            dgvResultados.Columns.Add(Columna("Máx. long.", "MaxLongitud", 80, "N0"));
            dgvResultados.Columns.Add(Columna("Carga", "FactorCarga", 65, "N3"));
            dgvResultados.Columns.Add(Columna("Tam. tabla", "TamTabla", 85, "N0"));
            dgvResultados.Columns.Add(Columna("Tiempo (ms)", "TiempoMs", 90, "N3"));
            dgvResultados.Columns.Add(Columna("¿Correcto?", "Correcto", 75));
            dgvResultados.DataSource = _resultados;
            tabRes.Controls.Add(dgvResultados);
            tabs.TabPages.Add(tabRes);

            // ---- Gráficas y resumen ----
            var tabGraf = new TabPage("Gráficas y resumen");
            var tablaGraf = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            tablaGraf.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            tablaGraf.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tablaGraf.RowStyles.Add(new RowStyle(SizeType.Absolute, 200f));

            var filtros = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4, 6, 4, 0) };
            cboBusqueda = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            cboBusqueda.Items.AddRange(new object[] { "Exitosa", "Fallida" });
            cboBusqueda.SelectedIndex = 0;
            cboMetrica = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
            cboMetrica.Items.AddRange(Metricas);
            cboMetrica.SelectedIndex = 0;
            cboFiltro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
            cboFiltro.Items.AddRange(new object[] { "Todas las técnicas", "Sec. + Bin. + Hash División", "Sec. + Bin. + Hash Multiplicación", "Sec. + Bin. + Hash Cuadrado medio" });
            cboFiltro.SelectedIndex = 0;
            chkLog = new CheckBox { Text = "Escala logarítmica", AutoSize = true, Checked = true, Margin = new Padding(10, 3, 0, 0) };
            filtros.Controls.Add(new Label { Text = "Búsqueda:", AutoSize = true, Margin = new Padding(0, 6, 3, 0) });
            filtros.Controls.Add(cboBusqueda);
            filtros.Controls.Add(new Label { Text = "Métrica:", AutoSize = true, Margin = new Padding(10, 6, 3, 0) });
            filtros.Controls.Add(cboMetrica);
            filtros.Controls.Add(new Label { Text = "Mostrar:", AutoSize = true, Margin = new Padding(10, 6, 3, 0) });
            filtros.Controls.Add(cboFiltro);
            filtros.Controls.Add(chkLog);
            cboBusqueda.SelectedIndexChanged += (s, e) => RefrescarGrafica();
            cboMetrica.SelectedIndexChanged += (s, e) => RefrescarGrafica();
            cboFiltro.SelectedIndexChanged += (s, e) => RefrescarGrafica();
            chkLog.CheckedChanged += (s, e) => RefrescarGrafica();

            grafica = new GraficaBarras { Dock = DockStyle.Fill };
            dgvResumen = CrearGrid();

            tablaGraf.Controls.Add(filtros, 0, 0);
            tablaGraf.Controls.Add(grafica, 0, 1);
            tablaGraf.Controls.Add(dgvResumen, 0, 2);
            tabGraf.Controls.Add(tablaGraf);
            tabs.TabPages.Add(tabGraf);

            // ---- Demostración ----
            tabs.TabPages.Add(ConstruirDemo());

            // ---- Bitácora ----
            var tabLog = new TabPage("Bitácora");
            txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f) };
            tabLog.Controls.Add(txtLog);
            tabs.TabPages.Add(tabLog);

            raiz.Controls.Add(tabs, 0, 1);

            var panelEstado = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
            panelEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
            panelEstado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            barra = new ProgressBar { Dock = DockStyle.Fill, Height = 22, Margin = new Padding(6) };
            lblEstado = new Label { Dock = DockStyle.Fill, AutoSize = false, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Text = "Listo." };
            panelEstado.Controls.Add(barra, 0, 0);
            panelEstado.Controls.Add(lblEstado, 1, 0);
            raiz.Controls.Add(panelEstado, 0, 2);
        }

        private TabPage ConstruirDemo()
        {
            var tab = new TabPage("Demostración");
            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var fila = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4, 6, 4, 0) };
            nudDemoN = Numerico(5, 40, 15, 1, 60);
            cboFDemo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            foreach (FuncionHash f in Enum.GetValues(typeof(FuncionHash))) cboFDemo.Items.Add(Nombres.Funcion(f));
            cboFDemo.SelectedIndex = 0;
            cboCDemo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            foreach (Colision c in Enum.GetValues(typeof(Colision))) cboCDemo.Items.Add(Nombres.Metodo(c));
            cboCDemo.SelectedIndex = 0;
            var btnGen = new Button { Text = "Generar datos", Width = 110, Height = 26 };
            btnGen.Click += BtnGenerarDemo_Click;
            nudClaveDemo = Numerico(0, int.MaxValue, 0, 1, 110);
            var btnBuscar = new Button { Text = "Buscar clave", Width = 100, Height = 26 };
            btnBuscar.Click += BtnBuscarDemo_Click;

            fila.Controls.Add(new Label { Text = "N:", AutoSize = true, Margin = new Padding(0, 6, 3, 0) });
            fila.Controls.Add(nudDemoN);
            fila.Controls.Add(new Label { Text = "Función:", AutoSize = true, Margin = new Padding(10, 6, 3, 0) });
            fila.Controls.Add(cboFDemo);
            fila.Controls.Add(new Label { Text = "Colisiones:", AutoSize = true, Margin = new Padding(10, 6, 3, 0) });
            fila.Controls.Add(cboCDemo);
            fila.Controls.Add(btnGen);
            fila.Controls.Add(new Label { Text = "Clave a buscar:", AutoSize = true, Margin = new Padding(20, 6, 3, 0) });
            fila.Controls.Add(nudClaveDemo);
            fila.Controls.Add(btnBuscar);

            txtDemo = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f) };
            txtDemo.Text = "Pulsa \"Generar datos\" para crear un conjunto pequeño (claves de 0 a 99,999), ver el arreglo, el arreglo ordenado y la tabla hash,\r\n" +
                           "y después busca una clave para ver las comparaciones e iteraciones de cada técnica.";

            raiz.Controls.Add(fila, 0, 0);
            raiz.Controls.Add(txtDemo, 0, 1);
            tab.Controls.Add(raiz);
            return tab;
        }

        private void BtnGenerarDemo_Click(object sender, EventArgs e)
        {
            int n = (int)nudDemoN.Value;
            var rnd = new Random();
            var conjunto = new HashSet<int>();
            var lista = new List<int>();
            while (lista.Count < n)
            {
                int k = rnd.Next(0, 100000);
                if (conjunto.Add(k)) lista.Add(k);
            }
            _demoClaves = lista.ToArray();
            _demoOrden = (int[])_demoClaves.Clone();
            Array.Sort(_demoOrden);

            FuncionHash f = (FuncionHash)cboFDemo.SelectedIndex;
            Colision c = (Colision)cboCDemo.SelectedIndex;
            _demoTabla = TablaHash.Crear(n, (double)nudAlfa.Value, f, c);
            foreach (int k in _demoClaves) _demoTabla.Insertar(k);

            var sb = new StringBuilder();
            sb.AppendLine("Arreglo original (para búsqueda secuencial):");
            sb.AppendLine("  " + string.Join(", ", _demoClaves));
            sb.AppendLine();
            sb.AppendLine("Arreglo ordenado (para búsqueda binaria):");
            sb.AppendLine("  " + string.Join(", ", _demoOrden));
            sb.AppendLine();
            sb.AppendLine("Tabla hash: función = " + Nombres.Funcion(f) + ", colisiones = " + Nombres.Metodo(c) +
                          ", m = " + _demoTabla.M + ", colisiones al insertar = " + _demoTabla.ColisionesInsercion +
                          ", factor de carga = " + _demoTabla.FactorCarga.ToString("0.000"));
            sb.AppendLine(_demoTabla.Volcar());
            txtDemo.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private void BtnBuscarDemo_Click(object sender, EventArgs e)
        {
            if (_demoClaves == null)
            {
                MessageBox.Show("Primero genera los datos.", "Demostración", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int clave = (int)nudClaveDemo.Value;
            long c1, i1, c2, i2, c3, i3;
            int p1 = Busqueda.Secuencial(_demoClaves, clave, out c1, out i1);
            int p2 = Busqueda.Binaria(_demoOrden, clave, out c2, out i2);
            int p3 = _demoTabla.Buscar(clave, out c3, out i3);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== Búsqueda de la clave " + clave + " ===");
            sb.AppendLine("Secuencial : " + (p1 >= 0 ? "encontrada en la posición " + p1 : "NO encontrada") + "  | comparaciones = " + c1 + ", iteraciones = " + i1);
            sb.AppendLine("Binaria    : " + (p2 >= 0 ? "encontrada en la posición " + p2 : "NO encontrada") + "  | comparaciones = " + c2 + ", iteraciones = " + i2);
            sb.AppendLine("Hash       : " + (p3 >= 0 ? "encontrada en la casilla [" + p3 + "]" : "NO encontrada") + "  | comparaciones = " + c3 + ", iteraciones = " + i3);
            txtDemo.AppendText(sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }

        // ---------- Helpers ----------
        private static CheckBox Chequeo(string texto, bool marcado)
        {
            return new CheckBox { Text = texto, Checked = marcado, AutoSize = true };
        }

        private static NumericUpDown Numerico(int min, int max, int valor, int incremento, int ancho)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = valor, Increment = incremento, Width = ancho };
        }

        private static Button Boton(string texto, EventHandler accion)
        {
            var b = new Button { Text = texto, Width = 175, Height = 28 };
            b.Click += accion;
            return b;
        }

        private static FlowLayoutPanel FilaHorizontal(params Control[] controles)
        {
            var f = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            f.Controls.AddRange(controles);
            return f;
        }

        private static FlowLayoutPanel FilaEtiquetada(string texto, NumericUpDown nud)
        {
            var lbl = new Label { Text = texto, AutoSize = false, Width = 125, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
            return FilaHorizontal(lbl, nud);
        }

        private static GroupBox Grupo(string titulo, params Control[] controles)
        {
            var gb = new GroupBox { Text = titulo, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(4), Padding = new Padding(6) };
            var fl = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(6, 20)
            };
            fl.Controls.AddRange(controles);
            gb.Controls.Add(fl);
            return gb;
        }

        private static DataGridView CrearGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
        }

        private static DataGridViewTextBoxColumn Columna(string encabezado, string propiedad, int ancho, string formato = null)
        {
            var c = new DataGridViewTextBoxColumn { HeaderText = encabezado, DataPropertyName = propiedad, FillWeight = ancho };
            if (formato != null) c.DefaultCellStyle.Format = formato;
            return c;
        }

        // ==================================================================
        //  EJECUCIÓN
        // ==================================================================
        private async void BtnEjecutar_Click(object sender, EventArgs e)
        {
            var cfg = new ConfigExperimento
            {
                Secuencial = chkSec.Checked,
                Binaria = chkBin.Checked,
                Consultas = (int)nudConsultas.Value,
                Semilla = (int)nudSemilla.Value,
                FactorCarga = (double)nudAlfa.Value
            };

            if (chk1k.Checked) cfg.Tamanos.Add(1000);
            if (chk10k.Checked) cfg.Tamanos.Add(10000);
            if (chk100k.Checked) cfg.Tamanos.Add(100000);
            if (chk1M.Checked) cfg.Tamanos.Add(1000000);
            if (chkCustom.Checked && nudCustom.Value > 0 && !cfg.Tamanos.Contains((int)nudCustom.Value))
                cfg.Tamanos.Add((int)nudCustom.Value);
            cfg.Tamanos.Sort();

            for (int i = 0; i < clbFunciones.Items.Count; i++)
                if (clbFunciones.GetItemChecked(i)) cfg.Funciones.Add((FuncionHash)i);
            for (int i = 0; i < clbColisiones.Items.Count; i++)
                if (clbColisiones.GetItemChecked(i)) cfg.Colisiones.Add((Colision)i);

            bool hayHash = cfg.Funciones.Count > 0 && cfg.Colisiones.Count > 0;
            if (cfg.Tamanos.Count == 0 || !(cfg.Secuencial || cfg.Binaria || hayHash))
            {
                MessageBox.Show("Selecciona al menos un tamaño y una técnica (secuencial, binaria o una función hash con un método de colisiones).",
                    "Faltan datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!hayHash) { cfg.Funciones.Clear(); cfg.Colisiones.Clear(); }

            if (cfg.Colisiones.Contains(Colision.SondeoCuadratico) && cfg.FactorCarga > 0.5)
            {
                var r = MessageBox.Show("Con sondeo cuadrático y α > 0.5 la tabla puede no encontrar casilla libre para algunas claves (quedarían sin insertar).\n\n¿Continuar de todos modos?",
                    "Aviso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            _resultados.Clear();
            txtLog.Clear();
            barra.Value = 0;
            AlternarEjecucion(true);
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            var progResultado = new Progress<ResultadoBusqueda>(rb =>
            {
                _resultados.Add(rb);
                RefrescarGrafica();
            });
            var progBitacora = new Progress<string>(msg =>
            {
                txtLog.AppendText(msg + Environment.NewLine);
                lblEstado.Text = msg.Trim();
            });
            var progPorcentaje = new Progress<int>(p => barra.Value = Math.Max(0, Math.Min(100, p)));

            try
            {
                await Task.Run(() => Experimento.Ejecutar(cfg, progResultado, progBitacora, progPorcentaje, ct));
                lblEstado.Text = "Experimento completado.";
                txtLog.AppendText("=== Experimento completado ===" + Environment.NewLine);
            }
            catch (OperationCanceledException)
            {
                lblEstado.Text = "Ejecución cancelada.";
                txtLog.AppendText("=== Cancelado por el usuario ===" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                lblEstado.Text = "Error: " + ex.Message;
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                AlternarEjecucion(false);
                RefrescarGrafica();
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
        }

        private void AlternarEjecucion(bool ejecutando)
        {
            btnEjecutar.Enabled = !ejecutando;
            btnCancelar.Enabled = ejecutando;
            btnCsv.Enabled = !ejecutando;
            btnPng.Enabled = !ejecutando;
        }

        // ==================================================================
        //  GRÁFICAS Y RESUMEN
        // ==================================================================
        private static double Valor(ResultadoBusqueda r, int metrica)
        {
            switch (metrica)
            {
                case 0: return r.PromComp;
                case 1: return r.MaxComp;
                case 2: return r.PromIter;
                case 3: return r.MaxIter;
                case 4: return r.TiempoMs;
                default: return r.Familia == "Hash" ? r.Colisiones : double.NaN;
            }
        }

        private static bool PasaFiltro(ResultadoBusqueda r, int filtro)
        {
            if (filtro == 0) return true;
            return r.Familia != "Hash" || (int)r.Func.Value == filtro - 1;
        }

        private void ConstruirDatos(string busqueda, int metrica, int filtro, out List<string> categorias, out List<SerieBarras> series)
        {
            var filtrados = _resultados.Where(r => r.Busqueda == busqueda && PasaFiltro(r, filtro)).ToList();
            var ns = filtrados.Select(r => r.N).Distinct().OrderBy(x => x).ToList();
            categorias = ns.Select(n => n.ToString("N0")).ToList();

            series = new List<SerieBarras>();
            foreach (var grupo in filtrados.GroupBy(r => r.Orden).OrderBy(g => g.Key))
            {
                var valores = new double[ns.Count];
                for (int i = 0; i < ns.Count; i++)
                {
                    var x = grupo.FirstOrDefault(r => r.N == ns[i]);
                    valores[i] = x == null ? double.NaN : Valor(x, metrica);
                }
                if (valores.All(double.IsNaN)) continue;
                series.Add(new SerieBarras
                {
                    Nombre = grupo.First().Corto,
                    Valores = valores,
                    Color = GraficaBarras.ColorDe(grupo.Key)
                });
            }
        }

        private static string TituloGrafica(string busqueda, int metrica, bool log)
        {
            string t = Metricas[metrica] + " - búsqueda " + busqueda.ToLower();
            return log ? t + " (escala log)" : t;
        }

        private void RefrescarGrafica()
        {
            if (grafica == null) return;
            string busqueda = cboBusqueda.SelectedItem.ToString();
            int metrica = cboMetrica.SelectedIndex;

            List<string> cats;
            List<SerieBarras> series;
            ConstruirDatos(busqueda, metrica, cboFiltro.SelectedIndex, out cats, out series);

            grafica.Titulo = TituloGrafica(busqueda, metrica, chkLog.Checked);
            grafica.EjeY = Metricas[metrica];
            grafica.Categorias = cats;
            grafica.Series = series;
            grafica.Logaritmica = chkLog.Checked;
            grafica.Invalidate();

            string formato = metrica == 4 ? "N3" : (metrica == 1 || metrica == 3 || metrica == 5) ? "N0" : "N2";
            dgvResumen.Columns.Clear();
            dgvResumen.Rows.Clear();
            dgvResumen.Columns.Add("tec", "Técnica");
            foreach (string c in cats) dgvResumen.Columns.Add("n" + c, "N = " + c);
            foreach (SerieBarras s in series)
            {
                var celdas = new List<object> { s.Nombre };
                celdas.AddRange(s.Valores.Select(v => double.IsNaN(v) ? (object)"-" : v.ToString(formato)));
                dgvResumen.Rows.Add(celdas.ToArray());
            }
        }

        // ==================================================================
        //  EXPORTAR
        // ==================================================================
        private void BtnCsv_Click(object sender, EventArgs e)
        {
            if (_resultados.Count == 0)
            {
                MessageBox.Show("Primero ejecuta el experimento.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "resultados_busqueda.csv" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var ci = CultureInfo.InvariantCulture;
                using (var sw = new StreamWriter(dlg.FileName, false, new UTF8Encoding(true)))
                {
                    sw.WriteLine("Tecnica,Corto,Familia,Funcion,Colision,N,Busqueda,PromComparaciones,MaxComparaciones,PromIteraciones,MaxIteraciones,Colisiones,MaxLongitud,FactorCarga,TamTabla,TiempoMs,Correcto");
                    foreach (var r in _resultados)
                    {
                        sw.WriteLine(string.Join(",", new string[]
                        {
                            r.Tecnica, r.Corto, r.Familia, r.Funcion, r.Metodo, r.N.ToString(ci), r.Busqueda,
                            r.PromComp.ToString("F4", ci), r.MaxComp.ToString(ci), r.PromIter.ToString("F4", ci), r.MaxIter.ToString(ci),
                            r.Colisiones.ToString(ci), r.MaxLongitud.ToString(ci), r.FactorCarga.ToString("F4", ci), r.TamTabla.ToString(ci),
                            r.TiempoMs.ToString("F4", ci), r.Correcto
                        }));
                    }
                }
                MessageBox.Show("CSV guardado en:\n" + dlg.FileName, "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnPng_Click(object sender, EventArgs e)
        {
            if (_resultados.Count == 0)
            {
                MessageBox.Show("Primero ejecuta el experimento.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new FolderBrowserDialog { Description = "Carpeta donde se guardarán las gráficas PNG" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string[] nombresFiltro = { "todas", "division", "multiplicacion", "cuadrado_medio" };
                int[] metricasExport = { 0, 2 }; // comparaciones promedio e iteraciones promedio
                int guardadas = 0;

                foreach (string busqueda in new[] { "Exitosa", "Fallida" })
                {
                    foreach (int metrica in metricasExport)
                    {
                        for (int filtro = 1; filtro <= 3; filtro++)
                        {
                            List<string> cats;
                            List<SerieBarras> series;
                            ConstruirDatos(busqueda, metrica, filtro, out cats, out series);
                            if (series.Count == 0) continue;

                            var g = new GraficaBarras
                            {
                                Titulo = TituloGrafica(busqueda, metrica, chkLog.Checked) + " - " + cboFiltro.Items[filtro],
                                EjeY = Metricas[metrica],
                                Categorias = cats,
                                Series = series,
                                Logaritmica = chkLog.Checked
                            };
                            using (var bmp = new Bitmap(1200, 650))
                            using (var gr = Graphics.FromImage(bmp))
                            {
                                g.Dibujar(gr, bmp.Width, bmp.Height);
                                string nombre = "grafica_" + busqueda.ToLower() + "_" + (metrica == 0 ? "comparaciones" : "iteraciones") + "_" + nombresFiltro[filtro] + ".png";
                                bmp.Save(Path.Combine(dlg.SelectedPath, nombre), ImageFormat.Png);
                                guardadas++;
                            }
                        }
                    }
                }
                MessageBox.Show(guardadas + " gráfica(s) guardada(s) en:\n" + dlg.SelectedPath, "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
