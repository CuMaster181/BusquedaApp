using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace BusquedaApp
{
    public class ResultadoBusqueda
    {
        public int Orden { get; set; }              // 0 = secuencial, 1 = binaria, 2.. = hash
        public string Tecnica { get; set; }         // nombre completo
        public string Corto { get; set; }           // nombre para leyendas
        public string Familia { get; set; }         // Secuencial / Binaria / Hash
        public FuncionHash? Func { get; set; }
        public Colision? Met { get; set; }
        public int N { get; set; }
        public string Busqueda { get; set; }        // Exitosa / Fallida
        public double PromComp { get; set; }
        public long MaxComp { get; set; }
        public double PromIter { get; set; }
        public long MaxIter { get; set; }
        public long Colisiones { get; set; }        // solo hash (al insertar)
        public int MaxLongitud { get; set; }        // solo hash
        public double FactorCarga { get; set; }     // solo hash
        public int TamTabla { get; set; }           // solo hash
        public double TiempoMs { get; set; }        // tiempo total de las consultas
        public bool Ok { get; set; }

        public string Funcion { get { return Func.HasValue ? Nombres.Funcion(Func.Value) : "-"; } }
        public string Metodo { get { return Met.HasValue ? Nombres.Metodo(Met.Value) : "-"; } }
        public string Correcto { get { return Ok ? "Sí" : "NO"; } }
    }

    public class ConfigExperimento
    {
        public List<int> Tamanos = new List<int>();
        public bool Secuencial = true;
        public bool Binaria = true;
        public List<FuncionHash> Funciones = new List<FuncionHash>();
        public List<Colision> Colisiones = new List<Colision>();
        public int Consultas = 1000;
        public int Semilla = 2026;
        public double FactorCarga = 0.5;
    }

    public delegate int Buscador(int clave, out long comparaciones, out long iteraciones);

    /// <summary>
    /// Experimento: para cada N genera N claves enteras ÚNICAS y aleatorias, y busca
    /// "Consultas" claves existentes (búsqueda exitosa) y "Consultas" claves inexistentes
    /// (búsqueda fallida) con cada técnica, contando comparaciones e iteraciones.
    /// Todas las técnicas reciben exactamente las mismas consultas.
    /// </summary>
    public static class Experimento
    {
        public static void Ejecutar(ConfigExperimento cfg,
                                    IProgress<ResultadoBusqueda> resultado,
                                    IProgress<string> bitacora,
                                    IProgress<int> porcentaje,
                                    CancellationToken ct)
        {
            int tecnicas = (cfg.Secuencial ? 1 : 0) + (cfg.Binaria ? 1 : 0) + cfg.Funciones.Count * cfg.Colisiones.Count;
            int total = Math.Max(1, cfg.Tamanos.Count * tecnicas);
            int hechos = 0;

            foreach (int n in cfg.Tamanos)
            {
                ct.ThrowIfCancellationRequested();
                bitacora.Report("--- N = " + n.ToString("N0") + ": generando " + n.ToString("N0") + " claves únicas (semilla " + cfg.Semilla + ") ---");

                var rnd = new Random(cfg.Semilla + n);
                int[] claves = new int[n];
                var conjunto = new HashSet<int>();
                int cuenta = 0;
                while (cuenta < n)
                {
                    int k = rnd.Next();
                    if (conjunto.Add(k)) claves[cuenta++] = k;
                }

                int q = cfg.Consultas;
                int[] exitosas = new int[q];
                int[] fallidas = new int[q];
                for (int i = 0; i < q; i++) exitosas[i] = claves[rnd.Next(n)];
                for (int i = 0; i < q; i++)
                {
                    int k;
                    do { k = rnd.Next(); } while (conjunto.Contains(k));
                    fallidas[i] = k;
                }

                int[] ordenado = (int[])claves.Clone();
                Array.Sort(ordenado); // la binaria necesita orden previo (no se cronometra)

                if (cfg.Secuencial)
                {
                    bitacora.Report("Búsqueda secuencial...");
                    Buscador b = (int x, out long cmp, out long itr) => Busqueda.Secuencial(claves, x, out cmp, out itr);
                    Registrar(resultado, 0, "Secuencial", "Secuencial", "Secuencial", null, null, n, b, exitosas, fallidas, null, ct);
                    porcentaje.Report(++hechos * 100 / total);
                }

                if (cfg.Binaria)
                {
                    bitacora.Report("Búsqueda binaria...");
                    Buscador b = (int x, out long cmp, out long itr) => Busqueda.Binaria(ordenado, x, out cmp, out itr);
                    Registrar(resultado, 1, "Binaria", "Binaria", "Binaria", null, null, n, b, exitosas, fallidas, null, ct);
                    porcentaje.Report(++hechos * 100 / total);
                }

                foreach (FuncionHash fh in cfg.Funciones)
                {
                    foreach (Colision col in cfg.Colisiones)
                    {
                        ct.ThrowIfCancellationRequested();
                        TablaHash tabla = TablaHash.Crear(n, cfg.FactorCarga, fh, col);
                        var sw = Stopwatch.StartNew();
                        for (int i = 0; i < claves.Length; i++) tabla.Insertar(claves[i]);
                        sw.Stop();

                        string nombre = "Hash " + Nombres.Funcion(fh) + " + " + Nombres.Metodo(col);
                        string corto = "Hash " + Nombres.FuncionCorta(fh) + "+" + Nombres.MetodoCorto(col);
                        bitacora.Report(nombre + "  (m = " + tabla.M.ToString("N0") + ", colisiones al insertar = " + tabla.ColisionesInsercion.ToString("N0") +
                                        ", construcción " + sw.Elapsed.TotalMilliseconds.ToString("N2") + " ms" +
                                        (tabla.NoInsertadas > 0 ? ", NO INSERTADAS: " + tabla.NoInsertadas : "") + ")");

                        TablaHash t = tabla;
                        Buscador b = (int x, out long cmp, out long itr) => t.Buscar(x, out cmp, out itr);
                        int orden = 2 + (int)fh * 4 + (int)col;
                        Registrar(resultado, orden, nombre, corto, "Hash", fh, col, n, b, exitosas, fallidas, t, ct);
                        porcentaje.Report(++hechos * 100 / total);
                    }
                }
            }
        }

        private static void Registrar(IProgress<ResultadoBusqueda> res, int orden, string tecnica, string corto, string familia,
                                      FuncionHash? fh, Colision? col, int n, Buscador f,
                                      int[] exitosas, int[] fallidas, TablaHash tabla, CancellationToken ct)
        {
            for (int pasada = 0; pasada < 2; pasada++)
            {
                bool exitosa = pasada == 0;
                var e = Evaluar(f, exitosa ? exitosas : fallidas, exitosa, ct);
                res.Report(new ResultadoBusqueda
                {
                    Orden = orden,
                    Tecnica = tecnica,
                    Corto = corto,
                    Familia = familia,
                    Func = fh,
                    Met = col,
                    N = n,
                    Busqueda = exitosa ? "Exitosa" : "Fallida",
                    PromComp = e.Item1,
                    MaxComp = e.Item2,
                    PromIter = e.Item3,
                    MaxIter = e.Item4,
                    TiempoMs = e.Item5,
                    Ok = e.Item6,
                    Colisiones = tabla != null ? tabla.ColisionesInsercion : 0,
                    MaxLongitud = tabla != null ? tabla.MaxLongitud : 0,
                    FactorCarga = tabla != null ? tabla.FactorCarga : 0,
                    TamTabla = tabla != null ? tabla.M : 0
                });
            }
        }

        private static Tuple<double, long, double, long, double, bool> Evaluar(Buscador f, int[] consultas, bool exitosa, CancellationToken ct)
        {
            long sumC = 0, sumI = 0, maxC = 0, maxI = 0;
            bool ok = true;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < consultas.Length; i++)
            {
                if ((i & 15) == 0) ct.ThrowIfCancellationRequested();
                long cmp, itr;
                int pos = f(consultas[i], out cmp, out itr);
                sumC += cmp;
                sumI += itr;
                if (cmp > maxC) maxC = cmp;
                if (itr > maxI) maxI = itr;
                if ((pos >= 0) != exitosa) ok = false;
            }
            sw.Stop();

            double q = consultas.Length;
            return Tuple.Create(sumC / q, maxC, sumI / q, maxI, sw.Elapsed.TotalMilliseconds, ok);
        }
    }
}
