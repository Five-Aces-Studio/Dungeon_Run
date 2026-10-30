using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RoomDoorCandidate
{
    public Vector2Int startCell;

    public Vector2Int outward;

    public int width;

    public Vector2Int Along => new Vector2Int(-outward.y, outward.x);
}

[Serializable]
public sealed class Corridor
{
    public int roomA;
    public int roomB;

    public RoomDoorCandidate doorA;
    public RoomDoorCandidate doorB;

    public List<Vector2Int> cells = new List<Vector2Int>();
}

[Serializable]
public sealed class DungeonGenerationSettings
{
    public int seed = 12345;

    [Min(0)] public int extraConnections = 0;
    [Min(0.01f)] public float cellSize = 1f;
    [Min(0.01f)] public float avatarRadius = 0.35f;
    [Min(0f)] public float clearance = 0.10f;

    [Min(1)] public int minimumOpeningCells = 1;
    [Min(3)] public int minimumRoomSide = 7;
    [Min(3)] public int maximumRoomSide = 11;

    public void Validate()
    {
        if (extraConnections < 0)
        {
            throw new ArgumentException(
                "La cantidad de conexiones extra no puede ser negativa.");
        }

        if (!IsFinite(cellSize) || cellSize <= 0f ||
            !IsFinite(avatarRadius) || avatarRadius <= 0f ||
            !IsFinite(clearance) || clearance < 0f)
        {
            throw new ArgumentException(
                "Las medidas deben ser finitas y válidas.");
        }

        if (minimumRoomSide < 3 ||
            maximumRoomSide < minimumRoomSide ||
            maximumRoomSide > 64)
        {
            throw new ArgumentException(
                "Los lados deben cumplir: 3 <= mínimo <= máximo <= 64.");
        }

        if (minimumOpeningCells < 1 ||
            minimumOpeningCells > minimumRoomSide)
        {
            throw new ArgumentException(
                "La abertura debe caber en la habitación más pequeña.");
        }

        float openingWidth = minimumOpeningCells * cellSize;
        float requiredWidth = 2f * (avatarRadius + clearance);

        if (!IsFinite(openingWidth) ||
            !IsFinite(requiredWidth) ||
            openingWidth < requiredWidth)
        {
            throw new ArgumentException(
                "La abertura mínima no permite pasar al avatar con margen.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Flags]
public enum CellOccupancy
{
    None,
    Blocked,
    Reserved
}

[Serializable]
public struct DungeonCellData
{
    public Vector2Int coordinate;
    public const int CorridorRoomId = -1;
    public int roomId;
    public CellOccupancy occupancy;
}

public enum DungeonRoomType
{
    Unassigned,
    Start,
    Stairs,
    Empty,
    Combat,
    Chest,
    Healing,
    Upgrade,
    Event
}

[Serializable]
public sealed class RoomData
{
    public int id;
    public RectInt bounds;
    public int distanceFromStart = -1;
    public bool hasReservedCenter;
    public Vector2Int reservedCenter;
    public DungeonRoomType type = DungeonRoomType.Unassigned;
    public List<RoomDoorCandidate> doorCandidates = new List<RoomDoorCandidate>();
}

[Serializable]
public sealed class DungeonFloorData
{
    public List<DungeonObjectData> objects = new List<DungeonObjectData>();
    public int seed;
    public float cellSize;
    public int corridorBuildAttempts;
    public int keyRoomId = -1;
    public Vector2Int keyCell;
    public List<RectInt> regions = new List<RectInt>();
    public List<RoomData> rooms = new List<RoomData>();
    public List<DungeonCellData> cells = new List<DungeonCellData>();
    public List<RoomConnection> plannedConnections = new List<RoomConnection>();
    public List<Corridor> corridors = new List<Corridor>();

    public Vector3 CellCenter(Vector2Int coordinate)
    {
        return new Vector3(
            coordinate.x * cellSize,
            0f,
            coordinate.y * cellSize);
    }
}

[Serializable]
public struct RoomConnection
{
    public int roomA;
    public int roomB;
}
