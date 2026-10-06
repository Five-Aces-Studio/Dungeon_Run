# Prototipo de laberinto: análisis y cambios que debes implementar

Fecha: 2026-10-04. Los ejemplos de C# de esta guía NO se han aplicado al proyecto.

## Qué existe actualmente

- `SceneGuille.unity` contiene el prototipo antiguo de combate y cartas.
- `SceneGuilleHex.unity` utiliza `RandomWalkWFC`, `HexMapNavigator`, `FogParticles` y `CameraFollowNode`. Su YAML conserva el nombre antiguo `RandomWalkWFCHex`, pero el GUID apunta al archivo actual `RandomWalkWFC.cs`.
- `SceneGuilleTri.unity` referencia el generador `WormWFC`, cuyo GUID no he encontrado en los `.meta` de Assets. No la tomaría como base sin comprobar ese componente en el Inspector.
- La generación tiene dos fases: `RandomWalk()` construye un conjunto de coordenadas; WFC asigna tipos a esas coordenadas según pesos, incompatibilidades y máximos. Actualmente WFC no diseña puertas ni pasillos.
- El navegador solo permite un salto a un hexágono vecino. Asigna `active = coord` inmediatamente; no mueve un personaje ni busca caminos largos.
- El conjunto `revealed` conserva el descubrimiento. El radio utiliza distancia hexagonal; `FogParticles` recibe una lista de posiciones para emitir volutas.
- `HexCellVisual` cambia el color y la emisión del primer Renderer hijo. Esa búsqueda deja de ser fiable cuando una celda contiene suelo, paredes, niebla y objetos.
- El código de exploración revisado no inicia por sí mismo un combate, una tienda o una recompensa al pisar esos tipos. Aquí preparamos su representación; la conexión con los sistemas de juego será otro paso explícito.

## Diseño propuesto

Piensa en dos planos de un edificio. Uno dice dónde están las habitaciones y sus puertas; el otro decide qué hay en cada habitación. El primero será el laberinto; el segundo seguirá siendo WFC.

```text
Semilla -> cuadrícula + pasos abiertos -> WFC de contenidos
        -> suelo + muros + objetos -> estado de descubrimiento
Clic -> comprobar paso -> correr -> confirmar llegada -> revelar
```

La cuadrícula pasa a ser cuadrada por tu petición. Los motivos triangulares se conservan en la arquitectura y los objetos. Esto es una prueba deliberada frente a la identidad triangular descrita en GAME_DESIGN, no un cambio definitivo de esas reglas.

Para la primera prueba: 12 x 10 celdas, lado de 3 m, pasos cardinales, un recorrido conectado y una pequeña probabilidad de abrir conexiones adicionales. Esos valores son parámetros de prueba.

La decisión fundamental es **vecindad geométrica != conexión transitable**. Dos celdas pegadas pueden tener una pared entre ellas.

## 1. Primero, representa las conexiones

