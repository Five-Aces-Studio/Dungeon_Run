using System;
using System.Collections.Generic;
using UnityEngine;

public static class BinarySpacePartitioner
{
    public static List<RectInt> Generate(Vector2Int areaSize, int minimumSide, int targetCount, int seed)
    {
        if (areaSize.x < 1 || areaSize.y < 1 ||
            areaSize.x > 256 || areaSize.y > 256)
        {
            throw new ArgumentException(
                "El área debe medir entre 1 y 256 casillas por lado.");
        }

        if (minimumSide < 1 ||
            minimumSide > areaSize.x ||
            minimumSide > areaSize.y)
        {
            throw new ArgumentException(
                "El tamaño mínimo debe ser positivo y caber en el área.");
        }

        if (targetCount < 1 || targetCount > 64)
        {
            throw new ArgumentException(
                "Solicita entre 1 y 64 regiones.");
        }

        var random = new System.Random(
            unchecked(seed ^ 0x2C9277B5));

        const int maximumAttempts = 8;

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var regions = new List<RectInt>
            {
                new RectInt(Vector2Int.zero, areaSize)
            };

            while (regions.Count < targetCount)
            {
                var candidates = new List<int>();

                for (int i = 0; i < regions.Count; i++)
                {
                    RectInt region = regions[i];

                    if (region.width >= minimumSide * 2 ||
                        region.height >= minimumSide * 2)
                    {
                        candidates.Add(i);
                    }
                }

                if (candidates.Count == 0)
                    break;

                int index = candidates[random.Next(candidates.Count)];
                RectInt parent = regions[index];

                bool canSplitX = parent.width >= minimumSide * 2;
                bool canSplitZ = parent.height >= minimumSide * 2;

                bool splitX =
                    canSplitX && (!canSplitZ || random.Next(2) == 0);

                RectInt first;
                RectInt second;

                if (splitX)
                {
                    int cut = random.Next(
                        minimumSide,
                        parent.width - minimumSide + 1);

                    first = new RectInt(
                        parent.xMin, parent.yMin,
                        cut, parent.height);

                    second = new RectInt(
                        parent.xMin + cut, parent.yMin,
                        parent.width - cut, parent.height);
                }
                else
                {
                    int cut = random.Next(
                        minimumSide,
                        parent.height - minimumSide + 1);

                    first = new RectInt(
                        parent.xMin, parent.yMin,
                        parent.width, cut);

                    second = new RectInt(
                        parent.xMin, parent.yMin + cut,
                        parent.width, parent.height - cut);
                }

                regions[index] = first;
                regions.Insert(index + 1, second);
            }

            if (regions.Count == targetCount)
                return regions;
        }

        throw new ArgumentException(
            $"No se consiguieron {targetCount} regiones tras " +
            $"{maximumAttempts} intentos. Revisa área, mínimo o cantidad.");
    }
}