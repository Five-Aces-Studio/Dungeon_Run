using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DungeonRoomView : MonoBehaviour
{
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;

    private const float FloorThickness = 0.15f;
    private const float WallHeight = 2f;
    private const float WallThickness = 0.15f;

    private GameObject generatedRoot;
    public GameObject Root => generatedRoot;
    public void AdoptRoot(GameObject root, DungeonFloorData floor)
    {
        generatedRoot = root;
        floorCells.Clear();
        foreach (Transform child in root.transform)
        {
            if (!child.name.StartsWith("Floor_")) continue;
            var collider = child.GetComponent<BoxCollider>();
            var center = collider.bounds.center;
            floorCells.Add(collider, new Vector2Int(Mathf.RoundToInt(center.x/floor.cellSize), Mathf.RoundToInt(center.z/floor.cellSize)));
        }
    }
    public GameObject ReleaseRoot()
    {
        var root = generatedRoot;
        generatedRoot = null;
        floorCells.Clear();
        return root;
    }

    private readonly Dictionary<Collider, Vector2Int> floorCells = new Dictionary<Collider, Vector2Int>();

    public bool TryGetFloorCell(Collider collider, out Vector2Int coordinate)
    {
        coordinate = default;

        return collider != null && floorCells.TryGetValue(collider, out coordinate);
    }

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public void Build(DungeonFloorData floor)
    {
        if (!Application.isPlaying)
            throw new ArgumentException("Construye la vista en Play Mode.");

        if (floor == null || floor.cells == null ||
            float.IsNaN(floor.cellSize) ||
            float.IsInfinity(floor.cellSize) ||
            floor.cellSize <= 0f)
        {
            throw new ArgumentException("Los datos del suelo no son válidos.");
        }

        Bounds floorBounds = ReadMeshBounds(floorPrefab);
        Bounds wallBounds = ReadMeshBounds(wallPrefab);

        Clear();

        generatedRoot = new GameObject("GeneratedDungeon");

        var occupied = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
            occupied.Add(cell.coordinate);

        float size = floor.cellSize;
        int wallCount = 0;

        foreach (DungeonCellData cell in floor.cells)
        {
            Vector3 center = floor.CellCenter(cell.coordinate);

            BoxCollider floorCollider = CreatePiece(
                floorPrefab,
                floorBounds,
                $"Floor_{cell.coordinate.x}_{cell.coordinate.y}",
                center - Vector3.up * (FloorThickness * 0.5f),
                Quaternion.identity,
                new Vector3(size, FloorThickness, size));

            floorCells.Add(floorCollider, cell.coordinate);

            foreach (Vector2Int direction in Directions)
            {
                if (occupied.Contains(cell.coordinate + direction))
                    continue;

                Vector3 outward =
                    new Vector3(direction.x, 0f, direction.y);

                Vector3 wallCenter =
                    center +
                    outward * (size * 0.5f + WallThickness * 0.5f) +
                    Vector3.up * ((WallHeight - FloorThickness) * 0.5f);

                CreatePiece(
                    wallPrefab,
                    wallBounds,
                    $"Wall_{cell.coordinate.x}_{cell.coordinate.y}" +
                    $"_{direction.x}_{direction.y}",
                    wallCenter,
                    Quaternion.LookRotation(outward, Vector3.up),
                    new Vector3(size, WallHeight + FloorThickness, WallThickness));

                wallCount++;
            }
            // Fill convex seams outside the walkable footprint, without narrowing door openings.
            for (int corner = 0; corner < Directions.Length; corner++)
            {
                Vector2Int a = Directions[corner], b = Directions[(corner + 1) % Directions.Length];
                if (occupied.Contains(cell.coordinate + a) || occupied.Contains(cell.coordinate + b) ||
                    occupied.Contains(cell.coordinate + a + b)) continue;
                Vector3 diagonal = new Vector3(a.x+b.x, 0, a.y+b.y);
                CreatePiece(wallPrefab, wallBounds, $"Corner_{cell.coordinate}_{corner}",
                    center + diagonal * (size*.5f+WallThickness*.5f) + Vector3.up*((WallHeight-FloorThickness)*.5f),
                    Quaternion.identity, new Vector3(WallThickness,WallHeight+FloorThickness,WallThickness));
            }
        }

        Debug.Log(
            $"Vista: {floor.cells.Count} suelos y {wallCount} paredes.",
            this);
    }

    private static Bounds ReadMeshBounds(GameObject prefab)
    {
        if (prefab == null)
            throw new ArgumentException("Asigna los prefabs de suelo y pared.");

        MeshFilter filter = prefab.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null ||
            prefab.GetComponent<MeshRenderer>() == null)
        {
            throw new ArgumentException(
                $"{prefab.name} necesita MeshFilter y MeshRenderer en la raíz.");
        }

        Bounds bounds = filter.sharedMesh.bounds;

        if (bounds.size.x <= 0f ||
            bounds.size.y <= 0f ||
            bounds.size.z <= 0f)
        {
            throw new ArgumentException(
                $"{prefab.name} tiene dimensiones de malla inválidas.");
        }

        return bounds;
    }

    private BoxCollider CreatePiece(
        GameObject prefab,
        Bounds meshBounds,
        string objectName,
        Vector3 center,
        Quaternion rotation,
        Vector3 dimensions)
    {
        GameObject instance =
            Instantiate(prefab, generatedRoot.transform);

        instance.name = objectName;

        Vector3 scale = new Vector3(
            dimensions.x / meshBounds.size.x,
            dimensions.y / meshBounds.size.y,
            dimensions.z / meshBounds.size.z);

        instance.transform.localScale = scale;
        instance.transform.rotation = rotation;

        instance.transform.position =
            center -
            rotation * Vector3.Scale(meshBounds.center, scale);

        BoxCollider collider = instance.GetComponent<BoxCollider>();

        if (collider == null)
            collider = instance.AddComponent<BoxCollider>();

        collider.center = meshBounds.center;
        collider.size = meshBounds.size;
        collider.isTrigger = false;
        collider.enabled = true;

        return collider;
    }

    public void Clear()
    {
        floorCells.Clear();

        if (generatedRoot == null)
            return;

        generatedRoot.SetActive(false);
        Destroy(generatedRoot);
        generatedRoot = null;
    }

    private void OnDisable()
    {
        Clear();
    }
}
