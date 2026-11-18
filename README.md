# Unidad 6 - Métodos de búsqueda (Tarea 6.2)

**Alumno:** Mario Alejandro Martinez Alonso
**Matrícula:** I25050368
**Docente:** Rubén Miguel Riojas

Aplicación C# (.NET, Windows Forms) con búsqueda secuencial, búsqueda binaria y una tabla hash propia,
más un experimento que cuenta **comparaciones** e **iteraciones** de cada técnica.

## Contenido
| Archivo | Descripción |
|---|---|
| `Busquedas.cs` | Búsqueda secuencial, binaria y `TablaHash` (3 funciones de dispersión y 4 métodos de colisión) |
| `Experimento.cs` | Genera N claves únicas aleatorias y mide búsquedas exitosas y fallidas |
| `GraficaBarras.cs` | Gráfica de barras propia (GDI+) |
| `MainForm.cs` | Interfaz: configuración, resultados, gráficas, demostración, bitácora, exportación |

### Tabla hash
- Funciones de dispersión: **división** (k mod m), **multiplicación** (Knuth, A = 0.618…) y **cuadrado medio**.
- Colisiones: **encadenamiento separado**, **sondeo lineal**, **sondeo cuadrático** y **doble hash** (h2 = 1 + k mod (m-1)).
- El tamaño m es el siguiente número primo para el factor de carga α elegido (por defecto 0.5).

### Métricas
- *Comparaciones*: comparaciones de la clave buscada contra claves almacenadas (binaria: una comparación de tres vías por iteración).
- *Iteraciones*: vueltas del ciclo (secuencial/binaria) o casillas/nodos visitados (hash).
- Búsqueda **exitosa** (clave existente) y **fallida** (clave inexistente), con las mismas consultas para todas las técnicas.

## Cómo ejecutarlo
1. Visual Studio 2022 (carga *Desarrollo de escritorio con .NET*) → abrir `BusquedaApp.csproj` → **F5**.
   Por consola: `dotnet run -c Release`. Con .NET 6 cambia el `TargetFramework` a `net6.0-windows`.
2. Pestaña principal: elige tamaños y técnicas → **Ejecutar experimento**.
3. Pestaña **Demostración**: genera pocos datos y busca una clave para ver cada técnica paso a paso (útil para el video).
4. **Exportar CSV** y **Exportar gráficas PNG** para el reporte.

## Subir a GitHub
```bash
git init
git add .
git commit -m "Tarea 6.2 - Metodos de busqueda"
git branch -M main
git remote add origin https://github.com/<tu-usuario>/<tu-repo>.git
git push -u origin main
```
