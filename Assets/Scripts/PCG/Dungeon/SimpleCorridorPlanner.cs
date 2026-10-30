using System;
using System.Collections.Generic;
using UnityEngine;

public static class SimpleCorridorPlanner
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
        RoomConnection connection,
        out Corridor result)
    {
        result = null;

        if (floor == null ||
            floor.rooms == null ||
            floor.cells == null ||
            floor.regions == null ||
            floor.regions.Count == 0)
        {
            throw new ArgumentException(
                "El trazador necesita los datos de un piso BSP.");
        }

        int a = connection.roomA;
        int b = connection.roomB;

        if (a < 0 || a >= floor.rooms.Count ||
            b < 0 || b >= floor.rooms.Count ||
            a == b)
        {
            throw new ArgumentException(
                "La conexión tiene extremos inválidos.");
        }

        RoomData roomA = floor.rooms[a];
        RoomData roomB = floor.rooms[b];

        var occupied = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
            occupied.Add(cell.coordinate);

        foreach (RoomDoorCandidate doorA in roomA.doorCandidates)
        {
            foreach (RoomDoorCandidate doorB in roomB.doorCandidates)
            {
                if (doorA.width != 1 || doorB.width != 1)
                {
                    throw new ArgumentException(
                        "Este primer trazador solo admite puertas " +
                        "de una casilla de ancho.");
                }

                Vector2Int start = doorA.startCell + doorA.outward;
                Vector2Int end = doorB.startCell + doorB.outward;

                for (int variant = 0; variant < 2; variant++)
                {
                    List<Vector2Int> path = BuildPath(
                        start, end, variant == 0);

                    if (result != null &&
                        path.Count >= result.cells.Count)
                    {
                        continue;
                    }

                    if (!IsValid(
                            path, doorA, doorB,
                            occupied, floor.regions))
                    {
                        continue;
                    }

                    result = new Corridor
                    {
                        roomA = a,
                        roomB = b,
                        doorA = doorA,
                        doorB = doorB,
                        cells = path
                    };
                }
            }
        }

        if (result == null)
        {
            result = FindWithBFS(
                roomA,
                roomB,
                occupied,
                floor.regions);
        }

        return result != null;
    }

    private static Corridor FindWithBFS(RoomData roomA, RoomData roomB, HashSet<Vector2Int> occupied, List<RectInt> regions)
    {
        var area = new HashSet<Vector2Int>();

        foreach (RectInt region in regions)
        {
            for (int y = region.yMin; y < region.yMax; y++)
            {
                for (int x = region.xMin; x < region.xMax; x++)
                    area.Add(new Vector2Int(x, y));
            }
        }

        var forbidden = new HashSet<Vector2Int>(occupied);

        foreach (Vector2Int cell in occupied)
        {
            foreach (Vector2Int direction in Directions)
                forbidden.Add(cell + direction);
        }

        var destinations =
            new Dictionary<Vector2Int, RoomDoorCandidate>();

        foreach (RoomDoorCandidate door in roomB.doorCandidates)
        {
            Vector2Int outside = door.startCell + door.outward;

            if (IsValidEndpoint(door, occupied, area) &&
                !destinations.ContainsKey(outside))
            {
                destinations.Add(outside, door);
            }
        }

        if (destinations.Count == 0)
            return null;

        var entrances =
            new Dictionary<Vector2Int, RoomDoorCandidate>();

        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var pending = new Queue<Vector2Int>();

        foreach (RoomDoorCandidate door in roomA.doorCandidates)
        {
            Vector2Int outside = door.startCell + door.outward;

            if (!IsValidEndpoint(door, occupied, area) ||
                previous.ContainsKey(outside))
            {
                continue;
            }

            entrances.Add(outside, door);

            previous.Add(outside, outside);
            pending.Enqueue(outside);
        }

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();

            if (destinations.TryGetValue(
                    current, out RoomDoorCandidate exitDoor))
            {
                var path = new List<Vector2Int>();
                Vector2Int cursor = current;

                while (true)
                {
                    path.Add(cursor);

                    Vector2Int parent = previous[cursor];

                    if (parent == cursor)
                        break;

                    cursor = parent;
                }

                RoomDoorCandidate entranceDoor = entrances[cursor];

                path.Reverse();

                if (!IsValid(
                        path,
                        entranceDoor,
                        exitDoor,
                        occupied,
                        regions))
                {
                    throw new ArgumentException(
                        "La ruta BFS no cumple las condiciones del pasillo.");
                }

                return new Corridor
                {
                    roomA = roomA.id,
                    roomB = roomB.id,
                    doorA = entranceDoor,
                    doorB = exitDoor,
                    cells = path
                };
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                if (!area.Contains(next) || previous.ContainsKey(next))
                    continue;

                if (forbidden.Contains(next) &&
                    !destinations.ContainsKey(next))
                {
                    continue;
                }

                previous.Add(next, current);
                pending.Enqueue(next);
            }
        }

        return null;
    }

    private static bool IsValidEndpoint(
        RoomDoorCandidate door,
        HashSet<Vector2Int> occupied,
        HashSet<Vector2Int> area)
    {
        if (door.width != 1)
        {
            throw new ArgumentException(
                "El trazador actual solo admite puertas de una casilla.");
        }

        Vector2Int outside = door.startCell + door.outward;

        if (!occupied.Contains(door.startCell) ||
            occupied.Contains(outside) ||
            !area.Contains(outside))
        {
            return false;
        }

        foreach (Vector2Int direction in Directions)
        {
            Vector2Int neighbour = outside + direction;

            if (occupied.Contains(neighbour) &&
                neighbour != door.startCell)
            {
                return false;
            }
        }

        return true;
    }

    private static List<Vector2Int> BuildPath(
        Vector2Int start,
        Vector2Int end,
        bool xFirst)
    {
        var path = new List<Vector2Int> { start };

        Vector2Int corner = xFirst
            ? new Vector2Int(end.x, start.y)
            : new Vector2Int(start.x, end.y);

        AddStraightSegment(path, corner);
        AddStraightSegment(path, end);

        return path;
    }

    private static void AddStraightSegment(
        List<Vector2Int> path,
        Vector2Int target)
    {
        Vector2Int current = path[path.Count - 1];

        Vector2Int step = new Vector2Int(
            Math.Sign(target.x - current.x),
            Math.Sign(target.y - current.y));

        if (step.x != 0 && step.y != 0)
        {
            throw new ArgumentException(
                "El segmento debe ser horizontal o vertical.");
        }

        while (current != target)
        {
            current += step;
            path.Add(current);
        }
    }

    private static bool IsValid(
        List<Vector2Int> path,
        RoomDoorCandidate doorA,
        RoomDoorCandidate doorB,
        HashSet<Vector2Int> occupied,
        List<RectInt> regions)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int cell = path[i];

            if (occupied.Contains(cell))
                return false;

            bool insideArea = false;

            foreach (RectInt region in regions)
            {
                if (region.Contains(cell))
                {
                    insideArea = true;
                    break;
                }
            }

            if (!insideArea)
                return false;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbour = cell + direction;

                if (!occupied.Contains(neighbour))
                    continue;

                bool touchesEntrance =
                    i == 0 && neighbour == doorA.startCell;

                bool touchesExit =
                    i == path.Count - 1 &&
                    neighbour == doorB.startCell;

                if (!touchesEntrance && !touchesExit)
                    return false;
            }
        }

        return true;
    }
}