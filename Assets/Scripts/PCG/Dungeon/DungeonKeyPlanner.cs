using System;
using UnityEngine;

public static class DungeonKeyPlanner
{
    public static void Assign(DungeonFloorData floor)
    {
        if (floor == null || floor.rooms == null)
            throw new ArgumentException("El piso no contiene habitaciones.");

        int startRoomId = -1;
        int stairsRoomId = -1;

        foreach (RoomData room in floor.rooms)
        {
            if (room == null)
                throw new ArgumentException("Hay una habitación nula.");

            if (room.type == DungeonRoomType.Start)
            {
                if (startRoomId >= 0)
                {
                    throw new ArgumentException(
                        "Hay más de una habitación inicial.");
                }

                startRoomId = room.id;
            }

            if (room.type == DungeonRoomType.Stairs)
            {
                if (stairsRoomId >= 0)
                {
                    throw new ArgumentException(
                        "Hay más de una habitación de escaleras.");
                }

                stairsRoomId = room.id;
            }
        }

        if (startRoomId < 0 || stairsRoomId < 0)
        {
            throw new ArgumentException(
                "Asigna primero las habitaciones de inicio y escaleras.");
        }

        Vector2Int start =
            DungeonRoomTypeAssigner.FindReferenceCell(floor, startRoomId);

        var reachable = DungeonDistanceMap.Build(
            floor,
            start,
            stairsRoomId);

        int selectedRoomId = startRoomId;
        Vector2Int selectedCell = start;
        int selectedDistance = -1;

        foreach (RoomData room in floor.rooms)
        {
            if (room.id == startRoomId || room.id == stairsRoomId)
                continue;

            Vector2Int candidate =
                DungeonRoomTypeAssigner.FindReferenceCell(floor, room.id);

            if (!reachable.TryGetValue(candidate, out int distance))
                continue;

            bool betterCandidate =
                distance > selectedDistance ||
                (distance == selectedDistance &&
                 room.id < selectedRoomId);

            if (!betterCandidate)
                continue;

            selectedRoomId = room.id;
            selectedCell = candidate;
            selectedDistance = distance;
        }

        floor.keyRoomId = selectedRoomId;
        floor.keyCell = selectedCell;
    }
}