using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MazeLayout
{
    public static readonly Vector2Int[] Directions =
    {
        Vector2Int.right, Vector2Int.left,
        Vector2Int.up, Vector2Int.down
    };

    private readonly Dictionary<Vector2Int, List<Vector2Int>> links = new();
    public IEnumerable<Vector2Int> Cells => links.Keys;

    public bool CanMove(Vector2Int from, Vector2Int to) =>
        links.TryGetValue(from, out var neighbors) && neighbors.Contains(to);

    public IEnumerable<Vector2Int> Neighbors(Vector2Int cell) => links[cell];

    private void Connect(Vector2Int a, Vector2Int b)
    {
        if (!links[a].Contains(b)) links[a].Add(b);
        if (!links[b].Contains(a)) links[b].Add(a);
    }

    public static MazeLayout Build(int width, int height, int seed, float extraPassageChance)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException();
        var map = new MazeLayout();
        var rng = new System.Random(seed);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map.links.Add(new Vector2Int(x, y), new List<Vector2Int>());

        var visited = new HashSet<Vector2Int> { Vector2Int.zero };
        var stack = new Stack<Vector2Int>();
        stack.Push(Vector2Int.zero);
        var candidates = new List<Vector2Int>(4);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            candidates.Clear();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (map.links.ContainsKey(next) && !visited.Contains(next))
                    candidates.Add(next);
            }
            if (candidates.Count == 0) { stack.Pop(); continue; }
            var chosen = candidates[rng.Next(candidates.Count)];
            map.Connect(current, chosen);
            visited.Add(chosen);
            stack.Push(chosen);
        }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var a = new Vector2Int(x, y);
                TryExtra(a, a + Vector2Int.right);
                TryExtra(a, a + Vector2Int.up);
            }
        return map;

        void TryExtra(Vector2Int a, Vector2Int b)
        {
            if (map.links.ContainsKey(b) && !map.CanMove(a, b) && rng.NextDouble() < Mathf.Clamp01(extraPassageChance))
                map.Connect(a, b);
        }
    }

    public Dictionary<Vector2Int, int> Distances(Vector2Int start, int maxDistance)
    {
        var result = new Dictionary<Vector2Int, int>();
        if (!links.ContainsKey(start) || maxDistance < 0) return result;
        var queue = new Queue<Vector2Int>();
        result[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (result[current] >= maxDistance) continue;
            foreach (var next in links[current])
            {
                if (result.ContainsKey(next)) continue;
                result[next] = result[current] + 1;
                queue.Enqueue(next);
            }
        }
        return result;
    }
}