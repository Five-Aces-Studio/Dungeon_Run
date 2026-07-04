using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomWalkWFCHex : MonoBehaviour
{
    [Header("General Parameters")]
    [SerializeField] private int steps = 60;
    [SerializeField, Range(0, 2)] private int brushRadius = 1;
    [SerializeField, Range(0f, 0.3f)] private float holeChance = 0.08f;
    [SerializeField] private float radius = 1f; 
    [SerializeField] private bool progressive = true;

    [Header("Tiles")]
    public List<TileWFC> AvailableTiles = new List<TileWFC>();

    [Header("Forbidden neighbors")]
    public List<ForbiddenPair> forbidden = new List<ForbiddenPair>
    {
        new ForbiddenPair(CellType.Shop,  CellType.Shop),
        new ForbiddenPair(CellType.Shop,  CellType.Combat),
        new ForbiddenPair(CellType.Item,  CellType.Item),
        new ForbiddenPair(CellType.Event, CellType.Event),
    };

    [Header("Max cells")]
    public List<TypeCap> max = new List<TypeCap>
    {
        new TypeCap(CellType.Shop, 2),
        new TypeCap(CellType.Event, 4),
    };

    [SerializeField] private int maxRetries = 20;

    private Dictionary<Vector2Int, CellWFC> grid; 
    private HashSet<Vector2Int> domain;
    private TileWFC[] allTiles;
    private bool[,] compatible;
    private Dictionary<CellType, int> typeCount;
    private Dictionary<CellType, int> typeMax;
    private Queue<CellWFC> propagationQueue = new Queue<CellWFC>();
    private bool IsGenerating;
    private int Iteration, MaxIteration, retries;
    private bool sizeChecked;

    public event Action OnGenerationComplete;

    private static readonly Vector2Int[] HexDirs =
    {
        new Vector2Int( 1,  0), new Vector2Int(-1,  0),
        new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        new Vector2Int( 1, -1), new Vector2Int(-1,  1),
    };

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generar")]
    public void Generate()
    {
        retries = 0;
        sizeChecked = false;
        BuildRules();
        BuildTileSet();
        domain = CarveDomain();
        StartWave();
    }

    private static IEnumerable<Vector2Int> NeighborCoords(Vector2Int p)
    {
        for (int i = 0; i < 6; i++)
            yield return p + HexDirs[i];
    }

    private HashSet<Vector2Int> CarveDomain()
    {
        var cells = new HashSet<Vector2Int>();
        Vector2Int head = Vector2Int.zero;

        for (int s = 0; s < steps; s++)
        {
            CarveBrush(cells, head);
            head += HexDirs[Random.Range(0, 6)]; 
        }
        CarveBrush(cells, head);

        if (holeChance > 0f)
        {
            foreach (var c in cells.ToList())
            {
                bool interior = NeighborCoords(c).All(cells.Contains);
                if (interior && Random.value < holeChance)
                    cells.Remove(c);
            }
        }

        Debug.Log($"Dominio tallado: {cells.Count} celdas");
        return cells;
    }

    private void CarveBrush(HashSet<Vector2Int> cells, Vector2Int center)
    {
        var frontier = new Queue<(Vector2Int p, int d)>();
        var seen = new HashSet<Vector2Int> { center };
        frontier.Enqueue((center, 0));

        while (frontier.Count > 0)
        {
            var (p, d) = frontier.Dequeue();
            cells.Add(p);
            if (d == brushRadius) continue;
            foreach (var n in NeighborCoords(p))
                if (seen.Add(n))
                    frontier.Enqueue((n, d + 1));
        }
    }

    private void BuildRules()
    {
        int n = Enum.GetValues(typeof(CellType)).Length;
        compatible = new bool[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                compatible[i, j] = true;

        foreach (var f in forbidden)
        {
            compatible[(int)f.a, (int)f.b] = false;
            compatible[(int)f.b, (int)f.a] = false;
        }

        typeMax = new Dictionary<CellType, int>();
        foreach (var c in max)
            if (c.max > 0) typeMax[c.type] = c.max;
    }

    private void BuildTileSet()
    {
        allTiles = AvailableTiles.Where(t => t != null).ToArray();
        if (allTiles.Length == 0)
            Debug.LogError("AvailableTiles está vacío.");
    }

    private void StartWave()
    {
        ClearGridObjects();
        propagationQueue.Clear();
        typeCount = new Dictionary<CellType, int>();

        grid = new Dictionary<Vector2Int, CellWFC>();
        foreach (var p in domain)
        {
            grid[p] = new CellWFC
            {
                row = p.x, // q
                col = p.y, // r
                collapsed = false,
                tileOptions = allTiles,
                selectedTile = null
            };
        }

        Iteration = 0;
        MaxIteration = grid.Count;
        IsGenerating = true;

        Collapse(grid[grid.Keys.First()]);
    }

    private void Update()
    {
        if (IsGenerating) WaveStep();
    }

    private void WaveStep()
    {
        if (Iteration >= MaxIteration) return;
        Iteration++;

        if (progressive) InstantiateCollapsedCells();

        Propagate();
        if (!IsGenerating) return;

        CellWFC nextCell = FindLowestEntropy();

        if (nextCell != null)
        {
            Collapse(nextCell);
            Propagate();
        }
        else
        {
            IsGenerating = false;
            InstantiateCollapsedCells();
            Debug.Log("Generación completa");
            OnGenerationComplete?.Invoke();
        }
    }

    private IEnumerable<CellWFC> GetNeighbors(CellWFC cell)
    {
        foreach (var n in NeighborCoords(new Vector2Int(cell.row, cell.col)))
            if (grid.TryGetValue(n, out var c))
                yield return c;
    }

    private bool Compatible(TileWFC a, TileWFC b) =>
        compatible[(int)a.type, (int)b.type];

    private CellWFC FindLowestEntropy()
    {
        CellWFC best = null;
        float lowest = float.PositiveInfinity;

        foreach (var cell in grid.Values)
        {
            if (cell.collapsed) continue;
            float entropy = CalculateEntropy(cell.tileOptions) + Random.value * 1e-4f;
            if (entropy < lowest)
            {
                lowest = entropy;
                best = cell;
            }
        }
        return best;
    }

    private float CalculateEntropy(TileWFC[] tiles)
    {
        int sumOfWeights = tiles.Sum(t => t.Weight);
        float logSum = tiles.Sum(t => t.Weight * math.log(t.Weight));
        return math.log(sumOfWeights) - (logSum / sumOfWeights);
    }

    private void Collapse(CellWFC cell)
    {
        if (cell.tileOptions.Length == 0) { Fail(cell); return; }

        TileWFC tile = SelectTile(cell.tileOptions);
        cell.selectedTile = tile;
        cell.tileOptions = new[] { tile };
        cell.collapsed = true;
        propagationQueue.Enqueue(cell);

        int count;
        typeCount.TryGetValue(tile.type, out count);
        count = count + 1;
        typeCount[tile.type] = count;

        int max;
        if (typeMax.TryGetValue(tile.type, out max) && count >= max)
            BanTypeEverywhere(tile.type);
    }

    private void BanTypeEverywhere(CellType t)
    {
        foreach (var cell in grid.Values)
        {
            if (cell.collapsed) continue;
            int before = cell.tileOptions.Length;
            cell.tileOptions = cell.tileOptions.Where(x => x.type != t).ToArray();

            if (cell.tileOptions.Length == 0) { Fail(cell); return; }
            if (cell.tileOptions.Length < before) propagationQueue.Enqueue(cell);
        }
    }

    private TileWFC SelectTile(TileWFC[] tiles)
    {
        if (tiles.Length == 1) return tiles[0];

        int totalWeight = tiles.Sum(t => t.Weight);
        int random = Random.Range(0, totalWeight);
        int acumulative = 0;

        foreach (var tile in tiles)
        {
            acumulative += tile.Weight;
            if (random < acumulative)
                return tile;
        }
        return tiles[tiles.Length - 1];
    }

    private void Propagate()
    {
        while (propagationQueue.Count > 0)
        {
            CellWFC cell = propagationQueue.Dequeue();

            foreach (var neighbor in GetNeighbors(cell))
            {
                if (neighbor.collapsed) continue;

                int before = neighbor.tileOptions.Length;
                neighbor.tileOptions = FilterTiles(neighbor, cell);

                if (neighbor.tileOptions.Length == 0) { Fail(neighbor); return; }

                if (neighbor.tileOptions.Length == 1)
                    Collapse(neighbor);
                else if (neighbor.tileOptions.Length < before)
                    propagationQueue.Enqueue(neighbor);
            }
        }
    }

    private TileWFC[] FilterTiles(CellWFC neighbor, CellWFC currentCell)
    {
        List<TileWFC> valid = new List<TileWFC>();

        foreach (TileWFC nt in neighbor.tileOptions)
            foreach (TileWFC ct in currentCell.tileOptions)
                if (Compatible(nt, ct)) { valid.Add(nt); break; }

        return valid.ToArray();
    }

    private void Fail(CellWFC cell)
    {
        propagationQueue.Clear();

        retries = retries + 1;
        if (retries <= maxRetries)
        {
            Debug.LogWarning($"Contradicción en ({cell.row},{cell.col}). Reintento {retries}/{maxRetries}");
            StartWave();
        }
        else
        {
            IsGenerating = false;
            Debug.LogError("WFC falló tras agotar los reintentos. Revisa reglas/topes.");
        }
    }

    private Vector3 HexCenter(int q, int r)
    {
        float w = radius * 1.7320508f;
        return new Vector3(w * (q + r * 0.5f), 0f, -1.5f * radius * r);
    }

    private void InstantiateCollapsedCells()
    {
        foreach (var kv in grid)
        {
            CellWFC cell = kv.Value;
            if (!cell.collapsed || cell.instantiated) continue;

            var inst = Instantiate(cell.selectedTile, Vector3.zero, Quaternion.identity, transform);

            var rend = inst.GetComponentInChildren<Renderer>();
            Vector3 delta = HexCenter(cell.row, cell.col) - rend.bounds.center;
            delta.y = 0f;
            inst.transform.position += delta;

            if (!sizeChecked)
            {
                sizeChecked = true;
                Vector3 sz = rend.bounds.size;
                float expX = radius * 1.7320508f, expZ = radius * 2f;
                if (Mathf.Abs(sz.x - expX) > 0.02f || Mathf.Abs(sz.z - expZ) > 0.02f)
                    Debug.LogWarning($"Hex mal dimensionado: size={sz}, esperado ({expX:F2}, *, {expZ:F2}). ¿radius desincronizado entre HexWFC y ProceduralHexVisual?");
            }

            var hc = inst.gameObject.GetComponent<HexCell3D>();
            if (hc == null) hc = inst.gameObject.AddComponent<HexCell3D>();
            hc.q = cell.row;
            hc.r = cell.col;
            hc.type = cell.selectedTile.type;

            cell.instantiated = true;
        }
    }

    private void ClearGridObjects()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(transform.childCount - 1).gameObject);
    }

    private void OnDrawGizmos()
    {
        if (grid == null) return;
        Gizmos.color = Color.green;

        foreach (var kv in grid)
        {
            Vector3 c = HexCenter(kv.Key.x, kv.Key.y);
            for (int k = 0; k < 6; k++)
            {
                float a0 = (60f * k + 30f) * Mathf.Deg2Rad;
                float a1 = (60f * (k + 1) + 30f) * Mathf.Deg2Rad;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}