# Navegación por nodos, niebla de guerra y escena nocturna — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Navegación por clic entre nodos hex adyacentes con niebla de guerra permanente, escena nocturna con nodos emisivos y parámetro de momentum para mapas tipo «gusano».

**Architecture:** El generador `RandomWalkWFCHex` no cambia salvo el momentum del walk. Un nuevo `HexMapNavigator` (mismo GameObject) escucha `OnGenerationComplete`, indexa los `HexCell3D`, gestiona el nodo activo/clics/niebla y delega el aspecto por celda en `HexCellVisual` (MaterialPropertyBlock). `NightSceneSetup` oscurece la escena y crea el Volume de Bloom en runtime. `CameraFollowNode` (en la cámara) sigue al nodo activo.

**Tech Stack:** Unity 6000.3.19f1, URP 17.3, Input System 1.19 (exclusivo), C#.

**Regla del repo:** NO hacer `git commit` en ningún paso — el usuario commitea él mismo.

**Spec:** `docs/superpowers/specs/2026-07-09-hex-map-navigation-fog-design.md`

**GUIDs asignados a los scripts nuevos** (los `.meta` se escriben a mano para poder referenciarlos desde la escena):

| Script | GUID |
|---|---|
| `HexCellVisual.cs` | `e1b7c0a95bf14d73aa4c140a15dbaacb` |
| `HexMapNavigator.cs` | `322871ffbbc64c79be88cbbd20cb7d2c` |
| `CameraFollowNode.cs` | `31a2d57f7c4a448fb41c62958c017906` |
| `NightSceneSetup.cs` | `05003a04954548f78b5667ecc6e85654` |

**Verificación:** el proyecto no tiene infraestructura de tests. La verificación de cada tarea es (a) revisión del código escrito y (b) compilación batchmode de Unity al final (Tarea 8) + checklist manual en el editor.

---

### Task 1: Momentum en el Random Walk

**Files:**
- Modify: `Assets/Scripts/PCG/RandomWalkWFCHex.cs`

- [ ] **Step 1: Añadir el campo `momentum`**

Tras la línea `[SerializeField, Range(0f, 0.3f)] private float holeChance = 0.08f;` añadir:

```csharp
[SerializeField, Range(0f, 1f)] private float momentum = 0f;
```

- [ ] **Step 2: Usar momentum en `CarveDomain` y añadir `NextWalkDirection`**

Reemplazar el bucle del walk en `CarveDomain()`:

```csharp
private HashSet<Vector2Int> CarveDomain()
{
    var cells = new HashSet<Vector2Int>();
    Vector2Int head = Vector2Int.zero;
    int lastDir = -1;

    for (int s = 0; s < steps; s++)
    {
        CarveBrush(cells, head);
        lastDir = NextWalkDirection(lastDir);
        head += HexDirs[lastDir];
    }
    CarveBrush(cells, head);
    // ... (el bloque de holeChance no cambia)
```

Añadir el método (junto a `CarveBrush`). Nota: en `HexDirs` las direcciones opuestas están emparejadas (0↔1, 2↔3, 4↔5), por eso la opuesta es `lastDir ^ 1`. El truco `Random.Range(0, 5)` + incremento elige uniformemente entre las 5 direcciones restantes sin bucle:

```csharp
private int NextWalkDirection(int lastDir)
{
    if (lastDir < 0 || momentum <= 0f)
        return Random.Range(0, 6);

    if (Random.value < momentum)
        return lastDir;

    int opposite = lastDir ^ 1;
    int dir = Random.Range(0, 5);
    if (dir >= opposite) dir++;
    return dir;
}
```

- [ ] **Step 3: Revisar que con `momentum = 0` el flujo es idéntico al original** (entra siempre por `Random.Range(0, 6)`).

---

### Task 2: `HexCellVisual` (estados visuales con MaterialPropertyBlock)

**Files:**
- Create: `Assets/Scripts/PCG/HexCellVisual.cs`
- Create: `Assets/Scripts/PCG/HexCellVisual.cs.meta`

- [ ] **Step 1: Crear el script completo**

