# Navegación por nodos, niebla de guerra y escena nocturna (mapa hex WFC)

Fecha: 2026-07-09
Estado: aprobado por el usuario

## Contexto

`RandomWalkWFCHex` genera un mapa de celdas hexagonales en dos fases: un Random Walk
(`CarveDomain` + `CarveBrush`) talla el dominio de celdas, y un Wave Function Collapse
asigna a cada celda un tipo (`Normal`, `Combat`, `Item`, `Event`, `Shop`) respetando
pares prohibidos y topes por tipo. Cada celda instanciada lleva `HexCell3D` (q, r, type)
y su malla con `MeshCollider` la construye `Hexagon` en un hijo del prefab.

Restricciones técnicas verificadas:

- URP 17.3 (Unity 6). HDR activo por defecto → emisión HDR + Bloom funcionan.
- Input System nuevo en exclusiva (`activeInputHandler: 1`) → los clics se leen con
  `Mouse.current`, no con `Input.GetMouseButtonDown`.
- 4 materiales base (`Black`, `Blue`, `Red`, `Yellow`) que los prefabs de
  `TilesMapHex` sobreescriben por tipo. `MaterialPropertyBlock` no puede activar
  keywords de shader → el keyword `_EMISSION` debe activarse en los assets `.mat`.

## Objetivos

1. **Momentum en el Random Walk**: parámetro `[Range(0,1)] momentum` para generar
   mapas alargados tipo «gusano». Con `momentum = 0` el comportamiento es idéntico
   al actual.
2. **Navegación**: al terminar la generación hay un nodo activo, garantizado de tipo
   `Normal`. Clic en un nodo adyacente → pasa a ser el activo. Estado visual claro.
3. **Niebla de guerra**: los nodos lejanos están en niebla; al navegar, la niebla
   retrocede revelando nodos. Lo explorado queda revelado de forma permanente (RTS).
4. **Escena nocturna**: fondo oscuro; los nodos revelados emiten luz (emisión + Bloom);
   los nodos en niebla no emiten.
5. **Cámara**: sigue suavemente al nodo activo.

## Diseño

### A. Momentum (`RandomWalkWFCHex.CarveDomain`)

Nuevo campo serializado `momentum` (0..1, por defecto 0). El walk recuerda el índice
de la última dirección; en cada paso:

- con probabilidad `momentum` repite la última dirección;
- en caso contrario elige al azar, excluyendo la dirección opuesta a la última
  cuando `momentum > 0`.

El primer paso siempre es aleatorio puro. Con `momentum = 0` no se excluye nada:
comportamiento actual intacto.

### B. `HexMapNavigator` (nuevo, en el GameObject del generador)

- Se suscribe a `RandomWalkWFCHex.OnGenerationComplete`.
- **Registro**: indexa los `HexCell3D` hijos en `Dictionary<Vector2Int, HexCell3D>`
  y añade `HexCellVisual` a cada celda.
- **Nodo inicial**: la celda `Normal` con menor distancia hexagonal a (0,0).
  Si no existe ninguna `Normal`, relanza `Generate()` (tope: 5 reintentos,
  después error en consola).
- **Clic**: `Mouse.current.leftButton.wasPressedThisFrame` + `Physics.Raycast`
  desde `Camera.main`; el collider resuelve la celda con
  `GetComponentInParent<HexCell3D>()`. Solo se acepta si la celda es una de las
  6 adyacentes al nodo activo (distancia axial 1).
- Mantiene `HashSet<Vector2Int> revealed` (permanente) y el estado de niebla.

### C. Niebla y estados visuales (`HexCellVisual`)

Aplicados con `MaterialPropertyBlock` sobre `_BaseColor` y `_EmissionColor` del
renderer hijo. Colores de emisión por `CellType` configurables en el inspector
del navigator (5 entradas tipo → color HDR).

| Estado | Cuándo | Aspecto |
|---|---|---|
| Activo | Nodo actual | Emisión ×3 con pulso senoidal suave |
| Adyacente | Vecino del activo (clicable) | Emisión ×1.5 |
| Revelado | Explorado (permanente) | Emisión ×1, color de su tipo |
| Penumbra | Anillo a distancia == `revealRadius` | Emisión ×0.25 |
| Niebla | Nunca revelado | Emisión 0, base gris muy oscuro |

Regla de revelado al activar un nodo (`revealRadius` por defecto 2):

- distancia < `revealRadius` → revelado permanente;
- distancia == `revealRadius` → penumbra (promociona a revelado al acercarse);
- resto → niebla (si no fue revelado antes).

Distancia hexagonal axial: `(|dq| + |dr| + |dq + dr|) / 2`.

### D. Escena nocturna (`NightSceneSetup`)

Componente en el GameObject del generador; en `Awake`:

