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

[RequireComponent(typeof(RandomWalkWFC))]
public class HexMapNavigator : MonoBehaviour
{
    [Header("Niebla")]
    [SerializeField, Min(1)] private int revealRadius = 2;

    [Header("Nodo inicial")]
    [SerializeField] private int maxStartRetries = 5;

    [Header("Emisión")]
    [SerializeField, Range(0f, 5f)] private float emissionIntensity = 1.2f;
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

    private RandomWalkWFC generator;
    private CameraFollowNode cameraFollow;
    private FogParticles fogParticles;
    private ActiveNodeMarker activeMarker;
    private readonly Dictionary<Vector2Int, HexCell> cells = new Dictionary<Vector2Int, HexCell>();
    private readonly HashSet<Vector2Int> revealed = new HashSet<Vector2Int>();
    private Vector2Int active;
    private bool ready;
    private int startRetries;

    private void Awake()
    {
        generator = GetComponent<RandomWalkWFC>();
        fogParticles = GetComponent<FogParticles>();
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
        if (fogParticles != null) fogParticles.ResetFog();

        foreach (HexCell cell in GetComponentsInChildren<HexCell>())
        {
            Vector2Int coord = new Vector2Int(cell.q, cell.r);
            cells[coord] = cell;

            HexCellVisual visual = cell.GetComponent<HexCellVisual>();
            if (visual == null) visual = cell.gameObject.AddComponent<HexCellVisual>();
            visual.Configure(EmissionFor(cell.type), emissionIntensity);
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
        UpdateActiveMarker();
        MoveCameraToActive();
    }

    private bool TryFindStart(out Vector2Int best)
    {
        best = default;
        int bestDist = int.MaxValue;
        bool found = false;

        foreach (KeyValuePair<Vector2Int, HexCell> kv in cells)
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
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        HexCell cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null) return;

        Vector2Int coord = new Vector2Int(cell.q, cell.r);
        if (HexDistance(coord, active) != 1) return;

        active = coord;
        Reveal(active);
        RefreshStates();
        UpdateActiveMarker();
        MoveCameraToActive();
    }

    private void Reveal(Vector2Int center)
    {
        foreach (Vector2Int coord in cells.Keys)
            if (HexDistance(coord, center) < revealRadius)
                revealed.Add(coord);
    }

    private void RefreshStates()
    {
        List<FogParticles.FogPoint> fogPoints = new List<FogParticles.FogPoint>();

        foreach (KeyValuePair<Vector2Int, HexCell> kv in cells)
        {
            HexCellVisual visual = kv.Value.GetComponent<HexCellVisual>();
            if (visual == null) continue;

            int dist = HexDistance(kv.Key, active);
            CellVisualState state;
            if (kv.Key == active) state = CellVisualState.Active;
            else if (dist == 1) state = CellVisualState.Adjacent;
            else if (revealed.Contains(kv.Key)) state = CellVisualState.Revealed;
            else if (dist == revealRadius) state = CellVisualState.Penumbra;
            else state = CellVisualState.Fog;

            visual.SetState(state);

            if (state == CellVisualState.Fog || state == CellVisualState.Penumbra)
            {
                Renderer rend = kv.Value.GetComponentInChildren<Renderer>();
                fogPoints.Add(new FogParticles.FogPoint
                {
                    position = rend != null ? rend.bounds.center : kv.Value.transform.position,
                    density = state == CellVisualState.Fog ? 1f : 0.5f
                });
            }
        }

        if (fogParticles != null) fogParticles.SetCoverage(fogPoints);
    }

    private void UpdateActiveMarker()
    {
        if (!cells.TryGetValue(active, out HexCell cell)) return;
        Renderer rend = cell.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        if (activeMarker == null)
        {
            GameObject go = new GameObject("ActiveNodeMarker");
            go.transform.SetParent(transform, false);
            activeMarker = go.AddComponent<ActiveNodeMarker>();
        }

        float hexRadius = rend.bounds.size.z * 0.5f;
        Vector3 top = rend.bounds.center + Vector3.up * rend.bounds.extents.y;
        activeMarker.Show(top, hexRadius);
    }

    private void MoveCameraToActive()
    {
        if (cameraFollow == null || !cells.TryGetValue(active, out HexCell cell)) return;
        Renderer rend = cell.GetComponentInChildren<Renderer>();
        Vector3 target = rend != null ? rend.bounds.center : cell.transform.position;
        cameraFollow.SetTarget(target);
    }

    private Color EmissionFor(CellType type)
    {
        foreach (CellTypeEmission e in emissionColors)
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