Crea tú `Assets/Scripts/PCG/MazeLayout.cs`. Esta clase no instancia objetos; solo guarda coordenadas y pasos. Usa un DFS iterativo: avanza a una celda sin visitar; si no puede avanzar, retrocede por la pila. Después abre algunos pasos adicionales para introducir rutas alternativas.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MazeLayout
{
    public static readonly Vector2Int[] Directions =
    {
        Vector2Int.right, Vector2Int.left,
        Vector2Int.up, Vector2Int.down
    };

    private readonly Dictionary<Vector2Int, List<Vector2Int>> links = new();
    public IEnumerable<Vector2Int> Cells => links.Keys;

    public bool CanMove(Vector2Int from, Vector2Int to) =>
        links.TryGetValue(from, out var neighbors) && neighbors.Contains(to);

    public IEnumerable<Vector2Int> Neighbors(Vector2Int cell) => links[cell];

    private void Connect(Vector2Int a, Vector2Int b)
    {
        if (!links[a].Contains(b)) links[a].Add(b);
        if (!links[b].Contains(a)) links[b].Add(a);
    }

    public static MazeLayout Build(int width, int height, int seed, float extraPassageChance)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException();
        var map = new MazeLayout();
        var rng = new System.Random(seed);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map.links.Add(new Vector2Int(x, y), new List<Vector2Int>());

        var visited = new HashSet<Vector2Int> { Vector2Int.zero };
        var stack = new Stack<Vector2Int>();
        stack.Push(Vector2Int.zero);
        var candidates = new List<Vector2Int>(4);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            candidates.Clear();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (map.links.ContainsKey(next) && !visited.Contains(next))
                    candidates.Add(next);
            }
            if (candidates.Count == 0) { stack.Pop(); continue; }
            var chosen = candidates[rng.Next(candidates.Count)];
            map.Connect(current, chosen);
            visited.Add(chosen);
            stack.Push(chosen);
        }

        // Cada borde se considera una sola vez, con orden estable.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var a = new Vector2Int(x, y);
                TryExtra(a, a + Vector2Int.right);
                TryExtra(a, a + Vector2Int.up);
            }
        return map;

        void TryExtra(Vector2Int a, Vector2Int b)
        {
            if (map.links.ContainsKey(b) && !map.CanMove(a, b) &&
                rng.NextDouble() < Mathf.Clamp01(extraPassageChance))
                map.Connect(a, b);
        }
    }

    public Dictionary<Vector2Int, int> Distances(Vector2Int start, int maxDistance)
    {
        var result = new Dictionary<Vector2Int, int>();
        if (!links.ContainsKey(start) || maxDistance < 0) return result;
        var queue = new Queue<Vector2Int>();
        result[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (result[current] >= maxDistance) continue;
            foreach (var next in links[current])
            {
                if (result.ContainsKey(next)) continue;
                result[next] = result[current] + 1;
                queue.Enqueue(next);
            }
        }
        return result;
    }
}
```

No has cambiado WFC todavía. El objetivo de este paso es poder responder «¿hay puerta entre A y B?» sin mirar una malla ni lanzar un raycast.

## 2. Integración en RandomWalkWFC, conservando WFC

Mantén el nombre del archivo y su `.meta` durante esta primera iteración. Añade un modo serializado `squareMaze`, desactivado por defecto para que SceneGuilleHex conserve su configuración.

| Lugar del archivo | Cambio a realizar |
|---|---|
| Campos | Añade `squareMaze`, `width`, `height`, `cellSize = 3`, `seed`, `extraPassageChance`, y una propiedad pública de solo lectura `MazeLayout Layout`. |
| `Generate()` | En modo laberinto crea `Layout = MazeLayout.Build(...)` y `domain = new HashSet<Vector2Int>(Layout.Cells)`. En modo hexagonal conserva `RandomWalk()`. |
| `StartWave()` | Recorre el dominio ordenado por `y` y después `x`. Antes del primer colapso, limita `(0,0)` a opciones `Normal`; si no existen, termina con un error claro. Así el inicio no exige regenerar mapas enteros. |
| `GetNeighbors()` | En modo laberinto devuelve únicamente los vecinos de `Layout.Neighbors(coord)`. Se propone aplicar las incompatibilidades de contenido a habitaciones comunicadas. Si quisieras prohibiciones incluso a través de muros, usa una consulta separada de vecindad geométrica. |
| Entropía, pesos, `Compatible`, filtrado y máximos | Reutiliza el funcionamiento existente. No hace falta convertir todos los contenidos en nuevas reglas de puertas. |
| `HexCenter()` | Sustituye su uso por `CellCenter()`, que en modo laberinto devuelve `(q * cellSize, 0, r * cellSize)` como posición LOCAL. |
| `InstantiateCollapsedCells()` | Coloca la raíz de cada celda por coordenadas. No calcules su posición a partir del primer `Renderer.bounds`: un cofre o una pared descentraría la celda. |
| Geometría | Crea suelo, muros, contenido y niebla bajo raíces separadas y con referencias explícitas. No conserves el componente `Hexagon` en los nuevos prefabs cuadrados. |
| Gizmos | Dibuja cuatro lados y/o pasos abiertos en modo cuadrado; conserva los seis lados en modo hexagonal. |
| Fin de generación | Publica `OnGenerationComplete` solo después de instanciar todo y construir los muros. Para esta prueba usa `progressive = false`, evitando enseñar el mapa completo antes de configurar la niebla. |

Para los muros: recorre cada celda y comprueba norte y este. Si el vecino no existe o `CanMove` es falso, crea un muro en el punto medio del borde. Añade además los bordes exteriores sur y oeste. Así no duplicas las paredes compartidas. El muro exportado mide 3 m a lo largo de X; gíralo 90 grados para un borde paralelo a Z.

Hay dos correcciones de robustez recomendadas dentro del generador actual:

- `BuildTileSet()` registra un error si está vacío, pero la generación continúa. Valida opciones no vacías y pesos positivos antes de empezar.
- `Fail()` llama inmediatamente a `StartWave()` desde `Collapse`/`BanType`/`Propagate`. Eso permite que una operación de la oleada antigua continúe después del reinicio. Marca la oleada como fallida y reiníciala desde el nivel superior en el siguiente paso; abandona inmediatamente la propagación actual. Conserva el límite de reintentos y no publiques éxito al agotarlo.

La semilla del ejemplo reproduce la topología. Para reproducir también los contenidos debes sustituir el uso de `UnityEngine.Random` en WFC por otra instancia local de `System.Random`, con semilla derivada y recorrido estable de celdas. El generador actual comparte azar global con las partículas; no basta con fijar una semilla una sola vez.

No añadas aún WFC de geometría: el WFC actual trabaja con contenidos. Si más adelante quieres piezas de pasillo que se colapsen, necesitarán conectores direccionales; además habrá que garantizar conectividad global. Las restricciones locales de WFC por sí solas no demuestran que todo el mapa sea accesible. [Referencia original de WFC](https://github.com/mxgmn/WaveFunctionCollapse).

## 3. Navegación y avatar

Conserva la lectura del ratón del Input System y el diccionario de celdas. En `HexMapNavigator.Update()` reemplaza la comprobación de distancia por `generator.Layout.CanMove(active, coord)` cuando esté activo el modo laberinto.

Para la primera versión conserva el clic en una celda adyacente conectada. Pulsar una celda lejana sigue sin efecto. Buscar rutas largas con BFS puede añadirse después y solo debe atravesar celdas descubiertas; no está implícito en esta prueba.

Añade un `MazeAvatarMover` que se encargue únicamente de mover un Transform y controlar el Animator. El flujo del navegador debe ser:

```text
si no está listo, hay UI bajo el puntero o hay movimiento -> ignorar clic
resolver celda con el collider del suelo
si no hay paso abierto -> ignorar clic
bloquear nuevos movimientos
Running = true
recorrer el segmento origen-destino a velocidad constante
al alcanzar exactamente el destino:
    Running = false
    active = destino
    Reveal / RefreshStates
    resolver contenido de la celda, cuando exista esa integración