- cámara: `clearFlags = SolidColor`, fondo casi negro (#05060A);
- `RenderSettings.ambientMode = Flat`, ambiente azul muy oscuro;
- luz direccional de la escena atenuada (~0.05, tono frío) para intuir siluetas;
- crea en runtime un `Volume` global con `Bloom` (threshold ~0.9, intensidad
  moderada). Se crea por código para no editar a mano assets de VolumeProfile.

### E. Cámara (`CameraFollowNode`)

Componente en la cámara. El navigator le asigna el objetivo; conserva el offset
inicial cámara→objetivo y se mueve con `SmoothDamp`.

### Integración en escena y assets

- `SceneGuilleHex.unity`: añadir `HexMapNavigator` y `NightSceneSetup` al
  GameObject del generador y `CameraFollowNode` a la cámara (los `.cs.meta` se
  crean con GUIDs propios para poder referenciarlos desde el YAML de la escena).
- Materiales `Black/Blue/Red/Yellow.mat`: activar keyword `_EMISSION` y flags de GI
  para que el MPB pueda controlar el color de emisión por instancia.

## Flujo completo

`Generate()` → walk con momentum → WFC → `OnGenerationComplete` → navigator indexa,
elige nodo `Normal` inicial y revela su entorno → clic en adyacente → nuevo activo,
revelado permanente, penumbra en el frente → cámara sigue al activo.

## Manejo de errores

- Sin celda `Normal` tras generar → regenerar (máx. 5 veces) y error si se agota.
- Clic fuera del mapa o en celda no adyacente → ignorado.
- Regeneración (menú contextual «Generar» o reintentos del WFC) → el navigator
  se reinicializa en el siguiente `OnGenerationComplete` y limpia su estado.

## Addendum (2026-07-09): niebla física con partículas

Aprobado tras la primera implementación: además del apagado visual, las celdas en
niebla/penumbra están cubiertas por volutas de niebla reales.

- **`FogParticles`** (componente en el GameObject del generador): crea en runtime un
  único `ParticleSystem` (espacio mundo, emisión manual) con material
  `URP Particles/Unlit` transparente y textura de nube generada por código
  (falloff gaussiano × Perlin).
- `HexMapNavigator.RefreshStates` recopila las celdas en `Fog` (densidad 1) y
  `Penumbra` (densidad 0.5) y llama a `FogParticles.SetCoverage`. Un tick de
  emisión (~0.45 s) genera volutas sobre cada celda cubierta, proporcionales a la
  densidad. Al revelarse una celda deja de recibir volutas y las existentes mueren
  solas (~4 s) → la niebla se disipa gradualmente.
- Las celdas en niebla conservan la silueta oscura bajo las volutas (decisión del
  usuario: ayuda a ver hacia dónde se puede explorar).
- `HexMapNavigator.Init` llama a `FogParticles.ResetFog()` al regenerar el mapa.

## Addendum 2 (2026-07-10): ciclo de vida de la niebla y marcador del nodo activo

- **Ciclo de vida ligado al mapa** (decisión del usuario): `FogParticles` ya no crea
  el sistema en `Awake`; se suscribe a `OnGenerationComplete` y crea (o recrea si fue
  destruido) el `ParticleSystem` al completarse cada generación. El handler no toca
  la cobertura — eso sigue siendo del navigator, otro suscriptor del mismo evento —
  para que el orden de invocación entre suscriptores sea irrelevante. Además,
  `RandomWalkWFCHex.ClearGridObjects` ahora solo destruye hijos con `TileWFC`
  (los tiles del grid), no los auxiliares en runtime (FogPuffs, NightVolume).
- **Emisión uniforme**: todas las celdas fuera de la niebla (activa, adyacentes,
  reveladas) brillan con la misma intensidad (×3, la que antes tenía solo el activo).
  Penumbra y niebla no cambian. Se eliminó el pulso de emisión de `HexCellVisual`.
- **`ActiveNodeMarker`** (nuevo): anillo hexagonal plano procedural (URP Unlit, color
  HDR para que le afecte el Bloom) que flota sobre el nodo activo con un pulso suave
  de escala. Lo crea el navigator en runtime bajo el generador y lo reposiciona en
  cada navegación; el radio se deduce del bounds del tile (Z = 2·radius).

## Verificación

Proyecto sin infraestructura de tests; verificación manual en el editor:

1. Generar mapa: arranca con un nodo `Normal` activo, entorno revelado, resto en niebla.
2. Clic en adyacente → cambia el activo, la niebla retrocede, lo explorado persiste.
3. Clic en nodo lejano o fuera del mapa → sin efecto.
4. Escena oscura con nodos emisivos brillando (Bloom); nodos en niebla apagados.
5. `momentum = 0.7`, `brushRadius = 1`, `holeChance = 0` → mapa tipo gusano.
6. `momentum = 0` → mapas indistinguibles de los actuales.
