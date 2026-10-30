using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonCorridorBuilder
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static DungeonFloorData Build(
    DungeonFloorData layout,
    int maximumAttempts = 8)
    {
        if (layout == null ||
            layout.rooms == null ||
            layout.cells == null ||
            layout.regions == null ||
            layout.plannedConnections == null ||
            layout.corridors == null)
        {
            throw new ArgumentException(
                "Los datos del piso no están completos.");
        }

        if (maximumAttempts < 1 || maximumAttempts > 32)
        {
            throw new ArgumentException(
                "La cantidad máxima de intentos debe estar entre 1 y 32.");
        }

        if (layout.corridors.Count > 0)
        {
            throw new ArgumentException(
                "Construye los pasillos sobre una distribución nueva.");
        }

        foreach (DungeonCellData cell in layout.cells)
        {
            if (cell.roomId < 0)
            {
                throw new ArgumentException(
                    "La distribución de entrada debe contener solo habitaciones.");
            }
        }

        int connectionCount = layout.plannedConnections.Count;

        var random = new System.Random(unchecked(layout.seed ^ 0x37B91D45));

        var triedOrders = new HashSet<string>();

        int attemptsRun = 0;
        RoomConnection lastFailed = default;

        int candidateLimit = connectionCount <= 1 ? 1 : maximumAttempts;

        for (int candidate = 0; candidate < candidateLimit; candidate++)
        {
            var order = new List<int>();

            for (int i = 0; i < connectionCount; i++)
                order.Add(i);

            if (candidate == 1)
            {
                order.Reverse();
            }
            else if (candidate >= 2)
            {
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);

                    int temporary = order[i];
                    order[i] = order[j];
                    order[j] = temporary;
                }
            }

            string orderKey = string.Join(",", order);

            if (!triedOrders.Add(orderKey))
                continue;

            attemptsRun++;

            var floor = new DungeonFloorData
            {
                seed = layout.seed,
                cellSize = layout.cellSize,
                regions = new List<RectInt>(layout.regions),
                rooms = new List<RoomData>(layout.rooms),
                cells = new List<DungeonCellData>(layout.cells),
                plannedConnections =
                    new List<RoomConnection>(layout.plannedConnections)
            };

            var corridorsByConnection = new Corridor[connectionCount];
            bool complete = true;

            foreach (int connectionIndex in order)
            {
                RoomConnection connection =
                    floor.plannedConnections[connectionIndex];

                bool found = SimpleCorridorPlanner.TryFind(
                    floor,
                    connection,
                    out Corridor corridor);

                if (!found)
                {
                    complete = false;
                    lastFailed = connection;
                    break;
                }

                corridorsByConnection[connectionIndex] = corridor;

                foreach (Vector2Int coordinate in corridor.cells)
                {
                    floor.cells.Add(new DungeonCellData
                    {
                        coordinate = coordinate,
                        roomId = DungeonCellData.CorridorRoomId,
                        occupancy = CellOccupancy.None
                    });
                }
            }

            if (!complete)
                continue;

            floor.corridors.AddRange(corridorsByConnection);
            floor.corridorBuildAttempts = attemptsRun;

            ValidateConnectedFloor(floor);

            return floor;
        }

        throw new ArgumentException(
            $"No se pudo construir el piso tras probar " +
            $"{attemptsRun} órdenes distintos. " +
            $"Última conexión fallida: " +
            $"{lastFailed.roomA} -> {lastFailed.roomB}. " +
            "Esto no demuestra que el piso sea imposible.");
    }

    private static void ValidateConnectedFloor(DungeonFloorData floor)
    {
        var coordinates = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
        {
            if (!coordinates.Add(cell.coordinate))
            {
                throw new ArgumentException(
                    $"Hay suelo duplicado en {cell.coordinate}.");
            }
        }

        if (coordinates.Count == 0)
            throw new ArgumentException("El piso no contiene suelo.");

        var visited = new HashSet<Vector2Int>();
        var pending = new Queue<Vector2Int>();

        Vector2Int start = floor.cells[0].coordinate;

        visited.Add(start);
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbour = current + direction;

                if (!coordinates.Contains(neighbour))
                    continue;

                if (visited.Add(neighbour))
                    pending.Enqueue(neighbour);
            }
        }

        if (visited.Count != coordinates.Count)
        {
            throw new ArgumentException(
                $"El suelo está desconectado: se alcanzan " +
                $"{visited.Count} de {coordinates.Count} casillas.");
        }
    }
}