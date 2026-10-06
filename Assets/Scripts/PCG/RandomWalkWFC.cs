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
public class ForbiddenMax
{
    public CellType type;
    public int max;
    public ForbiddenMax(CellType t, int m) { type = t; max = m; }
}

public class RandomWalkWFC : MonoBehaviour
{
    [Header("General Parameters")]
    [SerializeField] private int steps = 60;
    [SerializeField, Range(0, 2)] private int brushRadius = 1;
    [SerializeField, Range(0f, 0.3f)] private float holeChance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float wormness = 0f;
    [SerializeField] private float radius = 1f;
    [SerializeField] private bool progressive = true;

    [Header("Maze Mode")]
    [SerializeField] private bool squareMaze;
    [SerializeField, Min(1)] private int width = 12;
    [SerializeField, Min(1)] private int height = 10;
    [SerializeField, Min(3f)] private float cellSize = 3f;
    [SerializeField] private int seed = 12345;
    [SerializeField, Range(0f, 1f)]
    private float extraPassageChance = 0.08f;

    public MazeLayout Layout { get; private set; }
    public bool IsSquareMaze => squareMaze;
    public float CellSize => cellSize;
    public bool HasGenerated { get; private set; }

    public IEnumerable<CellWFC> GeneratedCells =>
        grid == null ? Enumerable.Empty<CellWFC>() : grid.Values;

    public event Action OnGenerationStarted;

    private System.Random contentRandom;
    private bool waveFailed;


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
    public List<ForbiddenMax> max = new List<ForbiddenMax>
    {
        new ForbiddenMax(CellType.Shop, 2),
        new ForbiddenMax(CellType.Event, 4),
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

    public void Generate()
    {
        IsGenerating = false;
        HasGenerated = false;
        waveFailed = false;

        OnGenerationStarted?.Invoke();

        ClearGridObjects();
        propagationQueue.Clear();
        grid = null;
        Layout = null;

        retries = 0;
        sizeChecked = false;

        if (squareMaze && (width < 1 || height < 1 || cellSize < 3f))
        {
            Debug.LogError("El laberinto necesita dimensiones positivas y Cell Size >= 3.");
            return;
        }

        BuildTileSet();

        if (allTiles.Length == 0 ||
            allTiles.Any(t => t.Weight <= 0) ||
            allTiles.Sum(t => (long)t.Weight) > int.MaxValue)
        {
            Debug.LogError("Revisa AvailableTiles: hacen falta opciones con pesos positivos.");
            return;
        }

        if (squareMaze && !allTiles.Any(t => t.type == CellType.Normal))
        {
            Debug.LogError("El laberinto necesita al menos una opción Normal para el inicio.");
            return;
        }

        BuildRules();

        contentRandom = new System.Random(unchecked(seed ^ 0x5F3759DF));

        if (squareMaze)
        {
            Layout = MazeLayout.Build(
                width, height, seed, extraPassageChance);

            domain = new HashSet<Vector2Int>(Layout.Cells);
        }
        else
        {
            domain = RandomWalk();
        }

        StartWave();
    }

    private static Vector2Int[] NeighborCoords(Vector2Int p)
    {
        Vector2Int[] neighbors = new Vector2Int[6];
        for (int i = 0; i < 6; i++)
            neighbors[i] = p + HexDirs[i];
        return neighbors;
    }

    private HashSet<Vector2Int> RandomWalk()
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        Vector2Int head = Vector2Int.zero;
        int lastDir = -1;

        for (int s = 0; s < steps; s++)
        {
            RandomWalkPaint(cells, head);
            lastDir = NextRandomWalkDirection(lastDir);
            head += HexDirs[lastDir];
        }
        RandomWalkPaint(cells, head);

        if (holeChance > 0f)
        {
            foreach (Vector2Int c in cells.ToList())
            {
                bool interior = NeighborCoords(c).All(cells.Contains);
                if (interior && Random.value < holeChance)
                    cells.Remove(c);
            }
        }

        return cells;
    }

    private int NextRandomWalkDirection(int lastDir)
    {
        if (lastDir < 0 || wormness <= 0f)
            return Random.Range(0, 6);

        if (Random.value < wormness)
            return lastDir;

        int opposite = lastDir ^ 1;
        int dir = Random.Range(0, 5);
        if (dir >= opposite) dir++;
        return dir;
    }

