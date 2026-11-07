using System;
using System.Collections.Generic;
using Godot;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Fast 4-Directional A* Pathfinding Engine for Isometric diamond grids in Godot 2D.
    /// Calculates the optimal movement route between grid nodes while checking map boundaries.
    /// </summary>
    public static class TilePathfinder
    {
        private static readonly Vector2I[] s_Directions = new Vector2I[]
        {
            new Vector2I(1, 0),
            new Vector2I(-1, 0),
            new Vector2I(0, 1),
            new Vector2I(0, -1)
        };

        private static readonly Dictionary<Vector2I, Vector2I> s_CameFrom = new(512);
        private static readonly Dictionary<Vector2I, int> s_GScore = new(512);
        private static readonly Dictionary<Vector2I, int> s_FScore = new(512);
        private static readonly List<Vector2I> s_OpenList = new(512);
        private static readonly HashSet<Vector2I> s_OpenSet = new();
        private static readonly HashSet<Vector2I> s_ClosedSet = new();

        private static int Heuristic(Vector2I a, Vector2I b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        public static List<Vector2I>? FindPath(Vector2I start, Vector2I target, IsometricTileManager tileManager, int maxSearchNodes = 500)
        {
            if (tileManager == null || start == target) return null;
            if (!tileManager.IsWithinBounds(target) || !tileManager.IsTileWalkable(target)) return null;

            s_CameFrom.Clear();
            s_GScore.Clear();
            s_FScore.Clear();
            s_OpenList.Clear();
            s_OpenSet.Clear();
            s_ClosedSet.Clear();

            s_GScore[start] = 0;
            s_FScore[start] = Heuristic(start, target);
            s_OpenList.Add(start);
            s_OpenSet.Add(start);

            while (s_OpenList.Count > 0)
            {
                int bestIndex = 0;
                int lowestF = s_FScore[s_OpenList[0]];
                for (int i = 1; i < s_OpenList.Count; i++)
                {
                    int score = s_FScore[s_OpenList[i]];
                    if (score < lowestF)
                    {
                        lowestF = score;
                        bestIndex = i;
                    }
                }

                Vector2I current = s_OpenList[bestIndex];
                if (current == target)
                {
                    return RetracePath(start, target, s_CameFrom);
                }

                int lastIdx = s_OpenList.Count - 1;
                s_OpenList[bestIndex] = s_OpenList[lastIdx];
                s_OpenList.RemoveAt(lastIdx);
                s_OpenSet.Remove(current);
                s_ClosedSet.Add(current);

                if (s_ClosedSet.Count >= maxSearchNodes) break;

                for (int i = 0; i < s_Directions.Length; i++)
                {
                    Vector2I neighbor = current + s_Directions[i];
                    if (!tileManager.IsWithinBounds(neighbor) || !tileManager.IsTileWalkable(neighbor)) continue;
                    if (s_ClosedSet.Contains(neighbor)) continue;

                    int tentativeG = s_GScore[current] + 1;
                    if (!s_GScore.TryGetValue(neighbor, out int currentG) || tentativeG < currentG)
                    {
                        s_CameFrom[neighbor] = current;
                        s_GScore[neighbor] = tentativeG;
                        s_FScore[neighbor] = tentativeG + Heuristic(neighbor, target);

                        if (s_OpenSet.Add(neighbor))
                        {
                            s_OpenList.Add(neighbor);
                        }
                    }
                }
            }

            return null;
        }

        private static List<Vector2I> RetracePath(Vector2I start, Vector2I target, Dictionary<Vector2I, Vector2I> cameFrom)
        {
            var path = new List<Vector2I>();
            Vector2I curr = target;
            while (curr != start)
            {
                path.Add(curr);
                if (!cameFrom.TryGetValue(curr, out curr)) break;
            }
            path.Reverse();
            return path;
        }
    }
}
