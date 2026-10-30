using System;
using UnityEngine;

public static class SimpleDungeonGenerator
{
    private const int GeometrySalt = 0x41C64E6D;

    public static DungeonFloorData Generate(
        DungeonGenerationSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        settings.Validate();

        var geometryRandom = new System.Random(
            unchecked(settings.seed ^ GeometrySalt));

        int width = geometryRandom.Next(
            settings.minimumRoomSide,
            settings.maximumRoomSide + 1);

        int depth = geometryRandom.Next(
            settings.minimumRoomSide,
            settings.maximumRoomSide + 1);

        var floor = new DungeonFloorData
        {
            seed = settings.seed,
            cellSize = settings.cellSize
        };

        floor.rooms.Add(new RoomData
        {
            id = 0,
            bounds = new RectInt(0, 0, width, depth)
        });

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                floor.cells.Add(new DungeonCellData
                {
                    coordinate = new Vector2Int(x, z),
                    roomId = 0,
                    occupancy = CellOccupancy.None
                });
            }
        }

        return floor;
    }

    public static DungeonFloorData Generate(DungeonGenerationSettings settings, RoomPreset preset, Vector2Int origin = default, int quarterTurns = 0)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (preset == null)
            throw new ArgumentException("Asigna un RoomPreset en el Inspector.");

        settings.Validate();
        preset.Validate();

        if (preset.Width < settings.minimumRoomSide ||
            preset.Depth < settings.minimumRoomSide ||
            preset.Width > settings.maximumRoomSide ||
            preset.Depth > settings.maximumRoomSide)
        {
            throw new ArgumentException(
                "El preset no cabe en el intervalo de tamaños configurado.");
        }

        var placement = new RoomGridTransform(
            new Vector2Int(preset.Width, preset.Depth),
            origin,
            quarterTurns);

        var floor = new DungeonFloorData
        {
            seed = settings.seed,
            cellSize = settings.cellSize
        };

        var room = new RoomData
        {
            id = 0,
            bounds = new RectInt(origin, placement.Size)
        };

        foreach (RoomDoorCandidate candidate in
                 preset.CreateDoorCandidates(settings.minimumOpeningCells))
        {
            RoomDoorCandidate placedDoor = candidate;

            placedDoor.startCell =
                placement.TransformCell(candidate.startCell);

            placedDoor.outward =
                placement.TransformDirection(candidate.outward);

            room.doorCandidates.Add(placedDoor);
        }

        floor.rooms.Add(room);

        foreach (Vector2Int coordinate in preset.CreateFloorMask())
        {
            floor.cells.Add(new DungeonCellData
            {
                coordinate = placement.TransformCell(coordinate),
                roomId = 0,
                occupancy = CellOccupancy.None
            });
        }

        return floor;
    }
}
