using System;
using System.Collections.Generic;
using System.Text;

namespace BusquedaApp
{
    public enum FuncionHash
    {
        Division = 0,
        Multiplicacion = 1,
        CuadradoMedio = 2
    }

    public enum Colision
    {
        Encadenamiento = 0,
        SondeoLineal = 1,
        SondeoCuadratico = 2,
        DobleHash = 3
    }

    public static class Nombres
    {
        public static string Funcion(FuncionHash f)
        {
            switch (f)
            {
                case FuncionHash.Division: return "División";
                case FuncionHash.Multiplicacion: return "Multiplicación";
                default: return "Cuadrado medio";
            }
        }

        public static string FuncionCorta(FuncionHash f)
        {
            switch (f)
            {
                case FuncionHash.Division: return "Div";
                case FuncionHash.Multiplicacion: return "Mult";
                default: return "CMed";
            }
        }

        public static string Metodo(Colision c)
        {
            switch (c)
            {
                case Colision.Encadenamiento: return "Encadenamiento";
                case Colision.SondeoLineal: return "Sondeo lineal";
                case Colision.SondeoCuadratico: return "Sondeo cuadrático";
                default: return "Doble hash";
            }
        }

        public static string MetodoCorto(Colision c)
        {
            switch (c)
            {
                case Colision.Encadenamiento: return "Enc";
                case Colision.SondeoLineal: return "Lin";
                case Colision.SondeoCuadratico: return "Cuad";
                default: return "Dbl";
            }
        }
    }

    /// <summary>
    /// Búsqueda secuencial y binaria. Ambas devuelven el índice (o -1) y, ademas,
    /// el número de COMPARACIONES de claves y de ITERACIONES del ciclo realizadas.
    /// </summary>
    public static class Busqueda
    {
        /// <summary>Secuencial: recorre el arreglo (sin orden previo). Peor caso O(n).</summary>
        public static int Secuencial(int[] a, int clave, out long comparaciones, out long iteraciones)
        {
            comparaciones = 0;
            iteraciones = 0;
            for (int i = 0; i < a.Length; i++)
            {
                iteraciones++;
                comparaciones++;
                if (a[i] == clave) return i;
            }
            return -1;
        }

        /// <summary>
        /// Binaria: REQUIERE el arreglo ordenado. En cada iteración se hace UNA comparación
        /// de tres vías (CompareTo) con el elemento central. Peor caso O(log n).
        /// </summary>
        public static int Binaria(int[] a, int clave, out long comparaciones, out long iteraciones)
        {
            comparaciones = 0;
            iteraciones = 0;
            int bajo = 0, alto = a.Length - 1;
            while (bajo <= alto)
            {
                iteraciones++;
                int medio = bajo + (alto - bajo) / 2;
                int c = a[medio].CompareTo(clave);
                comparaciones++;
                if (c == 0) return medio;
                if (c < 0) bajo = medio + 1;
                else alto = medio - 1;
            }
            return -1;
        }
    }

    /// <summary>
    /// Tabla hash propia con 3 funciones de dispersión y 4 métodos de resolución de colisiones:
    /// encadenamiento separado y direccionamiento abierto (sondeo lineal, cuadrático y doble hash).
    /// Las claves son enteros no negativos.
    /// </summary>
    public sealed class TablaHash
    {
        public int M { get; private set; }
        public FuncionHash Funcion { get; private set; }
        public Colision Metodo { get; private set; }

        /// <summary>Inserciones cuya posición inicial ya estaba ocupada.</summary>
        public long ColisionesInsercion { get; private set; }
        /// <summary>Cadena más larga (encadenamiento) o secuencia de sondeo más larga (abierto).</summary>
        public int MaxLongitud { get; private set; }
        public int Cantidad { get; private set; }
        public int NoInsertadas { get; private set; }
        public double FactorCarga { get { return (double)Cantidad / M; } }

        // Direccionamiento abierto
        private readonly int[] _claves;
        private readonly bool[] _ocupado;
        // Encadenamiento (listas enlazadas implementadas con arreglos)
        private readonly int[] _cabeza;
        private readonly int[] _siguiente;
        private readonly int[] _nodoClave;
        private readonly int[] _largo;
        private int _nodos;

        public TablaHash(int m, FuncionHash funcion, Colision metodo, int capacidad)
        {
            M = m;
            Funcion = funcion;
            Metodo = metodo;
            if (metodo == Colision.Encadenamiento)
            {
                _cabeza = new int[m];
                for (int i = 0; i < m; i++) _cabeza[i] = -1;
                _siguiente = new int[capacidad];
                _nodoClave = new int[capacidad];
                _largo = new int[m];
            }
            else
            {
                _claves = new int[m];
                _ocupado = new bool[m];
            }
        }

        /// <summary>Crea una tabla de tamaño primo con el factor de carga objetivo indicado.</summary>
        public static TablaHash Crear(int n, double factorCarga, FuncionHash f, Colision c)
        {
            int minimo = (int)Math.Ceiling(n / factorCarga);
            if (c != Colision.Encadenamiento && minimo <= n) minimo = n + 1; // abierto: m > n
            int m = SiguientePrimo(Math.Max(minimo, 3));
            return new TablaHash(m, f, c, n);
        }

