using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonContentReservations
{
    public static void Build(DungeonFloorData floor)
    {
        if (floor == null || floor.rooms == null || floor.cells == null)
            throw new ArgumentException("El piso no contiene datos completos.");

        if (floor.keyRoomId < 0 ||
            floor.keyRoomId >= floor.rooms.Count)
        {
            throw new ArgumentException(
                "Planifica primero la ubicación de la llave.");
        }

        var cellIndices = new Dictionary<Vector2Int, int>();

        for (int i = 0; i < floor.cells.Count; i++)
        {
            Vector2Int coordinate = floor.cells[i].coordinate;

            if (cellIndices.ContainsKey(coordinate))
            {
                throw new ArgumentException(
                    $"Hay una casilla duplicada en {coordinate}.");
            }

            cellIndices.Add(coordinate, i);
        }

        var pendingReservations = new HashSet<int>();
        var centers = new Dictionary<int, Vector2Int>();

        for (int i = 0; i < floor.rooms.Count; i++)
        {
            RoomData room = floor.rooms[i];

            if (room == null || room.id != i)
            {
                throw new ArgumentException(
                    "Los IDs de habitación deben coincidir con sus índices.");
            }

            if (room.hasReservedCenter)
            {
                throw new ArgumentException(
                    "Construye estas reservas una sola vez por piso.");
            }

            bool needsCenter =
                room.type == DungeonRoomType.Start ||
                room.type == DungeonRoomType.Stairs ||
                room.type == DungeonRoomType.Chest ||
                room.type == DungeonRoomType.Healing ||
                room.type == DungeonRoomType.Upgrade ||
                room.type == DungeonRoomType.Event;

            if (!needsCenter)
                continue;

            Vector2Int center =
                DungeonRoomTypeAssigner.FindReferenceCell(floor, room.id);

            int radius = room.type == DungeonRoomType.Start ? 0 : 1;

            ReserveArea(center, room.id, radius);
            centers.Add(room.id, center);
        }

        ReserveArea(floor.keyCell, floor.keyRoomId, 0);

        foreach (int index in pendingReservations)
        {
            DungeonCellData cell = floor.cells[index];
            cell.occupancy |= CellOccupancy.Reserved;
            floor.cells[index] = cell;
        }

        foreach (var entry in centers)
        {
            RoomData room = floor.rooms[entry.Key];
            room.reservedCenter = entry.Value;
            room.hasReservedCenter = true;
        }

        void ReserveArea(Vector2Int center, int roomId, int radius)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    Vector2Int coordinate =
                        center + new Vector2Int(x, y);

                    if (!cellIndices.TryGetValue(coordinate, out int index))
                    {
                        throw new ArgumentException(
                            $"Falta suelo para reservar contenido " +
                            $"en la habitación {roomId}: {coordinate}.");
                    }

                    DungeonCellData cell = floor.cells[index];

                    if (cell.roomId != roomId ||
                        (cell.occupancy &
                         (CellOccupancy.Blocked | CellOccupancy.Reserved)) != 0)
                    {
                        throw new ArgumentException(
                            $"La casilla {coordinate} no está disponible " +
                            $"para el contenido de la habitación {roomId}.");
                    }

                    pendingReservations.Add(index);
                }
            }
        }
    }
}