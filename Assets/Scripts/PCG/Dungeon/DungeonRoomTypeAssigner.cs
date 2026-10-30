using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonRoomTypeAssigner
{
    public static void Assign(
        DungeonFloorData floor,
        int startRoomId)
    {
        if (floor == null ||
            floor.rooms == null ||
            floor.cells == null ||
            floor.corridors == null ||
            floor.rooms.Count < 2)
        {
            throw new ArgumentException(
                "Se necesitan al menos dos habitaciones y datos completos.");
        }

        int roomCount = floor.rooms.Count;

        if (startRoomId < 0 || startRoomId >= roomCount)
            throw new ArgumentException("La habitación inicial no existe.");

        for (int i = 0; i < roomCount; i++)
        {
            if (floor.rooms[i] == null || floor.rooms[i].id != i)
            {
                throw new ArgumentException(
                    "Los IDs de habitación deben coincidir con sus índices.");
            }
        }

        Vector2Int start = FindReferenceCell(floor, startRoomId);
        var distanceMap = DungeonDistanceMap.Build(floor, start);

        var distances = new int[roomCount];
        var connectionCounts = new int[roomCount];

        foreach (Corridor corridor in floor.corridors)
        {
            if (corridor == null ||
                corridor.roomA < 0 || corridor.roomA >= roomCount ||
                corridor.roomB < 0 || corridor.roomB >= roomCount ||
                corridor.roomA == corridor.roomB)
            {
                throw new ArgumentException(
                    "Hay un pasillo con extremos inválidos.");
            }

            connectionCounts[corridor.roomA]++;
            connectionCounts[corridor.roomB]++;
        }

        int stairsRoomId = -1;
        bool selectedIsLeaf = false;

        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int reference = FindReferenceCell(floor, i);

            if (!distanceMap.TryGetValue(reference, out int distance))
            {
                throw new ArgumentException(
                    $"La habitación {i} es inalcanzable desde el inicio.");
            }

            distances[i] = distance;

            if (i == startRoomId)
                continue;

            bool candidateIsLeaf = connectionCounts[i] == 1;

            bool betterCandidate =
                stairsRoomId < 0 ||
                (candidateIsLeaf && !selectedIsLeaf) ||
                (candidateIsLeaf == selectedIsLeaf &&
                 distance > distances[stairsRoomId]);

            if (!betterCandidate)
                continue;

            stairsRoomId = i;
            selectedIsLeaf = candidateIsLeaf;
        }

        for (int i = 0; i < roomCount; i++)
        {
            RoomData room = floor.rooms[i];

            room.distanceFromStart = distances[i];
            room.type = DungeonRoomType.Unassigned;

            if (i == startRoomId)
                room.type = DungeonRoomType.Start;
            else if (i == stairsRoomId)
                room.type = DungeonRoomType.Stairs;
        }
    }

    public static int AssignHealing(
    DungeonFloorData floor,
    int requestedCount)
    {
        if (floor == null || floor.rooms == null)
            throw new ArgumentException("El piso no contiene habitaciones.");

        if (requestedCount < 0)
        {
            throw new ArgumentException(
                "La cantidad de habitaciones de curación no puede ser negativa.");
        }

        if (floor.keyRoomId < 0 ||
            floor.keyRoomId >= floor.rooms.Count)
        {
            throw new ArgumentException(
                "Planifica primero la ubicación de la llave.");
        }

        var candidates = new List<RoomData>();
        int maximumDistance = 0;

        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i ||
                room.distanceFromStart < 0)
            {
                throw new ArgumentException(
                    "Las habitaciones deben tener IDs y distancias válidos.");
            }

            if (room.type == DungeonRoomType.Healing)
            {
                throw new ArgumentException(
                    "Asigna la curación sobre un piso sin curaciones previas.");
            }

            maximumDistance = Math.Max(
                maximumDistance,
                room.distanceFromStart);

            if (room.type == DungeonRoomType.Unassigned &&
                room.id != floor.keyRoomId)
            {
                candidates.Add(room);
            }
        }

        int assignedCount = Math.Min(requestedCount, candidates.Count);

        var random = new System.Random(
            unchecked(floor.seed ^ 0x19C45A27));

        var bestCandidates = new List<int>();

        for (int slot = 0; slot < assignedCount; slot++)
        {
            double targetDistance =
                maximumDistance * ((slot + 1.0) / (assignedCount + 1.0));

            double bestDifference = double.PositiveInfinity;
            bestCandidates.Clear();

            for (int i = 0; i < candidates.Count; i++)
            {
                double difference = Math.Abs(
                    candidates[i].distanceFromStart - targetDistance);

                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestCandidates.Clear();
                    bestCandidates.Add(i);
                }
                else if (difference == bestDifference)
                {
                    bestCandidates.Add(i);
                }
            }

            int chosenIndex =
                bestCandidates[random.Next(bestCandidates.Count)];

            RoomData selected = candidates[chosenIndex];
            candidates.RemoveAt(chosenIndex);

            selected.type = DungeonRoomType.Healing;
        }

        return assignedCount;
    }

    public static int AssignChests(
        DungeonFloorData floor,
        int requestedCount)
    {
        if (floor == null ||
            floor.rooms == null ||
            floor.corridors == null)
        {
            throw new ArgumentException(
                "El piso no contiene los datos necesarios.");
        }

        if (requestedCount < 0)
        {
            throw new ArgumentException(
                "La cantidad de habitaciones de cofre no puede ser negativa.");
        }

        if (floor.keyRoomId < 0 ||
            floor.keyRoomId >= floor.rooms.Count)
        {
            throw new ArgumentException(
                "Planifica primero la ubicación de la llave.");
        }

        var leaves = new List<RoomData>();
        var others = new List<RoomData>();

        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i)
            {
                throw new ArgumentException(
                    "Los IDs de habitación deben coincidir con sus índices.");
            }

            if (room.type == DungeonRoomType.Chest)
            {
                throw new ArgumentException(
                    "Asigna los cofres sobre un piso sin cofres previamente asignados.");
            }

            if (room.type != DungeonRoomType.Unassigned ||
                room.id == floor.keyRoomId)
            {
                continue;
            }

            if (room.distanceFromStart < 0)
            {
                throw new ArgumentException(
                    $"La habitación {room.id} no tiene una distancia válida.");
            }

            int connections = 0;

            foreach (Corridor corridor in floor.corridors)
            {
                if (corridor == null)
                    throw new ArgumentException("Hay un pasillo nulo.");

                if (corridor.roomA == room.id ||
                    corridor.roomB == room.id)
                {
                    connections++;
                }
            }

            if (connections == 0)
            {
                throw new ArgumentException(
                    $"La habitación {room.id} no tiene conexiones.");
            }

            if (connections == 1)
                leaves.Add(room);
            else
                others.Add(room);
        }

        int assignedCount = Math.Min(
            requestedCount,
            leaves.Count + others.Count);

        var random = new System.Random(
            unchecked(floor.seed ^ 0x2B73D915));

        for (int i = 0; i < assignedCount; i++)
        {
            List<RoomData> candidates =
                leaves.Count > 0 ? leaves : others;

            int index = random.Next(candidates.Count);

            RoomData selected = candidates[index];
            candidates.RemoveAt(index);

            selected.type = DungeonRoomType.Chest;
        }

        return assignedCount;
    }

    public static void AssignRemaining(
    DungeonFloorData floor,
    float combatProbability)
    {
        if (floor == null || floor.rooms == null)
            throw new ArgumentException("El piso no contiene habitaciones.");

        if (float.IsNaN(combatProbability) ||
            float.IsInfinity(combatProbability) ||
            combatProbability < 0f ||
            combatProbability > 1f)
        {
            throw new ArgumentException(
                "La probabilidad de combate debe estar entre cero y uno.");
        }

        int startCount = 0;
        int stairsCount = 0;

        // Validamos antes de modificar los tipos.
        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i)
            {
                throw new ArgumentException(
                    "Las habitaciones deben tener IDs consecutivos " +
                    "que coincidan con sus índices.");
            }

            if (room.type == DungeonRoomType.Start)
                startCount++;

            if (room.type == DungeonRoomType.Stairs)
                stairsCount++;
        }

        if (startCount != 1 || stairsCount != 1)
        {
            throw new ArgumentException(
                "Debe existir exactamente una habitación inicial " +
                "y una habitación de escaleras.");
        }

        var random = new System.Random(
            unchecked(floor.seed ^ 0x51A7C39D));

        foreach (RoomData room in floor.rooms)
        {
            bool eligible =
                room.type == DungeonRoomType.Unassigned ||
                room.type == DungeonRoomType.Empty ||
                room.type == DungeonRoomType.Combat;

            if (!eligible)
                continue;

            room.type = random.NextDouble() < combatProbability
                ? DungeonRoomType.Combat
                : DungeonRoomType.Empty;
        }
    }

    public static Vector2Int FindReferenceCell(
        DungeonFloorData floor,
        int roomId)
    {
        if (floor == null ||
            floor.rooms == null ||
            floor.cells == null ||
            roomId < 0 ||
            roomId >= floor.rooms.Count ||
            floor.rooms[roomId] == null)
        {
            throw new ArgumentException(
                "No se puede consultar la habitación indicada.");
        }

        Vector2 center =
            RoomConnectionPlanner.RoomCenter(floor.rooms[roomId]);

        bool found = false;
        Vector2Int best = default;
        float bestDistance = float.PositiveInfinity;

        foreach (DungeonCellData cell in floor.cells)
        {
            if (cell.roomId != roomId ||
                (cell.occupancy & CellOccupancy.Blocked) != 0)
            {
                continue;
            }

            float distance =
                ((Vector2)cell.coordinate - center).sqrMagnitude;

            bool winsTie =
                distance == bestDistance &&
                (cell.coordinate.x < best.x ||
                 (cell.coordinate.x == best.x &&
                  cell.coordinate.y < best.y));

            if (!found || distance < bestDistance || winsTie)
            {
                found = true;
                best = cell.coordinate;
                bestDistance = distance;
            }
        }

        if (!found)
        {
            throw new ArgumentException(
                $"La habitación {roomId} no tiene casillas transitables.");
        }

        return best;
    }

    public static int AssignUpgrades(DungeonFloorData floor, int requestedCount)
    {
        if (floor == null || floor.rooms == null)
            throw new ArgumentException("El piso no contiene habitaciones.");

        if (requestedCount < 0)
        {
            throw new ArgumentException(
                "La cantidad de habitaciones de mejora no puede ser negativa.");
        }

        if (floor.keyRoomId < 0 ||
            floor.keyRoomId >= floor.rooms.Count)
        {
            throw new ArgumentException(
                "Planifica primero la ubicación de la llave.");
        }

        var candidates = new List<RoomData>();
        var anchors = new List<RoomData>();
        int startCount = 0;

        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i ||
                room.distanceFromStart < 0)
            {
                throw new ArgumentException(
                    "Las habitaciones deben tener IDs y distancias válidos.");
            }

            if (room.type == DungeonRoomType.Upgrade)
            {
                throw new ArgumentException(
                    "Asigna las mejoras sobre un piso sin mejoras previas.");
            }

            if (room.type == DungeonRoomType.Start)
                startCount++;

            if (room.type == DungeonRoomType.Start ||
                room.type == DungeonRoomType.Healing)
            {
                anchors.Add(room);
            }

            if (room.type == DungeonRoomType.Unassigned &&
                room.id != floor.keyRoomId)
            {
                candidates.Add(room);
            }
        }

        if (startCount != 1)
        {
            throw new ArgumentException(
                "Debe existir exactamente una habitación inicial.");
        }

        int assignedCount = Math.Min(requestedCount, candidates.Count);

        if (assignedCount == 0)
            return 0;

        var distanceMaps =
            new List<Dictionary<Vector2Int, int>>();

        foreach (RoomData anchor in anchors)
        {
            Vector2Int origin = FindReferenceCell(floor, anchor.id);
            distanceMaps.Add(DungeonDistanceMap.Build(floor, origin));
        }

        var random = new System.Random(
            unchecked(floor.seed ^ 0x36B7A125));

        var selectedRooms = new List<RoomData>();
        var bestCandidates = new List<int>();

        for (int slot = 0; slot < assignedCount; slot++)
        {
            int bestSeparation = -1;
            bestCandidates.Clear();

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int reference =
                    FindReferenceCell(floor, candidates[i].id);

                int nearestDistance = int.MaxValue;

                foreach (var map in distanceMaps)
                {
                    if (!map.TryGetValue(reference, out int distance))
                    {
                        throw new ArgumentException(
                            $"La habitación {candidates[i].id} " +
                            "no es accesible desde un punto de referencia.");
                    }

                    nearestDistance = Math.Min(nearestDistance, distance);
                }

                if (nearestDistance > bestSeparation)
                {
                    bestSeparation = nearestDistance;
                    bestCandidates.Clear();
                    bestCandidates.Add(i);
                }
                else if (nearestDistance == bestSeparation)
                {
                    bestCandidates.Add(i);
                }
            }

            int chosenIndex =
                bestCandidates[random.Next(bestCandidates.Count)];

            RoomData selected = candidates[chosenIndex];
            candidates.RemoveAt(chosenIndex);
            selectedRooms.Add(selected);

            if (slot + 1 < assignedCount)
            {
                Vector2Int origin = FindReferenceCell(floor, selected.id);
                distanceMaps.Add(DungeonDistanceMap.Build(floor, origin));
            }
        }

        foreach (RoomData room in selectedRooms)
            room.type = DungeonRoomType.Upgrade;

        return selectedRooms.Count;
    }

    public static int AssignEvents(
    DungeonFloorData floor,
    int requestedCount)
    {
        if (floor == null || floor.rooms == null || floor.cells == null)
            throw new ArgumentException("El piso no contiene datos completos.");

        if (requestedCount < 0)
        {
            throw new ArgumentException(
                "La cantidad de habitaciones de evento no puede ser negativa.");
        }

        if (floor.keyRoomId < 0 ||
            floor.keyRoomId >= floor.rooms.Count)
        {
            throw new ArgumentException(
                "Planifica primero la ubicación de la llave.");
        }

        var candidates = new List<RoomData>();

        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i ||
                room.distanceFromStart < 0)
            {
                throw new ArgumentException(
                    "Las habitaciones deben tener IDs y distancias válidos.");
            }

            if (room.type == DungeonRoomType.Event)
            {
                throw new ArgumentException(
                    "Asigna los eventos sobre un piso sin eventos previos.");
            }

            if (room.type != DungeonRoomType.Unassigned ||
                room.id == floor.keyRoomId)
            {
                continue;
            }

            if (HasEventSpace(floor, room))
                candidates.Add(room);
        }

        int assignedCount = Math.Min(requestedCount, candidates.Count);

        var random = new System.Random(
            unchecked(floor.seed ^ 0x47A29C13));

        for (int i = 0; i < assignedCount; i++)
        {
            int index = random.Next(candidates.Count);

            RoomData selected = candidates[index];
            candidates.RemoveAt(index);

            selected.type = DungeonRoomType.Event;
        }

        return assignedCount;
    }

    private static bool HasEventSpace(
        DungeonFloorData floor,
        RoomData room)
    {
        Vector2Int center = FindReferenceCell(floor, room.id);

        var available = new HashSet<Vector2Int>();

        foreach (DungeonCellData cell in floor.cells)
        {
            if (cell.roomId != room.id)
                continue;

            bool unavailable =
                (cell.occupancy &
                 (CellOccupancy.Blocked | CellOccupancy.Reserved)) != 0;

            if (!unavailable)
                available.Add(cell.coordinate);
        }

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector2Int coordinate =
                    center + new Vector2Int(x, y);

                if (!available.Contains(coordinate))
                    return false;
            }
        }

        return true;
    }
}