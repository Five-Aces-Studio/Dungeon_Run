using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RandomWalkWFC))]
public sealed class MazeMapView : MonoBehaviour
{
    [Header("Architecture")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject fogPrefab;

    [Header("Contents")]
    [SerializeField] private GameObject combatPrefab;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private GameObject eventPrefab;
    [SerializeField] private GameObject shopPrefab;

    [Header("Picking")]
    [SerializeField] private string floorLayerName = "MazeCell";

    public event Action MapReady;
    public event Action MapCleared;

    public RandomWalkWFC Generator { get; private set; }
    public bool IsReady { get; private set; }

    public IReadOnlyDictionary<Vector2Int, MazeCellView> Cells => cells;

    private readonly Dictionary<Vector2Int, MazeCellView> cells = new();
    private Transform generatedRoot;

    private void Awake()
    {
        Generator = GetComponent<RandomWalkWFC>();
    }

    private void OnEnable()
    {
        Generator.OnGenerationStarted += Clear;
        Generator.OnGenerationComplete += Build;

        if (Generator.HasGenerated && Generator.IsSquareMaze)
            Build();
    }

    private void OnDisable()
    {
        Generator.OnGenerationStarted -= Clear;
        Generator.OnGenerationComplete -= Build;
        Clear();
    }

    private void Clear()
    {
        IsReady = false;
        MapCleared?.Invoke();
        cells.Clear();

        if (generatedRoot == null)
            return;

        // Destroy se ejecuta al final del frame:
        // desactivamos ahora los colliders y los renderers antiguos.
        generatedRoot.gameObject.SetActive(false);
        Destroy(generatedRoot.gameObject);
        generatedRoot = null;
    }

    private void Build()
    {
        Clear();

        if (!Generator.IsSquareMaze || Generator.Layout == null)
            return;

        int floorLayer = LayerMask.NameToLayer(floorLayerName);

        if (floorLayer < 0)
        {
            Debug.LogError($"Crea la layer '{floorLayerName}' antes de generar.");
            return;
        }

        if (floorPrefab == null || wallPrefab == null || fogPrefab == null ||
            combatPrefab == null || itemPrefab == null ||
            eventPrefab == null || shopPrefab == null)
        {
            Debug.LogError("Faltan prefabs en MazeMapView.");
            return;
        }

        if (floorPrefab.GetComponentInChildren<Collider>(true) == null ||
            fogPrefab.GetComponentInChildren<Renderer>(true) == null)
        {
            Debug.LogError("El suelo necesita Collider y la niebla Renderer.");
            return;
        }

        generatedRoot = new GameObject("GeneratedMaze").transform;
        generatedRoot.SetParent(transform, false);

        float size = Generator.CellSize;
        float modelScale = size / 3f;

        foreach (CellWFC data in Generator.GeneratedCells)
        {
            Vector2Int coordinate = new Vector2Int(data.q, data.r);

            GameObject cellObject = new GameObject(
                $"Cell_{data.q}_{data.r}_{data.selectedTile.type}");

            cellObject.transform.SetParent(generatedRoot, false);
            cellObject.transform.localPosition =
                new Vector3(data.q * size, 0f, data.r * size);

            GameObject floor = Instantiate(
                floorPrefab, cellObject.transform, false);

            floor.transform.localPosition = Vector3.zero;
            floor.transform.localRotation = Quaternion.identity;
            floor.transform.localScale *= modelScale;

            foreach (Transform child in floor.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = floorLayer;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(cellObject.transform, false);

            GameObject selectedPrefab = ContentFor(data.selectedTile.type);

            if (selectedPrefab != null)
            {
                GameObject prop = Instantiate(
                    selectedPrefab, content.transform, false);

                prop.transform.localPosition = Vector3.zero;
                prop.transform.localRotation = Quaternion.identity;

                FitContent(prop, content.transform, size);
            }

            GameObject fog = Instantiate(
                fogPrefab, cellObject.transform, false);

            fog.transform.localPosition =
                new Vector3(0f, 0.04f * modelScale, 0f);

            fog.transform.localRotation = Quaternion.identity;
            fog.transform.localScale *= modelScale;

            MazeCellView view = cellObject.AddComponent<MazeCellView>();

            view.Initialize(
                coordinate,
                data.selectedTile.type,
                content,
                fog.GetComponentInChildren<Renderer>(true),
                size);

            cells.Add(coordinate, view);
        }

        BuildWalls(size, modelScale);

        IsReady = true;
        MapReady?.Invoke();
    }

    private GameObject ContentFor(CellType type)
    {
        return type switch
        {
            CellType.Combat => combatPrefab,
            CellType.Item => itemPrefab,
            CellType.Event => eventPrefab,
            CellType.Shop => shopPrefab,
            _ => null
        };
    }

    private void BuildWalls(float size, float modelScale)
    {
        foreach (Vector2Int cell in Generator.Layout.Cells)
        {
            // Norte y este incluyen paredes compartidas y exteriores.
            PlaceWallIfClosed(cell, Vector2Int.up, size, modelScale);
            PlaceWallIfClosed(cell, Vector2Int.right, size, modelScale);

            // Sur y oeste solo se añaden en el borde exterior.
            if (!cells.ContainsKey(cell + Vector2Int.down))
                PlaceWallIfClosed(cell, Vector2Int.down, size, modelScale);

            if (!cells.ContainsKey(cell + Vector2Int.left))
                PlaceWallIfClosed(cell, Vector2Int.left, size, modelScale);
        }
    }

    private void PlaceWallIfClosed(
        Vector2Int cell,
        Vector2Int direction,
        float size,
        float modelScale)
    {
        if (Generator.Layout.CanMove(cell, cell + direction))
            return;

        GameObject wall = Instantiate(wallPrefab, generatedRoot, false);

        wall.transform.localPosition = new Vector3(
            (cell.x + direction.x * 0.5f) * size,
            0f,
            (cell.y + direction.y * 0.5f) * size);

        float angle = direction.x != 0 ? 90f : 0f;
        wall.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        wall.transform.localScale *= modelScale;
    }

    private static void FitContent(
        GameObject prop,
        Transform content,
        float size)
    {
        Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return;

        Bounds bounds = CombinedBounds(renderers);

        // Reservar el exterior de la celda para caminar.
        float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        float allowedSize = size * 0.32f;

        if (horizontalSize > allowedSize)
            prop.transform.localScale *= allowedSize / horizontalSize;

        bounds = CombinedBounds(renderers);

        Vector3 desiredBottomCenter = content.TransformPoint(
            new Vector3(0f, 0.025f * size / 3f, 0f));

        Vector3 currentBottomCenter = new Vector3(
            bounds.center.x, bounds.min.y, bounds.center.z);

        prop.transform.position += desiredBottomCenter - currentBottomCenter;
    }

    private static Bounds CombinedBounds(Renderer[] renderers)
    {
        Bounds result = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            result.Encapsulate(renderers[i].bounds);

        return result;
    }
}