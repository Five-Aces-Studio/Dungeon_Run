using System.Collections.Generic;
using UnityEngine;

public sealed class MazeCellView : MonoBehaviour
{
    private static readonly int CoverageId =
        Shader.PropertyToID("_Coverage");

    private static readonly Vector2Int[] Ring =
    {
        new Vector2Int(-1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1, -1),
        new Vector2Int( 1,  0),
        new Vector2Int( 1,  1),
        new Vector2Int( 0,  1),
        new Vector2Int(-1,  1),
        new Vector2Int(-1,  0)
    };

    public Vector2Int Coordinate { get; private set; }
    public CellType Type { get; private set; }
    public Vector3 ArrivalPosition => RingPoint(0);

    private GameObject contentRoot;
    private Renderer fogRenderer;
    private MaterialPropertyBlock fogBlock;

    private float cellSize;
    private float surfaceHeight;
    private float coverage = 1f;
    private float targetCoverage = 1f;

    private const float FadeSeconds = 0.25f;

    public void Initialize(
        Vector2Int coordinate,
        CellType type,
        GameObject content,
        Renderer fog,
        float size)
    {
        Coordinate = coordinate;
        Type = type;
        contentRoot = content;
        fogRenderer = fog;

        cellSize = size;
        surfaceHeight = 0.025f * size / 3f;

        fogBlock = new MaterialPropertyBlock();

        coverage = 1f;
        targetCoverage = 1f;

        ApplyVisibility();
    }

    public void SetDiscovery(bool discovered, bool penumbra)
    {
        targetCoverage = discovered ? 0f : penumbra ? 0.5f : 1f;

        // Ocultar inmediatamente objetos, luces y sombras desconocidos.
        if (targetCoverage > 0f)
            contentRoot.SetActive(false);
    }

    private void Update()
    {
        coverage = Mathf.MoveTowards(
            coverage,
            targetCoverage,
            Time.deltaTime / FadeSeconds);

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (fogRenderer == null)
            return;

        fogRenderer.GetPropertyBlock(fogBlock);
        fogBlock.SetFloat(CoverageId, coverage);
        fogRenderer.SetPropertyBlock(fogBlock);

        fogRenderer.enabled = coverage > 0f;

        contentRoot.SetActive(targetCoverage == 0f && coverage == 0f);
    }

    public Vector3 RingPoint(int index)
    {
        Vector2Int point = Ring[index];

        Vector3 local = new Vector3(
            point.x * cellSize * 0.3f,
            surfaceHeight,
            point.y * cellSize * 0.3f);

        return transform.TransformPoint(local);
    }

    public void AppendRingPath(
        List<Vector3> result,
        int from,
        int to)
    {
        int clockwise = (to - from + 8) % 8;
        int counterclockwise = (from - to + 8) % 8;
        int step = clockwise <= counterclockwise ? 1 : -1;

        int current = from;

        while (current != to)
        {
            current = (current + step + 8) % 8;
            result.Add(RingPoint(current));
        }
    }
}