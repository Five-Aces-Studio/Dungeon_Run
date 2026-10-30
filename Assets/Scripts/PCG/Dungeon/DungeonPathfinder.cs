using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static bool TryFind(
        DungeonFloorData floor,
        Vector2Int start,
        Vector2Int destination,
        out List<Vector2Int> path,
        Func<Vector2Int, Vector2Int, bool> canTraverse = null)
    {
        path = new List<Vector2Int>();

        if (floor == null || floor.cells == null)
            throw new ArgumentException("El piso no contiene datos válidos.");

        var coordinates = new HashSet<Vector2Int>();
        var walkable = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
        {
            if (!coordinates.Add(cell.coordinate))
            {
                throw new ArgumentException(
                    $"Hay una casilla duplicada en {cell.coordinate}.");
            }

            if ((cell.occupancy & CellOccupancy.Blocked) == 0)
                walkable.Add(cell.coordinate);
        }

        if (!walkable.Contains(start) ||
            !walkable.Contains(destination))
        {
            return false;
        }

        var open = new List<Vector2Int> { start };
        var closed = new HashSet<Vector2Int>();

        var costs = new Dictionary<Vector2Int, int>
        {
            [start] = 0
        };

        if (canTraverse != null &&
        (!canTraverse(start, start) ||
         !canTraverse(destination, destination)))
        {
            return false;
        }

        var previous = new Dictionary<Vector2Int, Vector2Int>();

        while (open.Count > 0)
        {
            int bestIndex = 0;
            int bestEstimate = Distance(open[0], destination);
            int bestTotal = costs[open[0]] + bestEstimate;

            for (int i = 1; i < open.Count; i++)
            {
                int estimate = Distance(open[i], destination);
                int total = costs[open[i]] + estimate;

                if (total < bestTotal ||
                    (total == bestTotal && estimate < bestEstimate))
                {
                    bestIndex = i;
                    bestEstimate = estimate;
                    bestTotal = total;
                }
            }

            Vector2Int current = open[bestIndex];
            open.RemoveAt(bestIndex);

            if (current == destination)
            {
                Vector2Int cursor = destination;

                while (cursor != start)
                {
                    path.Add(cursor);
                    cursor = previous[cursor];
                }

                path.Add(start);
                path.Reverse();
                return true;
            }

            closed.Add(current);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbour = current + direction;

                if (!walkable.Contains(neighbour) ||
                    closed.Contains(neighbour))
                {
                    continue;
                }

                if (canTraverse != null && !canTraverse(current, neighbour))
                    continue;

                int newCost = costs[current] + 1;

                if (costs.TryGetValue(neighbour, out int oldCost) &&
                    newCost >= oldCost)
                {
                    continue;
                }

                costs[neighbour] = newCost;
                previous[neighbour] = current;

                if (!open.Contains(neighbour))
                    open.Add(neighbour);
            }
        }

        return false;
    }

    private static int Distance(Vector2Int a, Vector2Int b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
}