desbloquear movimientos
```

Para el desplazamiento utiliza `Vector3.MoveTowards(actual, destino, velocidad * Time.deltaTime)` por frame y rota hacia el desplazamiento. El Animator usa el bool **`Running`**. La raíz no utiliza root motion: el código controla el avance y la animación mueve las extremidades en el sitio.

Al regenerar: bloquea entrada antes de destruir celdas, cancela el movimiento pendiente, invalida cualquier llegada antigua, limpia `revealed` y reinicializa el avatar con el nuevo mapa. Un evento `OnGenerationStarted` antes de limpiar objetos es una forma concreta de coordinarlo. Al deshabilitar el navegador cancela también su movimiento.

Adapta `CameraFollowNode` para recibir el Transform del avatar y leer su posición en `LateUpdate`. Conserva su `SmoothDamp`. Si sigues enviando solamente el centro del destino, la cámara anticipará la llegada y se separará del personaje.

El cofre o enemigo ocupa el centro visual de su celda. Define un `ArrivalAnchor` delante del objeto para que el avatar no termine dentro de él; si después permites cruzar la celda, el movimiento deberá recorrer puntos de paso que rodeen ese objeto. En la primera prueba trata esas celdas como paradas de interacción. No uses un collider del prop para sustituir la validación de las conexiones del mapa.

## 4. Descubrimiento y shader de niebla

Conserva `revealed` como estado de juego. El shader solo dibuja: no sabe qué ha visitado el jugador.

- Usa `Layout.Distances(active, revealRadius)` para el radio. No uses distancia Manhattan para revelar: ignoraría las paredes.
- Para conservar la regla actual: distancia de grafo `< revealRadius` se añade a `revealed`; `== revealRadius` es penumbra si no estaba revelada; lo demás queda en niebla.
- El estado `Adjacent` requiere un paso abierto real, no solo distancia de coordenadas 1.
- La distancia por pasos permite revelar alrededor de una esquina. Es una regla de exploración por grafo, no una simulación óptica de línea de visión.
- En modo laberinto elimina las llamadas a `FogParticles` y desactiva ese componente en la instancia de prueba.

El recurso preparado es `PF_MazeFog`: plano cuadrado de 3 m, sin collider, elevado 0.04 m, con shader URP `DungeonRun/Maze/FogOfWar`.

Contrato del shader: **`_Coverage = 1`** oculta el suelo, **`0.5`** muestra penumbra y **`0`** lo descubre. El ruido animado usa coordenadas de mundo para que no se repita una nube idéntica en cada baldosa. Solo afecta al color, no perfora la opacidad de las zonas desconocidas.

Adapta `HexCellVisual` (o crea una vista específica) con referencias explícitas a `fogRenderer`, `contentRoot` y `selectionRoot`. Este fragmento ilustra el cambio; intégralo con tus campos y ciclo de vida:

```csharp
// Cachear una vez por vista, no crear cada frame.
private readonly MaterialPropertyBlock fogBlock = new();
private static readonly int CoverageId = Shader.PropertyToID("_Coverage");

