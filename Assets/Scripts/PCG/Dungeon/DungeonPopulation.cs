using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonObjectKind { Key, Door, Stairs, Chest, Healing, Upgrade, Event, Enemy, Boss, Decoration }
[Serializable]
public sealed class DungeonObjectData
{
    public int id, roomId;
    public DungeonObjectKind kind;
    public Vector2Int cell;
    public bool blocking, consumed;
    public Vector2Int facing = Vector2Int.up;
}

/// <summary>One-cell footprints. Presentation must fit within these footprints.</summary>
public static class DungeonPopulation
{
    public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    public static DungeonFloorData Prepare(DungeonFloorData source, bool bossFloor, int decorationPerRoom, int enemiesPerRoom)
    {
        if (source == null || decorationPerRoom < 0 || enemiesPerRoom < 0 || enemiesPerRoom > 3)
            throw new ArgumentException("Invalid population settings.");
        var floor = JsonUtility.FromJson<DungeonFloorData>(JsonUtility.ToJson(source));
        floor.objects.Clear();
        var indices = Indices(floor);
        var occupied = new HashSet<Vector2Int>();
        RoomData stairs = floor.rooms.Find(r => r.type == DungeonRoomType.Stairs);
        if (stairs == null) throw new ArgumentException("Missing stairs.");
        foreach (RoomData room in floor.rooms)
        {
            DungeonObjectKind kind;
            switch (room.type)
            {
                case DungeonRoomType.Stairs: kind = DungeonObjectKind.Stairs; break;
                case DungeonRoomType.Chest: kind = DungeonObjectKind.Chest; break;
                case DungeonRoomType.Healing: kind = DungeonObjectKind.Healing; break;
                case DungeonRoomType.Upgrade: kind = DungeonObjectKind.Upgrade; break;
                case DungeonRoomType.Event: kind = DungeonObjectKind.Event; break;
                default: continue;
            }
            if (!room.hasReservedCenter) throw new ArgumentException("Missing content reservation.");
            Add(kind, room.reservedCenter, room.id, true);
        }
        Add(DungeonObjectKind.Key, floor.keyCell, floor.keyRoomId, false);
        foreach (Corridor corridor in floor.corridors)
        {
            if (corridor.roomA == stairs.id) Gate(corridor.doorA);
            if (corridor.roomB == stairs.id) Gate(corridor.doorB);
        }
        var random = new System.Random(unchecked(floor.seed ^ 0x193A742));
        foreach (RoomData room in floor.rooms)
        {
            var candidates = new List<Vector2Int>();
            foreach (var cell in floor.cells)
                if (cell.roomId == room.id && cell.occupancy == CellOccupancy.None && !occupied.Contains(cell.coordinate))
                    candidates.Add(cell.coordinate);
            candidates.Sort((a,b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            for (int i = candidates.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); var c = candidates[i]; candidates[i] = candidates[j]; candidates[j] = c; }
            int enemies = room.type == DungeonRoomType.Combat && enemiesPerRoom > 0 ? random.Next(1, enemiesPerRoom + 1) : 0;
            foreach (Vector2Int cell in candidates)
            {
                if (enemies <= 0) break;
                Add(DungeonObjectKind.Enemy, cell, room.id, true); enemies--;
            }
            int decorations = 0;
            foreach (Vector2Int cell in candidates)
            {
                if (decorations >= decorationPerRoom) break;
                if (occupied.Contains(cell)) continue;
                bool nearWall = false;
                foreach (var d in Directions) if (!indices.ContainsKey(cell + d)) nearWall = true;
                if (!nearWall) continue;
                Add(DungeonObjectKind.Decoration, cell, room.id, true); decorations++;
            }
        }
        if (bossFloor)
        {
            var boss = floor.objects.Find(o => o.kind == DungeonObjectKind.Enemy);
            if (boss != null) boss.kind = DungeonObjectKind.Boss;
            else
            {
                int startId = floor.rooms.Find(r => r.type == DungeonRoomType.Start).id;
                foreach (var cell in floor.cells)
                    if (cell.roomId >= 0 && cell.roomId != stairs.id && cell.roomId != startId &&
                        cell.occupancy == CellOccupancy.None && !occupied.Contains(cell.coordinate))
                    { Add(DungeonObjectKind.Boss, cell.coordinate, cell.roomId, true); break; }
            }
            if (!floor.objects.Exists(o => o.kind == DungeonObjectKind.Boss)) throw new ArgumentException("No room for prototype boss.");
        }
        // Bounded recovery: remove optional objects with no interaction approach.
        bool changed;
        do
        {
            changed = false;
            var reachable = OpenDistances(floor);
            for (int i = floor.objects.Count - 1; i >= 0; i--)
            {
                var item = floor.objects[i];
                if (item.kind != DungeonObjectKind.Enemy && item.kind != DungeonObjectKind.Decoration) continue;
                if (HasApproach(reachable, item.cell)) continue;
                SetBlocked(floor, item.cell, false); floor.objects.RemoveAt(i); changed = true;
            }
        } while (changed);
        Validate(floor);
        return floor;

        void Gate(RoomDoorCandidate door)
        {
            if (door.width != 1) throw new ArgumentException("Only one-cell gates supported.");
            if (occupied.Contains(door.startCell)) return;
            Add(DungeonObjectKind.Door, door.startCell, stairs.id, true).facing = door.outward;
        }
        DungeonObjectData Add(DungeonObjectKind kind, Vector2Int coordinate, int roomId, bool blocked)
        {
            if (!indices.TryGetValue(coordinate, out int index) || !occupied.Add(coordinate))
                throw new ArgumentException("Invalid or overlapping content at " + coordinate);
            var item = new DungeonObjectData { id = floor.objects.Count, kind = kind, cell = coordinate, roomId = roomId, blocking = blocked };
            floor.objects.Add(item);
            if (blocked) { var cell = floor.cells[index]; cell.occupancy |= CellOccupancy.Blocked; floor.cells[index] = cell; }
            return item;
        }
    }
    public static Dictionary<Vector2Int,int> Indices(DungeonFloorData floor)
    {
        var result = new Dictionary<Vector2Int,int>();
        for (int i=0;i<floor.cells.Count;i++) result.Add(floor.cells[i].coordinate,i);
        return result;
    }
    public static void SetBlocked(DungeonFloorData floor, Vector2Int coordinate, bool blocked)
    {
        int index = floor.cells.FindIndex(c => c.coordinate == coordinate);
        if (index < 0) throw new ArgumentException("Missing cell.");
        var cell = floor.cells[index];
        if (blocked) cell.occupancy |= CellOccupancy.Blocked; else cell.occupancy &= ~CellOccupancy.Blocked;
        floor.cells[index] = cell;
    }
    public static bool HasApproach(Dictionary<Vector2Int,int> reachable, Vector2Int cell)
    { foreach (var d in Directions) if (reachable.ContainsKey(cell+d)) return true; return false; }
    public static Dictionary<Vector2Int,int> OpenDistances(DungeonFloorData floor)
    {
        var copy = new DungeonFloorData { cellSize = floor.cellSize, cells = new List<DungeonCellData>(floor.cells) };
        foreach (var item in floor.objects) if (item.kind == DungeonObjectKind.Door) SetBlocked(copy,item.cell,false);
        return DungeonDistanceMap.Build(copy, floor.rooms.Find(r => r.type == DungeonRoomType.Start).reservedCenter);
    }
    public static void Validate(DungeonFloorData floor)
    {
        var closed = DungeonDistanceMap.Build(floor,floor.rooms.Find(r => r.type == DungeonRoomType.Start).reservedCenter);
        if (!closed.ContainsKey(floor.keyCell)) throw new ArgumentException("Key is behind a gate or obstacle.");
        var stairs = floor.rooms.Find(r => r.type == DungeonRoomType.Stairs);
        foreach (var cell in floor.cells)
            if (cell.roomId == stairs.id && closed.ContainsKey(cell.coordinate))
                throw new ArgumentException("An alternate entrance bypasses the stairs lock.");
        var open = OpenDistances(floor);
        foreach (var corridor in floor.corridors)
            if (!open.ContainsKey(corridor.doorA.startCell) || !open.ContainsKey(corridor.doorB.startCell))
                throw new ArgumentException("Decoration disconnected a door.");
        foreach (var item in floor.objects)
            if (item.kind != DungeonObjectKind.Decoration && item.kind != DungeonObjectKind.Key && !HasApproach(open,item.cell))
                throw new ArgumentException("No interaction approach for " + item.kind);
    }
}
