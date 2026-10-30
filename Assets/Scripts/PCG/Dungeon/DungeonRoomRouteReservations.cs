using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonRoomRouteReservations
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    public static void Build(DungeonFloorData floor)
    {
        if (floor == null ||
            floor.rooms == null ||
            floor.cells == null ||
            floor.corridors == null)
        {
            throw new ArgumentException("El piso no contiene datos completos.");
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

        foreach (RoomData room in floor.rooms)
        {
            if (room == null)
                throw new ArgumentException("Hay una habitación nula.");

            var doors = new List<Vector2Int>();

            foreach (Corridor corridor in floor.corridors)
            {
                if (corridor == null)
                    throw new ArgumentException("Hay un pasillo nulo.");

                if (corridor.roomA == room.id)
                    doors.Add(corridor.doorA.startCell);

                if (corridor.roomB == room.id)
                    doors.Add(corridor.doorB.startCell);
            }

            if (doors.Count == 0)
            {
                throw new ArgumentException(
                    $"La habitación {room.id} no tiene puertas conectadas.");
            }

            bool avoidCenter =
                room.type == DungeonRoomType.Stairs ||
                room.type == DungeonRoomType.Chest ||
                room.type == DungeonRoomType.Healing ||
                room.type == DungeonRoomType.Upgrade ||
                room.type == DungeonRoomType.Event;

            if ((avoidCenter || room.type == DungeonRoomType.Start) &&
                !room.hasReservedCenter)
            {
                throw new ArgumentException(
                    $"Falta el centro reservado de la habitación {room.id}.");
            }

            var roomFloor = new DungeonFloorData
            {
                cellSize = floor.cellSize
            };

            foreach (DungeonCellData original in floor.cells)
            {
                if (original.roomId != room.id)
                    continue;

                DungeonCellData copy = original;

                if (avoidCenter &&
                    copy.coordinate == room.reservedCenter)
                {
                    copy.occupancy |= CellOccupancy.Blocked;
                }

                roomFloor.cells.Add(copy);
            }

            Vector2Int origin = doors[0];

            foreach (Vector2Int door in doors)
                Connect(door);

            if (avoidCenter)
            {
                bool foundAccess = false;

                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int approach = room.reservedCenter + direction;

                    if (!DungeonPathfinder.TryFind(
                            roomFloor,
                            origin,
                            approach,
                            out List<Vector2Int> path))
                    {
                        continue;
                    }

                    ReservePath(path);
                    foundAccess = true;
                    break;
                }

                if (!foundAccess)
                {
                    throw new ArgumentException(
                        $"No hay acceso al contenido de la habitación {room.id}.");
                }
            }
            else
            {
                Vector2Int target =
                    room.type == DungeonRoomType.Start
                        ? room.reservedCenter
                        : DungeonRoomTypeAssigner.FindReferenceCell(
                            floor, room.id);

                Connect(target);
            }

            if (room.id == floor.keyRoomId)
                Connect(floor.keyCell);

            void Connect(Vector2Int destination)
            {
                if (!DungeonPathfinder.TryFind(
                        roomFloor,
                        origin,
                        destination,
                        out List<Vector2Int> path))
                {
                    throw new ArgumentException(
                        $"No se puede conectar {destination} dentro " +
                        $"de la habitación {room.id}.");
                }

                ReservePath(path);
            }

            void ReservePath(List<Vector2Int> path)
            {
                foreach (Vector2Int coordinate in path)
                    pendingReservations.Add(cellIndices[coordinate]);
            }
        }

        foreach (int index in pendingReservations)
        {
            DungeonCellData cell = floor.cells[index];
            cell.occupancy |= CellOccupancy.Reserved;
            floor.cells[index] = cell;
        }
    }
}