using System;
using UnityEngine;

public readonly struct RoomGridTransform
{
    private readonly Vector2Int originalSize;
    private readonly Vector2Int origin;
    private readonly int quarterTurns;

    public Vector2Int Size =>
        quarterTurns % 2 == 0
            ? originalSize
            : new Vector2Int(originalSize.y, originalSize.x);

    public RoomGridTransform(Vector2Int originalSize, Vector2Int origin, int quarterTurns)
    {
        if (originalSize.x <= 0 || originalSize.y <= 0)
            throw new ArgumentException("El tamaño debe ser positivo.");

        if (quarterTurns < 0 || quarterTurns > 3)
            throw new ArgumentException("Los giros deben estar entre 0 y 3.");

        this.originalSize = originalSize;
        this.origin = origin;
        this.quarterTurns = quarterTurns;
    }

    public Vector2Int TransformCell(Vector2Int cell)
    {
        Vector2Int rotated;

        switch (quarterTurns)
        {
            case 1:
                rotated = new Vector2Int(
                    originalSize.y - 1 - cell.y,
                    cell.x);
                break;

            case 2:
                rotated = new Vector2Int(
                    originalSize.x - 1 - cell.x,
                    originalSize.y - 1 - cell.y);
                break;

            case 3:
                rotated = new Vector2Int(
                    cell.y,
                    originalSize.x - 1 - cell.x);
                break;

            default:
                rotated = cell;
                break;
        }

        return origin + rotated;
    }

    public Vector2Int TransformDirection(Vector2Int direction)
    {
        switch (quarterTurns)
        {
            case 1:
                return new Vector2Int(-direction.y, direction.x);

            case 2:
                return new Vector2Int(-direction.x, -direction.y);

            case 3:
                return new Vector2Int(direction.y, -direction.x);

            default:
                return direction;
        }
    }
}