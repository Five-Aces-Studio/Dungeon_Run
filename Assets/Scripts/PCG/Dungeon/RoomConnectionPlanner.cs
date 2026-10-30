using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoomConnectionPlanner
{
    public static List<RoomConnection> BuildMinimumTree(IReadOnlyList<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0)
        {
            throw new ArgumentException(
                "Se necesita al menos una habitación.");
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == null ||
                rooms[i].id != i ||
                rooms[i].bounds.width <= 0 ||
                rooms[i].bounds.height <= 0)
            {
                throw new ArgumentException(
                    "Las habitaciones deben tener tamaños válidos " +
                    "e IDs consecutivos que coincidan con su índice.");
            }
        }

        var connections = new List<RoomConnection>();
        var connected = new bool[rooms.Count];

        connected[0] = true;
        int connectedCount = 1;

        while (connectedCount < rooms.Count)
        {
            int bestA = -1;
            int bestB = -1;
            float bestCost = float.PositiveInfinity;

            for (int a = 0; a < rooms.Count; a++)
            {
                if (!connected[a])
                    continue;

                for (int b = 0; b < rooms.Count; b++)
                {
                    if (connected[b])
                        continue;

                    float cost = ConnectionCost(rooms[a], rooms[b]);

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (bestA < 0 || bestB < 0)
            {
                throw new ArgumentException(
                    "No se pudo completar el árbol de conexiones.");
            }

            connections.Add(new RoomConnection
            {
                roomA = rooms[bestA].id,
                roomB = rooms[bestB].id
            });

            connected[bestB] = true;
            connectedCount++;
        }

        return connections;
    }

    public static List<RoomConnection> BuildGraph(
    IReadOnlyList<RoomData> rooms,
    int extraConnections)
    {
        if (extraConnections < 0)
        {
            throw new ArgumentException(
                "La cantidad de conexiones extra no puede ser negativa.");
        }

        List<RoomConnection> connections = BuildMinimumTree(rooms);

        if (extraConnections == 0)
            return connections;

        int count = rooms.Count;
        var degrees = new int[count];
        var linked = new bool[count, count];

        foreach (RoomConnection connection in connections)
        {
            int a = connection.roomA;
            int b = connection.roomB;

            degrees[a]++;
            degrees[b]++;

            linked[a, b] = true;
            linked[b, a] = true;
        }

        int protectedLeaf = -1;

        for (int i = 0; i < count; i++)
        {
            if (degrees[i] == 1)
            {
                protectedLeaf = i;
                break;
            }
        }

        var candidates = new List<RoomConnection>();

        for (int a = 0; a < count; a++)
        {
            for (int b = a + 1; b < count; b++)
            {
                if (linked[a, b])
                    continue;

                if (a == protectedLeaf || b == protectedLeaf)
                    continue;

                candidates.Add(new RoomConnection
                {
                    roomA = a,
                    roomB = b
                });
            }
        }

        if (extraConnections > candidates.Count)
        {
            throw new ArgumentException(
                $"Se han pedido {extraConnections} conexiones extra, " +
                $"pero solo caben {candidates.Count} conservando una hoja.");
        }

        candidates.Sort((first, second) =>
        {
            float firstCost = ConnectionCost(rooms[first.roomA], rooms[first.roomB]);

            float secondCost = ConnectionCost(rooms[second.roomA], rooms[second.roomB]);

            int comparison = firstCost.CompareTo(secondCost);

            if (comparison != 0)
                return comparison;

            comparison = first.roomA.CompareTo(second.roomA);

            if (comparison != 0)
                return comparison;

            return first.roomB.CompareTo(second.roomB);
        });

        for (int i = 0; i < extraConnections; i++)
            connections.Add(candidates[i]);

        return connections;
    }

    public static Vector2 RoomCenter(RoomData room)
    {
        return new Vector2(
            room.bounds.xMin + (room.bounds.width - 1) * 0.5f,
            room.bounds.yMin + (room.bounds.height - 1) * 0.5f);
    }

    private static float ConnectionCost(RoomData a, RoomData b)
    {
        Vector2 centerA = RoomCenter(a);
        Vector2 centerB = RoomCenter(b);

        return Mathf.Abs(centerA.x - centerB.x) +
               Mathf.Abs(centerA.y - centerB.y);
    }
}