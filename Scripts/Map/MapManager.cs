using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FE5.Units;

namespace FE5.Map
{
    public enum TerrainType
    {
        Plain,
        Forest,
        Mountain,
        River,
        Sea,
        Wall,
        Door,
        Throne,
        Fortress,
        Peak
    }

    public struct TerrainData
    {
        public TerrainType Type;
        public int DefenseBonus;
        public int AvoidBonus;
        public int MovementCost;
        public bool IsPassable;
        public Color Color;

        public static TerrainData GetDefault(TerrainType type)
        {
            return type switch
            {
                TerrainType.Plain => new TerrainData { 
                    Type = type, DefenseBonus = 0, AvoidBonus = 0, 
                    MovementCost = 1, IsPassable = true, Color = new Color(0.6f, 0.8f, 0.4f) },
                TerrainType.Forest => new TerrainData { 
                    Type = type, DefenseBonus = 1, AvoidBonus = 20, 
                    MovementCost = 2, IsPassable = true, Color = new Color(0.2f, 0.6f, 0.2f) },
                TerrainType.Mountain => new TerrainData { 
                    Type = type, DefenseBonus = 1, AvoidBonus = 30, 
                    MovementCost = 4, IsPassable = true, Color = new Color(0.6f, 0.5f, 0.4f) },
                TerrainType.River => new TerrainData { 
                    Type = type, DefenseBonus = 0, AvoidBonus = 0, 
                    MovementCost = 5, IsPassable = true, Color = new Color(0.3f, 0.5f, 0.9f) },
                TerrainType.Sea => new TerrainData { 
                    Type = type, DefenseBonus = 0, AvoidBonus = 10, 
                    MovementCost = 2, IsPassable = false, Color = new Color(0.2f, 0.4f, 0.8f) },
                TerrainType.Wall => new TerrainData { 
                    Type = type, DefenseBonus = 0, AvoidBonus = 0, 
                    MovementCost = 999, IsPassable = false, Color = new Color(0.4f, 0.4f, 0.4f) },
                TerrainType.Throne => new TerrainData { 
                    Type = type, DefenseBonus = 3, AvoidBonus = 30, 
                    MovementCost = 1, IsPassable = true, Color = new Color(0.9f, 0.7f, 0.2f) },
                TerrainType.Fortress => new TerrainData { 
                    Type = type, DefenseBonus = 2, AvoidBonus = 20, 
                    MovementCost = 1, IsPassable = true, Color = new Color(0.5f, 0.5f, 0.7f) },
                TerrainType.Peak => new TerrainData { 
                    Type = type, DefenseBonus = 2, AvoidBonus = 40, 
                    MovementCost = 6, IsPassable = true, Color = new Color(0.7f, 0.7f, 0.7f) },
                _ => new TerrainData { 
                    Type = type, DefenseBonus = 0, AvoidBonus = 0, 
                    MovementCost = 1, IsPassable = true, Color = new Color(0.5f, 0.5f, 0.5f) }
            };
        }
    }

    public struct GridCell
    {
        public Vector2I Coordinates;
        public TerrainData Terrain;
        public Unit? Occupant;
        public bool IsHighlighted;
        public bool IsInMoveRange;
        public bool IsSelected;
    }

    public partial class MapManager : Node2D
    {
        [Export] public int MapWidth { get; set; } = 20;
        [Export] public int MapHeight { get; set; } = 15;
        [Export] public int CellSize { get; set; } = 32;

        private TileMapLayer? _groundLayer;
        private TileMapLayer? _terrainLayer;
        private Dictionary<Vector2I, TerrainData> _terrainData = new Dictionary<Vector2I, TerrainData>();
        private Dictionary<Vector2I, Unit> _unitsOnMap = new Dictionary<Vector2I, Unit>();

        [Signal] public delegate void MapGeneratedEventHandler();
        [Signal] public delegate void TerrainChangedEventHandler(Vector2I position, TerrainType newTerrain);
        [Signal] public delegate void UnitPlacedEventHandler(Unit unit, Vector2I position);
        [Signal] public delegate void UnitMovedEventHandler(Unit unit, Vector2I fromPosition, Vector2I toPosition);

        public override void _Ready()
        {
            InitializeLayers();
        }

        private void InitializeLayers()
        {
            _groundLayer = GetNodeOrNull<TileMapLayer>("GroundLayer");
            _terrainLayer = GetNodeOrNull<TileMapLayer>("TerrainLayer");
        }

        // 生成全平地的空地图
        public void GenerateEmptyMap()
        {
            _terrainData.Clear();
            _unitsOnMap.Clear();

            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    _terrainData[pos] = TerrainData.GetDefault(TerrainType.Plain);
                }
            }