        public static int SiguientePrimo(int n)
        {
            if (n <= 2) return 2;
            if (n % 2 == 0) n++;
            while (!EsPrimo(n)) n += 2;
            return n;
        }

        private static bool EsPrimo(int n)
        {
            if (n < 2) return false;
            if (n % 2 == 0) return n == 2;
            for (int d = 3; (long)d * d <= n; d += 2)
                if (n % d == 0) return false;
            return true;
        }

        // ---------------- Funciones de dispersión ----------------
        private int H1(int k)
        {
            switch (Funcion)
            {
                case FuncionHash.Division:
                    // h(k) = k mod m
                    return k % M;

                case FuncionHash.Multiplicacion:
                    {
                        // h(k) = floor( m * frac(k * A) ),  A = (raiz(5) - 1) / 2  (Knuth)
                        double x = k * 0.6180339887498949;
                        x -= Math.Floor(x);
                        int r = (int)(M * x);
                        return r >= M ? M - 1 : r;
                    }

                default:
                    {
                        // Cuadrado medio: se eleva al cuadrado y se toman los bits centrales
                        ulong cuadrado = (ulong)k * (ulong)k;
                        return (int)((cuadrado >> 16) % (ulong)M);
                    }
            }
        }

        // Segunda función para doble hash: h2(k) = 1 + (k mod (m-1))   (nunca es 0)
        private int H2(int k)
        {
            return 1 + (k % (M - 1));
        }

        private int Posicion(int h1, int h2, int i)
        {
            switch (Metodo)
            {
                case Colision.SondeoLineal: return (int)(((long)h1 + i) % M);
                case Colision.SondeoCuadratico: return (int)(((long)h1 + (long)i * i) % M);
                default: return (int)(((long)h1 + (long)i * h2) % M); // doble hash
            }
        }

        // ---------------- Insertar ----------------
        public bool Insertar(int clave)
        {
            int h1 = H1(clave);

            if (Metodo == Colision.Encadenamiento)
            {
                if (_cabeza[h1] != -1) ColisionesInsercion++;
                _nodoClave[_nodos] = clave;
                _siguiente[_nodos] = _cabeza[h1];
                _cabeza[h1] = _nodos;
                _nodos++;
                _largo[h1]++;
                if (_largo[h1] > MaxLongitud) MaxLongitud = _largo[h1];
                Cantidad++;
                return true;
            }

            int h2 = Metodo == Colision.DobleHash ? H2(clave) : 0;
            for (int i = 0; i < M; i++)
            {
                int idx = Posicion(h1, h2, i);
                if (!_ocupado[idx])
                {
                    _ocupado[idx] = true;
                    _claves[idx] = clave;
                    Cantidad++;
                    if (i > 0) ColisionesInsercion++;
                    if (i + 1 > MaxLongitud) MaxLongitud = i + 1;
                    return true;
                }
            }
            NoInsertadas++; // el sondeo no encontró lugar (posible con sondeo cuadrático y carga alta)
            return false;
        }

        // ---------------- Buscar ----------------
        /// <summary>
        /// Devuelve la posición (cubo o casilla) donde está la clave, o -1.
        /// comparaciones = comparaciones de claves; iteraciones = casillas/nodos visitados.
        /// </summary>
        public int Buscar(int clave, out long comparaciones, out long iteraciones)
        {
            comparaciones = 0;
            iteraciones = 0;
            int h1 = H1(clave);

            if (Metodo == Colision.Encadenamiento)
            {
                int nodo = _cabeza[h1];
                while (nodo != -1)
                {
                    iteraciones++;
                    comparaciones++;
                    if (_nodoClave[nodo] == clave) return h1;
                    nodo = _siguiente[nodo];
                }
                if (iteraciones == 0) iteraciones = 1; // se consultó el cubo vacío
                return -1;
            }

            int h2 = Metodo == Colision.DobleHash ? H2(clave) : 0;
            for (int i = 0; i < M; i++)
            {
                int idx = Posicion(h1, h2, i);
                iteraciones++;
                if (!_ocupado[idx]) return -1;      // casilla vacía: la clave no existe
                comparaciones++;
                if (_claves[idx] == clave) return idx;
            }
            return -1;
        }

        /// <summary>Texto con el contenido de la tabla (para la pestaña de demostración).</summary>
        public string Volcar()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < M; i++)
            {
                if (Metodo == Colision.Encadenamiento)
                {
                    sb.Append("[" + i.ToString().PadLeft(3) + "] ");
                    int nodo = _cabeza[i];
                    if (nodo == -1) sb.Append("-");
                    while (nodo != -1)
                    {
                        sb.Append(_nodoClave[nodo]);
                        nodo = _siguiente[nodo];
                        if (nodo != -1) sb.Append(" -> ");
                    }
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("[" + i.ToString().PadLeft(3) + "] " + (_ocupado[i] ? _claves[i].ToString() : "-"));
                }
            }
            return sb.ToString();
        }
    }
}
