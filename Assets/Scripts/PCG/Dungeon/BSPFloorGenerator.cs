using System;
using System.Collections.Generic;
using UnityEngine;

public static class BSPFloorGenerator
{
    public static DungeonFloorData Generate(DungeonGenerationSettings settings, RoomPreset[] presets, Vector2Int areaSize, int minimumRegionSide, int roomCount, int margin)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        settings.Validate();

        if (presets == null || presets.Length == 0)
            throw new ArgumentException("Asigna al menos un preset.");

        if (margin < 1 || margin > 64)
        {
            throw new ArgumentException(
                "El margen debe estar entre 1 y 64 casillas.");
        }

        foreach (RoomPreset preset in presets)
            SimpleDungeonGenerator.Generate(settings, preset);

        var regions = BinarySpacePartitioner.Generate(
            areaSize, minimumRegionSide, roomCount, settings.seed);

        var random = new System.Random(
            unchecked(settings.seed ^ 0x61C88647));

        var floor = new DungeonFloorData
        {
            seed = settings.seed,
            cellSize = settings.cellSize,
            regions = regions
        };

        var occupied = new HashSet<Vector2Int>();

        for (int roomId = 0; roomId < regions.Count; roomId++)
        {
            RectInt region = regions[roomId];

            int availableWidth = region.width - margin * 2;
            int availableDepth = region.height - margin * 2;

            var choices =
                new List<(RoomPreset preset, int turns)>();

            foreach (RoomPreset preset in presets)
            {
                for (int turns = 0; turns < 4; turns++)
                {
                    int width = turns % 2 == 0
                        ? preset.Width : preset.Depth;

                    int depth = turns % 2 == 0
                        ? preset.Depth : preset.Width;

                    if (width <= availableWidth &&
                        depth <= availableDepth)
                    {
                        choices.Add((preset, turns));
                    }
                }
            }

            if (choices.Count == 0)
            {
                throw new ArgumentException(
                    $"Ningún preset cabe en la región {roomId} " +
                    $"con margen {margin}. Revisa tamaños y margen.");
            }

            var choice = choices[random.Next(choices.Count)];

            int placedWidth = choice.turns % 2 == 0
                ? choice.preset.Width : choice.preset.Depth;

            int placedDepth = choice.turns % 2 == 0
                ? choice.preset.Depth : choice.preset.Width;

            int x = random.Next(
                region.xMin + margin,
                region.xMax - margin - placedWidth + 1);

            int z = random.Next(
                region.yMin + margin,
                region.yMax - margin - placedDepth + 1);

            DungeonFloorData singleRoom = SimpleDungeonGenerator.Generate(settings, choice.preset, new Vector2Int(x, z), choice.turns);

            RoomData room = singleRoom.rooms[0];
            room.id = roomId;
            floor.rooms.Add(room);

            foreach (DungeonCellData source in singleRoom.cells)
            {
                DungeonCellData cell = source;
                cell.roomId = roomId;

                if (!occupied.Add(cell.coordinate))
                {
                    throw new ArgumentException(
                        $"Dos habitaciones comparten la casilla {cell.coordinate}.");
                }

                floor.cells.Add(cell);
            }
        }

        floor.plannedConnections = RoomConnectionPlanner.BuildGraph(floor.rooms, settings.extraConnections);

        return floor;
    }
}