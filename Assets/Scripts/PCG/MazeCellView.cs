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
    public bool HasCenterObstacle { get; private set; }

    public Vector3 CenterPosition =>
        transform.TransformPoint(new Vector3(0f, surfaceHeight, 0f));

    public Vector3 ArrivalPosition =>
        HasCenterObstacle ? RingPoint(0) : CenterPosition;

    private GameObject contentRoot;
    private Renderer fogRenderer;
    private MaterialPropertyBlock fogBlock;

    public bool CanInteract =>
        HasCenterObstacle &&
        (Type == CellType.Combat || Type == CellType.Item) &&
        targetCoverage == 0f &&
        coverage == 0f;

    public Vector3 InteractionPosition { get; private set; }

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
        HasCenterObstacle = contentRoot.transform.childCount > 0;
        fogRenderer = fog;

        cellSize = size;
        surfaceHeight = 0.025f * size / 3f;

        Renderer[] contentRenderers =
            contentRoot.GetComponentsInChildren<Renderer>(true);

        if (contentRenderers.Length > 0)
        {
            Bounds bounds = contentRenderers[0].bounds;

            for (int i = 1; i < contentRenderers.Length; i++)
                bounds.Encapsulate(contentRenderers[i].bounds);

            InteractionPosition = new Vector3(
                bounds.center.x,
                bounds.max.y + 0.35f,
                bounds.center.z);
        }
        else
        {
            InteractionPosition = CenterPosition + Vector3.up * 1.5f;
        }

        fogBlock = new MaterialPropertyBlock();

        coverage = 1f;
        targetCoverage = 1f;

        ApplyVisibility();
    }

    public bool TryClearContent()
    {
        if (!CanInteract)
            return false;

        HasCenterObstacle = false;
        Type = CellType.Normal;

        for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        return true;
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

    //private void ApplyVisibility()
    //{
    //    if (fogRenderer == null)
    //        return;

    //    fogRenderer.GetPropertyBlock(fogBlock);
    //    fogBlock.SetFloat(CoverageId, coverage);
    //    fogRenderer.SetPropertyBlock(fogBlock);

    //    fogRenderer.enabled = coverage > 0f;

    //    contentRoot.SetActive(targetCoverage == 0f && coverage == 0f);
    //}
    private void ApplyVisibility()
    {
        if (fogRenderer != null)
        {
            fogRenderer.GetPropertyBlock(fogBlock);
            fogBlock.SetFloat(CoverageId, coverage);
            fogRenderer.SetPropertyBlock(fogBlock);

            fogRenderer.enabled = coverage > 0f;
        }

        contentRoot.SetActive(
            targetCoverage == 0f && coverage == 0f);
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