    private void RandomWalkPaint(HashSet<Vector2Int> cells, Vector2Int brushCenter)
    {
        Queue<(Vector2Int position, int distanceFromCenter)> pendingCells = new Queue<(Vector2Int position, int distanceFromCenter)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { brushCenter };
        pendingCells.Enqueue((brushCenter, 0));

        while (pendingCells.Count > 0)
        {
            (Vector2Int position, int distanceFromCenter) = pendingCells.Dequeue();
            cells.Add(position);
            if (distanceFromCenter == brushRadius) continue;
            foreach (Vector2Int neighbor in NeighborCoords(position))
                if (visited.Add(neighbor))
                    pendingCells.Enqueue((neighbor, distanceFromCenter + 1));
        }
    }

    private void BuildRules()
    {
        int n = Enum.GetValues(typeof(CellType)).Length;
        compatible = new bool[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                compatible[i, j] = true;

        foreach (ForbiddenPair f in forbidden)
        {
            compatible[(int)f.a, (int)f.b] = false;
            compatible[(int)f.b, (int)f.a] = false;
        }

        typeMax = new Dictionary<CellType, int>();
        foreach (ForbiddenMax c in max)
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

        waveFailed = false;
        typeCount = new Dictionary<CellType, int>();
        grid = new Dictionary<Vector2Int, CellWFC>();

        foreach (Vector2Int p in domain.OrderBy(p => p.y).ThenBy(p => p.x))
        {
            grid[p] = new CellWFC
            {
                q = p.x,
                r = p.y,
                collapsed = false,
                instantiated = false,
                tileOptions = allTiles,
                selectedTile = null
            };
        }

        IsGenerating = true;

        CellWFC first = squareMaze
            ? grid[Vector2Int.zero]
            : grid.Values.First();

        if (squareMaze)
        {
            first.tileOptions = allTiles
                .Where(t => t.type == CellType.Normal)
                .ToArray();
        }

        Collapse(first);
    }

    //private void Update()
    //{
    //    if (IsGenerating) WaveStep();
    //}

    private void Update()
    {
        if (!IsGenerating)
            return;

        if (waveFailed)
        {
            retries++;

            if (retries > maxRetries)
            {
                IsGenerating = false;
                Debug.LogError(
                    "WFC agotó sus reintentos. Revisa incompatibilidades y máximos.");
                return;
            }

            Debug.LogWarning($"Reintento WFC {retries}/{maxRetries}");
            StartWave();
            return;
        }

        WaveStep();
    }

    //private void WaveStep()
    //{
    //    if (Iteration >= MaxIteration) return;
    //    Iteration++;

    //    if (progressive) InstantiateCollapsedCells();

    //    Propagate();
    //    if (!IsGenerating) return;

    //    CellWFC nextCell = FindLowestEntropy();

    //    if (nextCell != null)
    //    {
    //        Collapse(nextCell);
    //        Propagate();
    //    }
    //    else
    //    {
    //        IsGenerating = false;
    //        InstantiateCollapsedCells();
    //        Debug.Log("Generación completa");
    //        OnGenerationComplete?.Invoke();
    //    }
    //}

    private void WaveStep()
    {
        Propagate();

        if (waveFailed)
            return;

        CellWFC next = FindLowestEntropy();

        if (next != null)
        {
            Collapse(next);

            if (waveFailed)
                return;

            Propagate();

            if (waveFailed)
                return;

            if (progressive && !squareMaze)
                InstantiateCollapsedCells();

            return;
        }

        IsGenerating = false;

        if (!squareMaze)
            InstantiateCollapsedCells();

        HasGenerated = true;

        Debug.Log("Generación completa");
        OnGenerationComplete?.Invoke();
    }

    //private List<CellWFC> GetNeighbors(CellWFC cell)
    //{
    //    List<CellWFC> neighbors = new List<CellWFC>();
    //    foreach (Vector2Int coord in NeighborCoords(new Vector2Int(cell.q, cell.r)))
    //        if (grid.TryGetValue(coord, out CellWFC neighbor))
    //            neighbors.Add(neighbor);
    //    return neighbors;
    //}
    private List<CellWFC> GetNeighbors(CellWFC cell)
    {
        Vector2Int position = new Vector2Int(cell.q, cell.r);

        IEnumerable<Vector2Int> coordinates = squareMaze
            ? Layout.Neighbors(position)
            : NeighborCoords(position);

        List<CellWFC> neighbors = new List<CellWFC>();

        foreach (Vector2Int coordinate in coordinates)
        {
            if (grid.TryGetValue(coordinate, out CellWFC neighbor))
                neighbors.Add(neighbor);
        }

        return neighbors;
    }

    private bool Compatible(TileWFC a, TileWFC b) =>
        compatible[(int)a.type, (int)b.type];

    //private CellWFC FindLowestEntropy()
    //{
    //    CellWFC best = null;
    //    float lowest = float.PositiveInfinity;

    //    foreach (CellWFC cell in grid.Values)
    //    {
    //        if (cell.collapsed) continue;
    //        float entropy = CalculateEntropy(cell.tileOptions) + Random.value * 1e-4f;
    //        if (entropy < lowest)
    //        {
    //            lowest = entropy;
    //            best = cell;
    //        }
    //    }
    //    return best;
    //}
    private CellWFC FindLowestEntropy()
    {
        CellWFC best = null;
        float lowest = float.PositiveInfinity;

        foreach (CellWFC cell in grid.Values
            .OrderBy(c => c.r)
            .ThenBy(c => c.q))
        {
            if (cell.collapsed)
                continue;

            float randomTieBreak = squareMaze
                ? (float)contentRandom.NextDouble()
                : Random.value;

            float entropy =
                CalculateEntropy(cell.tileOptions) + randomTieBreak * 1e-4f;

            if (entropy < lowest)
            {
                lowest = entropy;
                best = cell;
            }
        }

        return best;
    }

    private TileWFC SelectTile(TileWFC[] tiles)
    {
        if (tiles.Length == 1)
            return tiles[0];

        int totalWeight = tiles.Sum(t => t.Weight);

        int roll = squareMaze
            ? contentRandom.Next(totalWeight)
            : Random.Range(0, totalWeight);

        int accumulated = 0;

        foreach (TileWFC tile in tiles)
        {
            accumulated += tile.Weight;

            if (roll < accumulated)
                return tile;
        }

        return tiles[tiles.Length - 1];
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
            BanType(tile.type);
    }

    private void BanType(CellType t)
    {
        foreach (CellWFC cell in grid.Values)
        {
            if (cell.collapsed) continue;
            int before = cell.tileOptions.Length;
            cell.tileOptions = cell.tileOptions.Where(x => x.type != t).ToArray();

            if (cell.tileOptions.Length == 0) { Fail(cell); return; }
            if (cell.tileOptions.Length < before) propagationQueue.Enqueue(cell);
        }
    }

    //private TileWFC SelectTile(TileWFC[] tiles)
    //{
    //    if (tiles.Length == 1) return tiles[0];

    //    int totalWeight = tiles.Sum(t => t.Weight);
    //    int random = Random.Range(0, totalWeight);
    //    int acumulative = 0;

    //    foreach (TileWFC tile in tiles)
    //    {
    //        acumulative += tile.Weight;
    //        if (random < acumulative)
    //            return tile;
    //    }
    //    return tiles[tiles.Length - 1];
    //}

    //private void Propagate()
    //{
    //    while (propagationQueue.Count > 0)
    //    {
    //        CellWFC cell = propagationQueue.Dequeue();

    //        foreach (CellWFC neighbor in GetNeighbors(cell))
    //        {
    //            if (neighbor.collapsed) continue;

    //            int before = neighbor.tileOptions.Length;
    //            neighbor.tileOptions = FilterTiles(neighbor, cell);

    //            if (neighbor.tileOptions.Length == 0) { Fail(neighbor); return; }

    //            if (neighbor.tileOptions.Length == 1)
    //                Collapse(neighbor);
    //            else if (neighbor.tileOptions.Length < before)
    //                propagationQueue.Enqueue(neighbor);
    //        }
    //    }
    //}
    private void Propagate()
    {
        while (propagationQueue.Count > 0 && !waveFailed)
        {
            CellWFC cell = propagationQueue.Dequeue();

            foreach (CellWFC neighbor in GetNeighbors(cell))
            {
                TileWFC[] filtered = FilterTiles(neighbor, cell);

                if (filtered.Length == 0)
                {
                    Fail(neighbor);
                    return;
                }

                if (neighbor.collapsed)
                    continue;

                int before = neighbor.tileOptions.Length;
                neighbor.tileOptions = filtered;

                if (filtered.Length == 1)
                {
                    Collapse(neighbor);

                    if (waveFailed)
                        return;
                }
                else if (filtered.Length < before)
                {
                    propagationQueue.Enqueue(neighbor);
                }
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

    //private void Fail(CellWFC cell)
    //{
    //    propagationQueue.Clear();

    //    retries = retries + 1;
    //    if (retries <= maxRetries)
    //    {
    //        Debug.LogWarning($"Contradicción en ({cell.q},{cell.r}). Reintento {retries}/{maxRetries}");
    //        StartWave();
    //    }
    //    else
    //    {
    //        IsGenerating = false;
    //        Debug.LogError("WFC falló tras agotar los reintentos. Revisa reglas/topes.");
    //    }
    //}
    private void Fail(CellWFC cell)
    {
        if (waveFailed)
            return;

        waveFailed = true;
        propagationQueue.Clear();

        Debug.LogWarning($"Contradicción WFC en ({cell.q}, {cell.r}).");
    }

    private Vector3 HexCenter(int q, int r)
    {
        float w = radius * 1.7320508f;
        return new Vector3(w * (q + r * 0.5f), 0f, -1.5f * radius * r);
    }

    private void InstantiateCollapsedCells()
    {
        if (squareMaze)
            return;

        foreach (KeyValuePair<Vector2Int, CellWFC> kv in grid)
        {
            CellWFC cell = kv.Value;
            if (!cell.collapsed || cell.instantiated) continue;

            TileWFC inst = Instantiate(cell.selectedTile, Vector3.zero, Quaternion.identity, transform);

            Renderer rend = inst.GetComponentInChildren<Renderer>();
            Vector3 delta = HexCenter(cell.q, cell.r) - rend.bounds.center;
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

            HexCell hc = inst.gameObject.GetComponent<HexCell>();
            if (hc == null) hc = inst.gameObject.AddComponent<HexCell>();
            hc.q = cell.q;
            hc.r = cell.r;
            hc.type = cell.selectedTile.type;

            cell.instantiated = true;
        }
    }

    //private void ClearGridObjects()
    //{
    //    for (int i = transform.childCount - 1; i >= 0; i--)
    //    {
    //        Transform child = transform.GetChild(i);
    //        if (child.GetComponent<TileWFC>() != null)
    //            DestroyImmediate(child.gameObject);
    //    }
    //}
    private void ClearGridObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (child.GetComponent<TileWFC>() == null)
                continue;

            child.SetActive(false);

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnDrawGizmos()
    {
        if (squareMaze)
        {
            if (Layout == null)
                return;

            Gizmos.color = Color.cyan;

            foreach (Vector2Int cell in Layout.Cells)
            {
                Vector3 center = transform.TransformPoint(
                    new Vector3(cell.x * cellSize, 0.1f, cell.y * cellSize));

                foreach (Vector2Int neighbor in Layout.Neighbors(cell))
                {
                    // Dibujar cada conexión una sola vez.
                    if (neighbor.y < cell.y ||
                        (neighbor.y == cell.y && neighbor.x < cell.x))
                        continue;

                    Vector3 other = transform.TransformPoint(
                        new Vector3(
                            neighbor.x * cellSize, 0.1f, neighbor.y * cellSize));

                    Gizmos.DrawLine(center, other);
                }
            }

            return;
        }

        if (grid == null) return;
        Gizmos.color = Color.green;

        foreach (KeyValuePair<Vector2Int, CellWFC> kv in grid)
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

    [ContextMenu("Comprobar conexiones del laberinto")]
    private void CheckMazeLayout()
    {
        var layout = MazeLayout.Build(4, 4, 12345, 0f);

        int reachable = layout.Distances(Vector2Int.zero, 16).Count;
        int directedLinks = 0;

        foreach (var cell in layout.Cells)
        {
            foreach (var neighbor in layout.Neighbors(cell))
            {
                directedLinks++;

                Debug.Assert(layout.CanMove(neighbor, cell),
                    "Hay una conexión sin camino de vuelta.");

                var delta = neighbor - cell;
                Debug.Assert(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1,
                    "Hay una conexión entre celdas no cardinales.");
            }
        }

        Debug.Assert(reachable == 16, "Hay celdas inaccesibles.");
        Debug.Assert(directedLinks / 2 == 15,
            "Sin pasos adicionales, debe haber 15 conexiones.");

        Debug.Log($"Celdas accesibles: {reachable}/16. " +
                  $"Conexiones: {directedLinks / 2}/15.");
    }
}