```csharp
using UnityEngine;

public enum CellVisualState { Fog, Penumbra, Revealed, Adjacent, Active }

[DisallowMultipleComponent]
public class HexCellVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color FogBaseColor = new Color(0.04f, 0.045f, 0.06f);

    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private Color baseColor = Color.white;
    private Color emissionColor = Color.white;
    private CellVisualState state = CellVisualState.Fog;

    public CellVisualState State => state;

    public void Configure(Color emission)
    {
        CacheRenderer();
        emissionColor = emission;
        Apply();
    }

    public void SetState(CellVisualState newState)
    {
        state = newState;
        Apply();
    }

    private void CacheRenderer()
    {
        if (rend != null) return;
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
            baseColor = rend.sharedMaterial.GetColor(BaseColorId);
    }

    private void Update()
    {
        if (state != CellVisualState.Active || rend == null) return;
        float pulse = 3f + Mathf.Sin(Time.time * 3f) * 0.75f;
        ApplyColors(baseColor, pulse);
    }

    private void Apply()
    {
        if (rend == null) return;
        switch (state)
        {
            case CellVisualState.Active:   ApplyColors(baseColor, 3f); break;
            case CellVisualState.Adjacent: ApplyColors(baseColor, 1.5f); break;
            case CellVisualState.Revealed: ApplyColors(baseColor, 1f); break;
            case CellVisualState.Penumbra: ApplyColors(baseColor, 0.25f); break;
            default:                       ApplyColors(FogBaseColor, 0f); break;
        }
    }

    private void ApplyColors(Color baseCol, float emissionIntensity)
    {
        mpb.SetColor(BaseColorId, baseCol);
        mpb.SetColor(EmissionColorId, emissionColor * emissionIntensity);
        rend.SetPropertyBlock(mpb);
    }
}
```

- [ ] **Step 2: Crear `HexCellVisual.cs.meta`**

```yaml
fileFormatVersion: 2
guid: e1b7c0a95bf14d73aa4c140a15dbaacb
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

---

### Task 3: `CameraFollowNode`

**Files:**
- Create: `Assets/Scripts/PCG/CameraFollowNode.cs`
- Create: `Assets/Scripts/PCG/CameraFollowNode.cs.meta` (guid `31a2d57f7c4a448fb41c62958c017906`, misma plantilla que en Task 2)

- [ ] **Step 1: Crear el script completo**

En el primer `SetTarget` la cámara salta directamente a su sitio y se orienta mirando al nodo (la posición inicial de la cámara en la escena da igual); después se mueve con `SmoothDamp`:

```csharp
using UnityEngine;

public class CameraFollowNode : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
    [SerializeField] private float smoothTime = 0.35f;

    private Vector3 target;
    private Vector3 velocity;
    private bool hasTarget;

    public void SetTarget(Vector3 worldPosition)
    {
        target = worldPosition;
        if (!hasTarget)
        {
            hasTarget = true;
            transform.position = target + offset;
            transform.rotation = Quaternion.LookRotation(-offset);
        }
    }

    private void LateUpdate()
    {
        if (!hasTarget) return;
        transform.position = Vector3.SmoothDamp(transform.position, target + offset, ref velocity, smoothTime);
    }
}
```

- [ ] **Step 2: Crear el `.meta`** con la plantilla de Task 2 y guid `31a2d57f7c4a448fb41c62958c017906`.

---

### Task 4: `NightSceneSetup`

**Files:**
- Create: `Assets/Scripts/PCG/NightSceneSetup.cs`
- Create: `Assets/Scripts/PCG/NightSceneSetup.cs.meta` (guid `05003a04954548f78b5667ecc6e85654`)

- [ ] **Step 1: Crear el script completo**

El Volume de Bloom se crea por código para no editar assets de VolumeProfile a mano. `renderPostProcessing = true` es imprescindible para que URP aplique el Bloom en la cámara:

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NightSceneSetup : MonoBehaviour
{
    [Header("Cielo y ambiente")]
    [SerializeField] private Color backgroundColor = new Color(0.02f, 0.024f, 0.04f);
    [SerializeField] private Color ambientColor = new Color(0.03f, 0.04f, 0.07f);

    [Header("Luz direccional")]
    [SerializeField, Range(0f, 1f)] private float directionalIntensity = 0.05f;
    [SerializeField] private Color directionalColor = new Color(0.55f, 0.65f, 1f);

    [Header("Bloom")]
    [SerializeField] private float bloomIntensity = 1.2f;
    [SerializeField] private float bloomThreshold = 0.9f;

    private void Awake()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;

        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.intensity = directionalIntensity;
            light.color = directionalColor;
        }

        var volumeGo = new GameObject("NightVolume");
        volumeGo.transform.SetParent(transform, false);
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>();
        bloom.intensity.overrideState = true;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = bloomThreshold;
        volume.profile = profile;
    }
}
```

- [ ] **Step 2: Crear el `.meta`** con la plantilla de Task 2 y guid `05003a04954548f78b5667ecc6e85654`.

---

### Task 5: `HexMapNavigator`

**Files:**
- Create: `Assets/Scripts/PCG/HexMapNavigator.cs`
- Create: `Assets/Scripts/PCG/HexMapNavigator.cs.meta` (guid `322871ffbbc64c79be88cbbd20cb7d2c`)

