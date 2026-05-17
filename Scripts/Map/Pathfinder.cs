using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FE5.Units;

namespace FE5.Map
{
    // BFS 移动节点
    public class MoveNode
    {
        public Vector2I Position { get; set; }
        public int RemainingMove { get; set; }
        public MoveNode? Parent { get; set; }
        public int TotalCost { get; set; }

        public MoveNode(Vector2I pos, int remainingMove, MoveNode? parent = null)
        {
            Position = pos;
            RemainingMove = remainingMove;
            Parent = parent;
            TotalCost = parent?.TotalCost ?? 0;
        }
    }

    public class Pathfinder
    {
        private MapManager _mapManager;
        private readonly Vector2I[] _directions = new Vector2I[]
        {
            new Vector2I(0, -1),
            new Vector2I(0, 1),
            new Vector2I(-1, 0),
            new Vector2I(1, 0)
        };

        public Pathfinder(MapManager mapManager)
        {
            _mapManager = mapManager;
        }

        // 计算单位的可移动范围（BFS，考虑地形移动力消耗）
        public List<Vector2I> CalculateMoveRange(Unit unit)
        {
            var reachablePositions = new List<Vector2I>();
            var visited = new HashSet<Vector2I>();
            var queue = new Queue<MoveNode>();

            Vector2I startPos = unit.GridPosition;
            int maxMove = unit.Stats.Movement;

            queue.Enqueue(new MoveNode(startPos, maxMove));
            visited.Add(startPos);
            reachablePositions.Add(startPos);

            while (queue.Count > 0)
            {
                MoveNode current = queue.Dequeue();

                foreach (Vector2I dir in _directions)
                {
                    Vector2I nextPos = current.Position + dir;

                    if (visited.Contains(nextPos))
                        continue;

                    if (!_mapManager.IsValidPosition(nextPos))
                        continue;

                    TerrainData terrain = _mapManager.GetTerrain(nextPos);
                    int moveCost = terrain.MovementCost;

                    int remainingMove = current.RemainingMove - moveCost;
                    if (remainingMove < 0)
                        continue;

                    if (!_mapManager.IsPassable(nextPos, unit))
                        continue;

                    visited.Add(nextPos);
                    reachablePositions.Add(nextPos);

                    queue.Enqueue(new MoveNode(nextPos, remainingMove, current));
                }
            }

            return reachablePositions;
        }

        // 寻路：从单位位置到目标格子的最短路径（BFS）
        public List<Vector2I> FindPath(Unit unit, Vector2I targetPos)
        {
            List<Vector2I> moveRange = CalculateMoveRange(unit);
            
            if (!moveRange.Contains(targetPos))
                return new List<Vector2I>();

            var visited = new Dictionary<Vector2I, MoveNode>();
            var queue = new Queue<MoveNode>();

            Vector2I startPos = unit.GridPosition;
            int maxMove = unit.Stats.Movement;

            queue.Enqueue(new MoveNode(startPos, maxMove));
            visited[startPos] = new MoveNode(startPos, maxMove);

            while (queue.Count > 0)
            {
                MoveNode current = queue.Dequeue();

                if (current.Position == targetPos)
                {
                    return ReconstructPath(current);
                }

                foreach (Vector2I dir in _directions)
                {
                    Vector2I nextPos = current.Position + dir;

                    if (visited.ContainsKey(nextPos))
                        continue;

                    if (!_mapManager.IsValidPosition(nextPos))
                        continue;

                    TerrainData terrain = _mapManager.GetTerrain(nextPos);
                    int moveCost = terrain.MovementCost;

                    int remainingMove = current.RemainingMove - moveCost;
                    if (remainingMove < 0)
                        continue;

                    if (!_mapManager.IsPassable(nextPos, unit))
                        continue;

                    MoveNode nextNode = new MoveNode(nextPos, remainingMove, current);
                    visited[nextPos] = nextNode;
                    queue.Enqueue(nextNode);
                }
            }

            return new List<Vector2I>();
        }

        // 从终点节点回溯重建路径
        private List<Vector2I> ReconstructPath(MoveNode endNode)
        {
            List<Vector2I> path = new List<Vector2I>();
            MoveNode? current = endNode;

            while (current != null)
            {
                path.Add(current.Position);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        // 判断某格是否在移动范围内
        public bool IsInMoveRange(Vector2I pos, List<Vector2I> moveRange)
        {
            return moveRange.Contains(pos);
        }

        // 获取指定格子的四方向相邻格子
        public List<Vector2I> GetAdjacentCells(Vector2I pos)
        {
            List<Vector2I> adjacent = new List<Vector2I>();

            foreach (Vector2I dir in _directions)
            {
                Vector2I adjacentPos = pos + dir;
                if (_mapManager.IsValidPosition(adjacentPos))
                {
                    adjacent.Add(adjacentPos);
                }
            }

            return adjacent;
        }

        // 计算两格之间的曼哈顿距离
        public int CalculateDistance(Vector2I a, Vector2I b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        public int CalculateMoveCost(Unit unit, Vector2I fromPos, Vector2I toPos)
        {
            if (!_mapManager.IsValidPosition(toPos))
                return int.MaxValue;

            TerrainData terrain = _mapManager.GetTerrain(toPos);
            return terrain.MovementCost;
        }

        // 查找移动范围内可攻击的敌方单位
        public List<Vector2I> FindReachableEnemies(Unit unit)
        {
            List<Vector2I> enemies = new List<Vector2I>();
            List<Vector2I> moveRange = CalculateMoveRange(unit);

            foreach (var pos in moveRange)
            {
                Unit? occupant = _mapManager.GetUnitAt(pos);
                if (occupant != null && occupant.Faction != unit.Faction)
                {
                    foreach (var adjacent in GetAdjacentCells(pos))
                    {
                        if (moveRange.Contains(adjacent))
                        {
                            enemies.Add(pos);
                            break;
                        }
                    }
                }
            }

            return enemies;
        }

        public void PrintMoveRange(Unit unit, List<Vector2I> moveRange)
        {
            GD.Print($"\n=== {unit.UnitName} 的移动范围 ===");
            GD.Print($"位置: {unit.GridPosition}, 移动力: {unit.Stats.Movement}");
            GD.Print($"可到达位置数: {moveRange.Count}");

            foreach (var pos in moveRange.OrderBy(p => p.Y).ThenBy(p => p.X))
            {
                TerrainData terrain = _mapManager.GetTerrain(pos);
                string marker = pos == unit.GridPosition ? "[起点]" : "";
                GD.Print($"  ({pos.X}, {pos.Y}) - {terrain.Type} {marker}");
            }
        }

        public void PrintPath(List<Vector2I> path)
        {
            if (path.Count == 0)
            {
                GD.Print("路径为空");
                return;
            }

            GD.Print("\n=== 路径 ===");
            for (int i = 0; i < path.Count; i++)
            {
                string marker = i == 0 ? "[起点]" : i == path.Count - 1 ? "[终点]" : "";
                GD.Print($"  {i + 1}. ({path[i].X}, {path[i].Y}) {marker}");
            }
        }
    }
}