            EmitSignal(SignalName.MapGenerated);
            GD.Print($"空地图生成完成: {MapWidth}x{MapHeight}");
        }

        // 生成随机地形地图
        public void GenerateRandomMap(float forestDensity = 0.2f, float mountainDensity = 0.1f, float riverDensity = 0.05f)
        {
            _terrainData.Clear();

            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    TerrainType terrain = TerrainType.Plain;
                    Random rand = new Random(x + y * MapWidth);

                    float roll = (float)rand.NextDouble();
                    
                    if (roll < mountainDensity)
                    {
                        terrain = TerrainType.Mountain;
                    }
                    else if (roll < mountainDensity + forestDensity)
                    {
                        terrain = TerrainType.Forest;
                    }
                    else if (roll < mountainDensity + forestDensity + riverDensity)
                    {
                        terrain = TerrainType.River;
                    }

                    _terrainData[pos] = TerrainData.GetDefault(terrain);
                }
            }

            EmitSignal(SignalName.MapGenerated);
            GD.Print($"随机地图生成完成: {MapWidth}x{MapHeight}");
        }

        public void GenerateMapFromData(List<Tuple<int, int, TerrainType>> terrainData)
        {
            GenerateEmptyMap();
            
            foreach (var data in terrainData)
            {
                Vector2I pos = new Vector2I(data.Item1, data.Item2);
                if (IsValidPosition(pos))
                {
                    SetTerrain(pos, data.Item3);
                }
            }

            GD.Print($"自定义地图生成完成: {terrainData.Count} 个地形点");
        }

        // 设置指定格子的地形
        public void SetTerrain(Vector2I pos, TerrainType type)
        {
            if (IsValidPosition(pos))
            {
                _terrainData[pos] = TerrainData.GetDefault(type);
                EmitSignal(SignalName.TerrainChanged, pos, (int)type);
            }
        }

        // 获取指定格子的地形数据
        public TerrainData GetTerrain(Vector2I pos)
        {
            if (_terrainData.TryGetValue(pos, out TerrainData data))
            {
                return data;
            }
            return TerrainData.GetDefault(TerrainType.Plain);
        }

        public GridCell GetGridCell(Vector2I pos)
        {
            return new GridCell
            {
                Coordinates = pos,
                Terrain = GetTerrain(pos),
                Occupant = GetUnitAt(pos),
                IsHighlighted = false,
                IsInMoveRange = false,
                IsSelected = false
            };
        }

        // 判断坐标是否在地图范围内
        public bool IsValidPosition(Vector2I pos)
        {
            return pos.X >= 0 && pos.X < MapWidth && 
                   pos.Y >= 0 && pos.Y < MapHeight;
        }

        // 判断格子是否可通行（地形 + 单位占用），unit 为要移动的单位自身
        public bool IsPassable(Vector2I pos, Unit? unit = null)
        {
            if (!IsValidPosition(pos))
                return false;

            TerrainData terrain = GetTerrain(pos);
            if (!terrain.IsPassable)
                return false;

            if (_unitsOnMap.TryGetValue(pos, out Unit? existingUnit))
            {
                return existingUnit == unit;
            }

            return true;
        }

        // 将单位放置到地图指定格子
        public void PlaceUnit(Unit unit, Vector2I pos)
        {
            if (IsValidPosition(pos) && IsPassable(pos))
            {
                _unitsOnMap[pos] = unit;
                unit.GridPosition = pos;
                unit.Position = new Vector2(pos.X * CellSize + CellSize / 2, pos.Y * CellSize + CellSize / 2);
                EmitSignal(SignalName.UnitPlaced, unit, pos);
                GD.Print($"单位 {unit.UnitName} 放置到 ({pos.X}, {pos.Y})");
            }
        }

        // 移动单位（直接移动或逐格动画），不经过寻路
        public bool MoveUnit(Unit unit, Vector2I fromPos, Vector2I toPos, bool useAnimation = true)
        {
            if (!IsValidPosition(toPos) || !IsPassable(toPos, unit))
                return false;

            if (_unitsOnMap.ContainsKey(fromPos) && _unitsOnMap[fromPos] == unit)
            {
                _unitsOnMap.Remove(fromPos);
            }
            
            _unitsOnMap[toPos] = unit;
            
            if (useAnimation)
            {
                unit.MoveToWithAnimation(toPos, CellSize);
            }
            else
            {
                unit.MoveTo(toPos);
            }
            
            EmitSignal(SignalName.UnitMoved, unit, fromPos, toPos);
            GD.Print($"单位 {unit.UnitName} 从 ({fromPos.X}, {fromPos.Y}) 移动到 ({toPos.X}, {toPos.Y})");
            return true;
        }

        // 沿 BFS 路径移动单位（逐格平滑动画）
        public bool MoveUnitAlongPath(Unit unit, List<Vector2I> path)
        {
            if (path.Count < 2) return false;

            Vector2I fromPos = path[0];
            Vector2I toPos = path[^1];

            if (_unitsOnMap.TryGetValue(fromPos, out var u) && u == unit)
                _unitsOnMap.Remove(fromPos);

            _unitsOnMap[toPos] = unit;
            unit.MoveAlongPath(path, CellSize);

            EmitSignal(SignalName.UnitMoved, unit, fromPos, toPos);
            return true;
        }

        public void RemoveUnit(Vector2I pos)
        {
            _unitsOnMap.Remove(pos);
        }

        // 获取指定格子上的单位
        public Unit? GetUnitAt(Vector2I pos)
        {
            _unitsOnMap.TryGetValue(pos, out Unit? unit);
            return unit;
        }

        // 将世界坐标转换为网格坐标（浮点）
        public Vector2 WorldToGrid(Vector2 worldPos)
        {
            return new Vector2(
                Mathf.Floor(worldPos.X / CellSize),
                Mathf.Floor(worldPos.Y / CellSize)
            );
        }

        // 将世界坐标转换为网格坐标（整数）
        public Vector2I WorldToGridInt(Vector2 worldPos)
        {
            return new Vector2I(
                Mathf.FloorToInt(worldPos.X / CellSize),
                Mathf.FloorToInt(worldPos.Y / CellSize)
            );
        }

        // 将网格坐标转换为世界坐标（格子中心）
        public Vector2 GridToWorld(Vector2I gridPos)
        {
            return new Vector2(
                gridPos.X * CellSize + CellSize / 2,
                gridPos.Y * CellSize + CellSize / 2
            );
        }

        public List<Vector2I> GetAllUnitsPositions()
        {
            return _unitsOnMap.Keys.ToList();
        }

        public int GetTotalCells()
        {
            return MapWidth * MapHeight;
        }

        public Rect2 GetMapBounds()
        {
            return new Rect2(0, 0, MapWidth * CellSize, MapHeight * CellSize);
        }
    }
}