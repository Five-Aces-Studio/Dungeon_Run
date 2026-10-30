using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomShape
{
    Square,
    Rectangle,
    Circle
}

[CreateAssetMenu(fileName = "RoomPreset", menuName = "Dungeon Run/Room Preset")]
public sealed class RoomPreset : ScriptableObject
{
    [SerializeField] private RoomShape shape = RoomShape.Square;
    [SerializeField, Min(3)] private int width = 7;
    [SerializeField, Min(3)] private int depth = 7;

    public RoomShape Shape => shape;
    public int Width => width;
    public int Depth => depth;

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(RoomShape), shape))
            throw new ArgumentException("Forma de habitación desconocida.");

        if (width < 3 || depth < 3 || width > 64 || depth > 64)
        {
            throw new ArgumentException(
                "Las dimensiones del preset deben estar entre 3 y 64.");
        }

        if (shape != RoomShape.Rectangle && width != depth)
        {
            throw new ArgumentException(
                "Los presets cuadrados y circulares requieren ancho y fondo iguales.");
        }

        if (shape == RoomShape.Circle && width % 2 == 0)
        {
            throw new ArgumentException(
                "Por ahora, el círculo requiere un diámetro impar.");
        }
    }

    public List<Vector2Int> CreateFloorMask()
    {
        Validate();

        var cells = new List<Vector2Int>();

        float centerX = (width - 1) * 0.5f;
        float centerZ = (depth - 1) * 0.5f;
        float radius = (width - 1) * 0.5f;

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (shape == RoomShape.Circle)
                {
                    float dx = x - centerX;
                    float dz = z - centerZ;

                    if (dx * dx + dz * dz > radius * radius)
                        continue;
                }

                cells.Add(new Vector2Int(x, z));
            }
        }

        return cells;
    }

    public List<RoomDoorCandidate> CreateDoorCandidates(int openingWidth)
    {
        if (openingWidth < 1 || openingWidth > 64)
        {
            throw new ArgumentException(
                "La anchura de puerta debe estar entre 1 y 64 casillas.");
        }

        List<Vector2Int> orderedCells = CreateFloorMask();
        var floor = new HashSet<Vector2Int>(orderedCells);
        var candidates = new List<RoomDoorCandidate>();

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int outward in directions)
        {
            Vector2Int along =
                new Vector2Int(-outward.y, outward.x);

            foreach (Vector2Int start in orderedCells)
            {
                bool valid = true;

                for (int offset = 0; offset < openingWidth; offset++)
                {
                    Vector2Int inside = start + along * offset;
                    Vector2Int outside = inside + outward;
                    Vector2Int approach = inside - outward;

                    if (!floor.Contains(inside) ||
                        floor.Contains(outside) ||
                        !floor.Contains(approach))
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                    continue;

                candidates.Add(new RoomDoorCandidate
                {
                    startCell = start,
                    outward = outward,
                    width = openingWidth
                });
            }
        }

        if (candidates.Count == 0)
        {
            throw new ArgumentException(
                "El preset no tiene accesos rectos válidos con esa anchura.");
        }

        return candidates;
    }
}