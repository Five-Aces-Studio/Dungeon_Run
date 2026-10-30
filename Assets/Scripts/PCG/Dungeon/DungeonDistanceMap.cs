using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonDistanceMap
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static Dictionary<Vector2Int, int> Build(
        DungeonFloorData floor,
        Vector2Int start,
        int? excludedRoomId = null)
    {
        if (floor == null || floor.cells == null)
        {
            throw new ArgumentException(
                "El piso no contiene datos válidos.");
        }

        var coordinates = new HashSet<Vector2Int>();
        var walkable = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
        {
            if (!coordinates.Add(cell.coordinate))
            {
                throw new ArgumentException(
                    $"Hay una casilla duplicada en {cell.coordinate}.");
            }

            bool excluded = excludedRoomId.HasValue && cell.roomId == excludedRoomId.Value;

            if (!excluded &&
                (cell.occupancy & CellOccupancy.Blocked) == 0)
            {
                walkable.Add(cell.coordinate);
            }
        }

        if (!walkable.Contains(start))
        {
            throw new ArgumentException(
                "La casilla inicial no es transitable.");
        }

        var distances = new Dictionary<Vector2Int, int>
        {
            [start] = 0
        };

        var pending = new Queue<Vector2Int>();
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            int nextDistance = distances[current] + 1;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbour = current + direction;

                if (!walkable.Contains(neighbour) ||
                    distances.ContainsKey(neighbour))
                {
                    continue;
                }

                distances.Add(neighbour, nextDistance);
                pending.Enqueue(neighbour);
            }
        }

        return distances;
    }
}