- [ ] **Step 1: Crear el script completo**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class CellTypeEmission
{
    public CellType type;
    [ColorUsage(false, true)] public Color color = Color.white;
}

[RequireComponent(typeof(RandomWalkWFCHex))]
public class HexMapNavigator : MonoBehaviour
{
    [Header("Niebla")]
    [SerializeField, Min(1)] private int revealRadius = 2;

    [Header("Nodo inicial")]
    [SerializeField] private int maxStartRetries = 5;

    [Header("Emisión por tipo")]
    [SerializeField] private List<CellTypeEmission> emissionColors = new List<CellTypeEmission>
    {
        new CellTypeEmission { type = CellType.Normal, color = new Color(1f, 0.95f, 0.75f) },
        new CellTypeEmission { type = CellType.Combat, color = new Color(1f, 0.25f, 0.2f) },
        new CellTypeEmission { type = CellType.Item,   color = new Color(1f, 0.8f, 0.2f) },
        new CellTypeEmission { type = CellType.Event,  color = new Color(0.5f, 0.4f, 1f) },
        new CellTypeEmission { type = CellType.Shop,   color = new Color(0.2f, 1f, 0.6f) },
    };

    private static readonly Vector2Int[] HexDirs =
    {
        new Vector2Int( 1,  0), new Vector2Int(-1,  0),
        new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        new Vector2Int( 1, -1), new Vector2Int(-1,  1),
    };

    private RandomWalkWFCHex generator;
    private CameraFollowNode cameraFollow;
    private readonly Dictionary<Vector2Int, HexCell3D> cells = new Dictionary<Vector2Int, HexCell3D>();
    private readonly HashSet<Vector2Int> revealed = new HashSet<Vector2Int>();
    private Vector2Int active;
    private bool ready;
    private int startRetries;

    private void Awake()
    {
        generator = GetComponent<RandomWalkWFCHex>();
        if (Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollowNode>();
    }

    private void OnEnable() => generator.OnGenerationComplete += Init;
    private void OnDisable() => generator.OnGenerationComplete -= Init;

    private void Init()
    {
        ready = false;
        cells.Clear();
        revealed.Clear();

        foreach (var cell in GetComponentsInChildren<HexCell3D>())
        {
            var coord = new Vector2Int(cell.q, cell.r);
            cells[coord] = cell;

            var visual = cell.GetComponent<HexCellVisual>();
            if (visual == null) visual = cell.gameObject.AddComponent<HexCellVisual>();
            visual.Configure(EmissionFor(cell.type));
            visual.SetState(CellVisualState.Fog);
        }

        if (!TryFindStart(out Vector2Int start))
        {
            startRetries++;
            if (startRetries <= maxStartRetries)
            {
                Debug.LogWarning($"Sin celda Normal en el mapa; regenerando ({startRetries}/{maxStartRetries})");
                generator.Generate();
            }
            else
            {
                Debug.LogError("No se encontró ninguna celda Normal tras agotar los reintentos. Sube el peso del tile Normal.");
            }
            return;
        }

        startRetries = 0;
        active = start;
        ready = true;
        Reveal(active);
        RefreshStates();
        MoveCameraToActive();
    }

    private bool TryFindStart(out Vector2Int best)
    {
        best = default;
        int bestDist = int.MaxValue;
        bool found = false;

        foreach (var kv in cells)
        {
            if (kv.Value.type != CellType.Normal) continue;
            int dist = HexDistance(kv.Key, Vector2Int.zero);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = kv.Key;
                found = true;
            }
        }
        return found;
    }

    private void Update()
    {
        if (!ready) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        var cell = hit.collider.GetComponentInParent<HexCell3D>();
        if (cell == null) return;

        var coord = new Vector2Int(cell.q, cell.r);
        if (HexDistance(coord, active) != 1) return;

        active = coord;
        Reveal(active);
        RefreshStates();
        MoveCameraToActive();
    }

    private void Reveal(Vector2Int center)
    {
        foreach (var coord in cells.Keys)
            if (HexDistance(coord, center) < revealRadius)
                revealed.Add(coord);
    }

    private void RefreshStates()
    {
        foreach (var kv in cells)
        {
            var visual = kv.Value.GetComponent<HexCellVisual>();
            if (visual == null) continue;

            int dist = HexDistance(kv.Key, active);
            CellVisualState state;
            if (kv.Key == active) state = CellVisualState.Active;
            else if (dist == 1) state = CellVisualState.Adjacent;
            else if (revealed.Contains(kv.Key)) state = CellVisualState.Revealed;
            else if (dist == revealRadius) state = CellVisualState.Penumbra;
            else state = CellVisualState.Fog;

            visual.SetState(state);
        }
    }

