using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BSPLayoutProbe : MonoBehaviour
{
    [SerializeField] private int seed = 12345;
    [SerializeField] private Vector2Int areaSize = new Vector2Int(52, 39);
    [SerializeField, Min(1)] private int minimumSide = 13;
    [SerializeField, Min(1)] private int targetCount = 6;

    [SerializeField]
    private List<RectInt> regions = new List<RectInt>();

    [ContextMenu("Generar regiones BSP")]
    private void Generate()
    {
        try
        {
            regions = BinarySpacePartitioner.Generate(
                areaSize, minimumSide, targetCount, seed);

            Debug.Log($"BSP: {regions.Count} regiones.", this);
        }
        catch (ArgumentException exception)
        {
            regions.Clear();
            Debug.LogError(exception.Message, this);
        }
    }

    [ContextMenu("Comprobar BSP")]
    private void Check()
    {
        try
        {
            List<RectInt> first = BinarySpacePartitioner.Generate(
                areaSize, minimumSide, targetCount, seed);

            List<RectInt> second = BinarySpacePartitioner.Generate(
                areaSize, minimumSide, targetCount, seed);

            bool valid =
                first.Count == targetCount &&
                first.Count == second.Count;

            int totalArea = 0;

            for (int i = 0; i < first.Count; i++)
            {
                RectInt region = first[i];

                valid &= i < second.Count && region == second[i];
                valid &= region.width >= minimumSide;
                valid &= region.height >= minimumSide;
                valid &= region.xMin >= 0 && region.yMin >= 0;
                valid &= region.xMax <= areaSize.x;
                valid &= region.yMax <= areaSize.y;

                totalArea += region.width * region.height;

                for (int j = 0; j < i; j++)
                    valid &= !region.Overlaps(first[j]);
            }

            valid &= totalArea == areaSize.x * areaSize.y;

            if (valid)
            {
                Debug.Log(
                    "BSP correcto: repetible, sin solapamientos, " +
                    "con tamaños válidos y cobertura completa.",
                    this);
            }
            else
            {
                Debug.LogError("La comprobación BSP ha fallado.", this);
            }
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (regions == null || regions.Count == 0)
            return;

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.identity;

        for (int i = 0; i < regions.Count; i++)
        {
            RectInt region = regions[i];

            Gizmos.color = Color.HSVToRGB(
                i / (float)regions.Count, 0.65f, 0.95f);

            Vector3 center = new Vector3(
                region.xMin + (region.width - 1) * 0.5f,
                0.05f,
                region.yMin + (region.height - 1) * 0.5f);

            Gizmos.DrawWireCube(
                center,
                new Vector3(region.width, 0.02f, region.height));
        }

        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }
}