// En la actualización visual de la celda:
bool hidden = state == CellVisualState.Fog || state == CellVisualState.Penumbra;
contentRoot.SetActive(!hidden);
float coverage = state == CellVisualState.Fog ? 1f :
                 state == CellVisualState.Penumbra ? 0.5f : 0f;
fogRenderer.GetPropertyBlock(fogBlock);
fogBlock.SetFloat(CoverageId, coverage);
fogRenderer.SetPropertyBlock(fogBlock);
fogRenderer.enabled = coverage > 0f;
```

**Oculta `contentRoot` completo**, incluidas sus luces, efectos y sombras. Un plano de niebla no tapa objetos que sobresalen por encima; esta separación evita revelar el tipo de celda. El collider de suelo permanece disponible para el clic de exploración. No desactives la raíz completa de la celda.

Si quieres disipación suave: anima un valor visual `currentCoverage` hacia el valor objetivo durante unos 0.25 s y desactiva el Renderer solo al terminar. El estado lógico y la ocultación del contenido no deben depender del ruido del shader. Mantén el contenido oculto hasta completar la transición de descubrimiento.

Esta primera versión es niebla de guerra de superficie, no volumen de humo. Las paredes delimitan el mapa y siguen visibles. Más adelante se puede probar una máscara compartida o un volumen con bordes suaves si aporta claridad; no hace falta para sustituir las partículas.

## 5. Objetos en el centro y prefabs

Puedes mantener `CellType` y `TileWFC.Weight`. Los tipos ya expresan el contenido; no necesitan seguir expresándolo con el color del suelo.

| Tipo | Contenido visual |
|---|---|
| Normal | Suelo libre |
| Item | `PF_PROP_Maze_Chest` |
| Combat | `PF_SnakeSoldier` existente; en la muestra se usa escala 0.65 |
| Event | `PF_PROP_Maze_EventAltar` |
| Shop | `PF_PROP_Maze_Shop` |

Prepara cinco prefabs de celda que compartan el mismo suelo/material y difieran por su `ContentRoot` y `TileWFC.type`. Todos deben tener la raíz en el centro de la celda. Una estructura sugerida:

```text
CellRoot (TileWFC + HexCell durante la transición)
  Floor (PF_MazeFloor, BoxCollider)
  ContentRoot (cofre, enemigo, altar, tienda o vacío)
  Fog (PF_MazeFog)
  Selection (borde discreto, no color de tipo)
  ArrivalAnchor