    private void MoveCameraToActive()
    {
        if (cameraFollow == null || !cells.TryGetValue(active, out var cell)) return;
        var rend = cell.GetComponentInChildren<Renderer>();
        Vector3 target = rend != null ? rend.bounds.center : cell.transform.position;
        cameraFollow.SetTarget(target);
    }

    private Color EmissionFor(CellType type)
    {
        foreach (var e in emissionColors)
            if (e.type == type) return e.color;
        return Color.white;
    }

    private static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }
}
```

- [ ] **Step 2: Crear el `.meta`** con la plantilla de Task 2 y guid `322871ffbbc64c79be88cbbd20cb7d2c`.

---

### Task 6: Activar emisión en los 4 materiales

**Files:**
- Modify: `Assets/Materials/Black.mat`
- Modify: `Assets/Materials/Blue.mat`
- Modify: `Assets/Materials/Red.mat`
- Modify: `Assets/Materials/Yellow.mat`

`MaterialPropertyBlock` no puede activar keywords de shader, así que `_EMISSION` debe quedar activo en el asset. En cada `.mat`:

- [ ] **Step 1:** Cambiar `m_ValidKeywords: []` por:

```yaml
  m_ValidKeywords:
  - _EMISSION
```

- [ ] **Step 2:** Cambiar `m_LightmapFlags: 4` por `m_LightmapFlags: 2` (deja de marcar la emisión como negra para GI; convención de Unity al activar emisión).

- [ ] **Step 3:** Verificar con grep que los 4 materiales contienen `- _EMISSION` y ninguno conserva `m_ValidKeywords: []`.

El color `_EmissionColor: {r: 0, g: 0, b: 0, a: 1}` NO se toca: el valor por instancia lo pone el MPB.

---

### Task 7: Integración en `SceneGuilleHex.unity`

**Files:**
- Modify: `Assets/Scenes/SceneGuille/SceneGuilleHex.unity`

fileIDs nuevos (no colisionan con los existentes): `537071909` (HexMapNavigator), `537071910` (NightSceneSetup), `508390969` (CameraFollowNode).

- [ ] **Step 1:** En el GameObject `RandomWalkWFC` (fileID `537071906`), ampliar `m_Component`:

```yaml
  m_Component:
  - component: {fileID: 537071907}
  - component: {fileID: 537071908}
  - component: {fileID: 537071909}
  - component: {fileID: 537071910}
```

- [ ] **Step 2:** Tras el bloque `--- !u!114 &537071908` (el MonoBehaviour de RandomWalkWFCHex, termina en `maxRetries: 20`), insertar:

```yaml
--- !u!114 &537071909
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 537071906}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 322871ffbbc64c79be88cbbd20cb7d2c, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::HexMapNavigator
--- !u!114 &537071910
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 537071906}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 05003a04954548f78b5667ecc6e85654, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::NightSceneSetup
```

(Los campos serializados omitidos toman los valores por defecto del script.)

- [ ] **Step 3:** En el GameObject `Main Camera` (fileID `508390965`), ampliar `m_Component`:

```yaml
  m_Component:
  - component: {fileID: 508390968}
  - component: {fileID: 508390967}
  - component: {fileID: 508390966}
  - component: {fileID: 508390969}
```

- [ ] **Step 4:** Tras el bloque `--- !u!81 &508390966` (AudioListener), insertar:

```yaml
--- !u!114 &508390969
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 508390965}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 31a2d57f7c4a448fb41c62958c017906, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::CameraFollowNode
```

---

### Task 8: Verificación

- [ ] **Step 1: Compilación batchmode** (solo si el proyecto NO está abierto en el editor de Unity; si está abierto fallará con «another Unity instance is running»):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -quit -projectPath "c:\Users\USUARIO\OneDrive\Escritorio\Dungeon_Run" -logFile "compile_check.log"
```

Después: buscar `error CS` en `compile_check.log`. Esperado: sin coincidencias. Borrar el log al terminar. Si el proyecto está abierto en Unity, la alternativa es que el usuario mire la consola del editor tras la recompilación automática.

- [ ] **Step 2: Checklist manual en el editor (usuario):**

1. Play en `SceneGuilleHex`: al terminar la generación hay un nodo activo tipo Normal pulsando, vecinos resaltados, entorno revelado, resto en niebla oscura sin emisión.
2. Fondo casi negro y nodos revelados brillando con Bloom.
3. Clic en un nodo adyacente → pasa a activo, la niebla retrocede, lo explorado sigue visible; la cámara le sigue suave.
4. Clic en nodos lejanos o fuera del mapa → sin efecto.
5. `momentum = 0.7`, `brushRadius = 1`, `holeChance = 0` en el inspector → el mapa sale alargado tipo gusano.
