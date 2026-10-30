using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonPassageReservations
{
    public static void Build(DungeonFloorData floor)
    {
        if (floor == null ||
            floor.cells == null ||
            floor.rooms == null ||
            floor.corridors == null)
        {
            throw new ArgumentException(
                "El piso no contiene los datos necesarios.");
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

        foreach (Corridor corridor in floor.corridors)
        {
            if (corridor == null ||
                corridor.cells == null ||
                corridor.cells.Count == 0 ||
                corridor.roomA < 0 ||
                corridor.roomA >= floor.rooms.Count ||
                corridor.roomB < 0 ||
                corridor.roomB >= floor.rooms.Count ||
                corridor.roomA == corridor.roomB)
            {
                throw new ArgumentException(
                    "Hay un pasillo con datos inválidos.");
            }

            foreach (Vector2Int coordinate in corridor.cells)
            {
                ReserveCell(
                    coordinate,
                    DungeonCellData.CorridorRoomId);
            }

            ReserveDoor(corridor.doorA, corridor.roomA);
            ReserveDoor(corridor.doorB, corridor.roomB);
        }

        foreach (int index in pendingReservations)
        {
            DungeonCellData cell = floor.cells[index];
            cell.occupancy |= CellOccupancy.Reserved;
            floor.cells[index] = cell;
        }

        void ReserveDoor(RoomDoorCandidate door, int roomId)
        {
            if (door.width != 1 ||
                Math.Abs(door.outward.x) +
                Math.Abs(door.outward.y) != 1)
            {
                throw new ArgumentException(
                    "Se esperaba una puerta cardinal de una casilla.");
            }

            ReserveCell(door.startCell, roomId);

            ReserveCell(
                door.startCell + door.outward,
                DungeonCellData.CorridorRoomId);
        }

        void ReserveCell(Vector2Int coordinate, int expectedRoomId)
        {
            if (!cellIndices.TryGetValue(coordinate, out int index))
            {
                throw new ArgumentException(
                    $"Falta suelo en el paso {coordinate}.");
            }

            DungeonCellData cell = floor.cells[index];

            if (cell.roomId != expectedRoomId)
            {
                throw new ArgumentException(
                    $"La casilla {coordinate} no pertenece " +
                    "a la habitación o pasillo esperado.");
            }

            if ((cell.occupancy & CellOccupancy.Blocked) != 0)
            {
                throw new ArgumentException(
                    $"El paso {coordinate} está bloqueado.");
            }

            pendingReservations.Add(index);
        }
    }
}