```

`HexCell.q/r` se puede conservar temporalmente para minimizar cambios. No añadas paredes al catálogo de contenidos: se construyen desde `Layout`. El marcador hexagonal de `ActiveNodeMarker` debe convertirse en cuadrado o desactivarse en modo laberinto, usando el avatar y un borde de selección para indicar la posición.

En la muestra he preparado prefabs **visuales**, no esos cinco prefabs de gameplay, porque estos últimos dependen de los campos y contratos que vas a escribir.

## Recursos entregados

- `Assets/Art/Environment/MazePrototype/Models`: suelo, muro, pilar, cofre, altar, tienda y viajero adaptado del `CHR_Player` de TrigonalAbyss.
- `.../Prefabs`: versiones con materiales AcrylicV3 reutilizados, avatar y niebla.
- `.../Animations/AC_MazeExplorer.controller`: estados Idle/Run y bool Running. Los clips FBX repiten mediante transiciones al mismo estado al finalizar; no dependen de marcar Loop Time en el importador.
- `.../Shaders/SH_Maze_FogOfWar.shader` y `.../Materials/MAT_Maze_Fog.mat`.
- `docsBlender/MazePrototype/MazeKit.blend`: fuente editable, materiales originales reutilizados y animaciones provisionales.
- `Assets/Scenes/SceneGuille/SceneGuilleMaze.unity`: escena nueva solicitada expresamente, con la raíz `MazePrototype_VisualReview` en el origen y una muestra fija de 4x4. No es un mapa generado en runtime. La muestra se retiró de SceneVictorLab y las dos escenas canónicas de Victor se comprobaron sin diferencias respecto a su estado inicial en Git.

El muro mide 3 m de ancho y aproximadamente 1.43 m de alto. El suelo tiene su cara superior a 0.025 m. Coloca el avatar/interactuables sobre esa altura si necesitas contacto exacto. El viajero mide aproximadamente 1.8 m y su carrera es un primer ciclo sencillo bajo una capa, no una animación final con rig humano completo.

La cámara de muestra usa perspectiva elevada y los materiales existentes AcrylicV3. El código `WorldStyleGate` excluye cámaras ortográficas y limita el acabado de pantalla a SceneVictor/SceneVictorLab. Por eso la escena nueva no activa automáticamente ese acabado, aunque tiene asignado el perfil para la integración posterior. Su renderer se deja en Default para evitar índices inexistentes con la calidad Mobile/Android del proyecto.

Para habilitar tú el acabado completo también en esta escena, amplía `WorldStyleGate.IsStyledScene` con `sceneName == "SceneGuilleMaze"`, selecciona la calidad PC y el renderer `PC_LabPixel_Renderer` de esa calidad en `MazeReviewCamera`. Mantén `pixelEnabled = false` en `DungeonRunPixelRenderSettings`, porque el perfil acrílico ya incorpora su tratamiento de píxel. No se ha editado ese C# ni cambiado la configuración global de calidad. Las primeras capturas del laboratorio son comparaciones exploratorias anteriores al traslado; la validación final corresponde a SceneGuilleMaze.

## Orden de trabajo y pruebas

1. Implementa `MazeLayout` y comprueba conexiones simétricas, cardinales y conectividad de todas las celdas.
2. Con `extraPassageChance = 0`, un mapa de N celdas debe tener N-1 conexiones únicas. Comprueba el caso 1x1.
3. Integra WFC: mismos tipos/pesos/topes, inicio Normal, pares prohibidos sobre pasos abiertos. Prueba semillas fijas y contradicciones sin éxito falso ni bucles infinitos.
4. Dibuja suelos y paredes. Ningún borde abierto debe tener muro y ningún borde cerrado debe quedar sin él; no dupliques muros compartidos.
5. Integra clic y avatar: un clic contra pared no mueve; un clic válido corre una celda; clics repetidos no saltan pasos. El estado activo solo cambia al llegar.
6. Integra niebla: el contenido oculto no aparece, ni proyecta sombras reveladoras; volver por una ruta conocida no restaura la niebla. Regenerar durante un desplazamiento cancela la llegada antigua.
7. Comprueba objetos y anclas: no atravesar cofres o enemigos ni terminar superpuesto a ellos.
8. Revisa Console y capturas en Play Mode. Para medir rendimiento usa un mapa representativo, no la muestra 4x4.

Los fragmentos de esta guía son instrucciones para tu implementación y no han sido compilados como scripts del proyecto. La validación de los recursos visuales se registra al final del trabajo; no equivale a haber probado generación, navegación o revelado dinámicos.

## Verificación realizada de los recursos

- Blender MCP: modelos exportados, render del kit inspeccionado y cuatro poses del ciclo Run renderizadas. Se corrigieron los pesos del borde inferior de la capa y la visibilidad de las botas tras la primera revisión.
- Unity MCP: shader importado sin errores de compilación detectados; materiales existentes asignados a prefabs nuevos; escena final abierta y capturada en Play Mode.
- Comparación final reproducible: `docsBlender/MazePrototype/GuilleMaze_A_NoFog.png` frente a `GuilleMaze_Final_Fog.png`, misma cámara y geometría. Se comprobó `_Coverage` a 0 y 1 y se dejó a 1.
- Animator: ambos clips importados y asignados; se comprobó entrada al estado Run de 0.8 s mediante MCP. La animación es provisional; no se ha verificado locomoción entre celdas porque ese código no está implementado.
- Últimas comprobaciones: sin errores de Unity Console; hubo avisos intermitentes de reconexión WebSocket del MCP. En la prueba anterior en Victor apareció además el fallback Mobile de un renderer PC; la escena nueva utiliza Default para evitarlo.
- Blender mostró una excepción del complemento instalado Ucupaint al cambiar de frame (`Action.id_root` no disponible). Los renders y la exportación terminaron; no se ha modificado ni desactivado ese complemento.
- No se ejecutaron pruebas automáticas de gameplay: no se cambiaron scripts. `SceneVictor` y `SceneVictorLab` quedaron sin diff; se preservaron los cambios de configuración que existían antes del trabajo.

Mini-reto antes de integrar: si A=(0,0) y B=(1,0) tienen una pared entre ellas, ¿por qué la distancia Manhattan de 1 no basta para permitir el movimiento?
