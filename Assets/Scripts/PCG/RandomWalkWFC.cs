using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class ForbiddenPair
{
    public CellType a;
    public CellType b;
    public ForbiddenPair(CellType a, CellType b) { this.a = a; this.b = b; }
}

[Serializable]
public class TypeCap
{
    public CellType type;
    public int max;
    public TypeCap(CellType t, int m) { type = t; max = m; }
}

public class RandomWalkWFC : MonoBehaviour
{
    [Header("General Parameters")]
    [SerializeField] private int steps = 60;         
    [SerializeField, Range(0, 2)] private int brushRadius = 1;  
    [SerializeField, Range(0f, 0.3f)] private float holeChance = 0.08f; 
    [SerializeField] private float triangleSide = 1f;
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
    private TileWFC[] upTiles;
    private TileWFC[] downTiles;
    private bool[,] compatible;                
    private Dictionary<CellType, int> typeCount;
    private Dictionary<CellType, int> typeMax;
    private Queue<CellWFC> propagationQueue = new Queue<CellWFC>();
    private bool IsGenerating;
    private int Iteration, MaxIteration, retries;

    public event Action OnGenerationComplete;

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generar")]
    public void Generate()
    {
        retries = 0;
        BuildRules();
        BuildTileSets();
        domain = CarveDomain();  
        StartWave();             
    }

    private static bool PointingUp(Vector2Int p) => ((p.x + p.y) & 1) == 0;

    private static IEnumerable<Vector2Int> NeighborCoords(Vector2Int p)
    {
        yield return new Vector2Int(p.x, p.y - 1);
        yield return new Vector2Int(p.x, p.y + 1);
        yield return PointingUp(p) ? new Vector2Int(p.x + 1, p.y)
                                   : new Vector2Int(p.x - 1, p.y);
    }

    private HashSet<Vector2Int> CarveDomain()
    {
        var cells = new HashSet<Vector2Int>();
        Vector2Int head = Vector2Int.zero;

        for (int s = 0; s < steps; s++)
        {
            CarveBrush(cells, head);
            var options = NeighborCoords(head).ToList();
            head = options[Random.Range(0, options.Count)]; 
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

    private void BuildTileSets()
    {
        var tiles = AvailableTiles.Where(t => t != null).ToArray();
        upTiles   = tiles.Where(t => t.orientation == TriOrientation.Up).ToArray();
        downTiles = tiles.Where(t => t.orientation == TriOrientation.Down).ToArray();

        if (upTiles.Length == 0 || downTiles.Length == 0)
            Debug.LogError("Faltan teselas de una orientación (▲ o ▼).");
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
                row = p.x,
                col = p.y,
                collapsed = false,
                tileOptions = PointingUp(p) ? upTiles : downTiles,
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
        typeCount.TryGetValue(tile.type, out int count);
        typeCount[tile.type] = ++count;
        if (typeMax.TryGetValue(tile.type, out int max) && count >= max)
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

        if (retries++ < maxRetries)
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

    private Vector3 BandCenter(int row, int col)
    {
        float s = triangleSide;
        float h = s * 0.8660254f;
        return new Vector3(col * (s * 0.5f), 0f, -(row + 0.5f) * h);
    }

    private void InstantiateCollapsedCells()
    {
        foreach (var kv in grid)
        {
            CellWFC cell = kv.Value;
            if (!cell.collapsed || cell.instantiated) continue;

            bool up = PointingUp(kv.Key);
            Quaternion rot = up ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            var inst = Instantiate(cell.selectedTile, Vector3.zero, rot, transform);

            var r = inst.GetComponentInChildren<Renderer>();
            Vector3 delta = BandCenter(cell.row, cell.col) - r.bounds.center;
            delta.y = 0f; 
            inst.transform.position += delta;

            var tc = inst.gameObject.GetComponent<TriangleCell3D>();
            if (tc == null) tc = inst.gameObject.AddComponent<TriangleCell3D>();
            tc.row = cell.row;
            tc.col = cell.col;
            tc.pointingUp = up;
            tc.type = cell.selectedTile.type;

            cell.instantiated = true;

            Debug.Log($"({cell.row},{cell.col}) up={up} size={r.bounds.size}");
        }
    }

    private void ClearGridObjects()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(transform.childCount - 1).gameObject);
    }
}