using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Map
{
    public partial class MapRenderer : Node2D
    {
        [Export] public MapManager MapManager { get; set; } = null!;
        [Export] public Color HighlightColor = new Color(0.9f, 0.8f, 0.2f, 0.5f);
        [Export] public Color MoveRangeColor = new Color(0.4f, 0.7f, 1.0f, 0.4f);
        [Export] public Color SelectionColor = new Color(0.3f, 0.5f, 0.9f, 0.6f);
        [Export] public Color GridLineColor = new Color(0.25f, 0.2f, 0.15f, 0.8f);
        [Export] public float GridLineWidth = 1.0f;
        [Export] public Color CoordinateLabelColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        [Export] public float CoordinateLabelSize = 10.0f;
        [Export] public bool ShowCoordinates = false;
        [Export] public Color TerrainOverlayColor = new Color(0, 0, 0, 0.05f);
        [Export] public Color SelectedUnitGlowColor = new Color(0.8f, 0.8f, 1, 0.6f);

        private Vector2I? _selectedCell;
        private Vector2I? _hoverCell;
        private List<Vector2I> _moveRange = new List<Vector2I>();
        private Dictionary<Vector2I, Color> _cellOverlays = new Dictionary<Vector2I, Color>();

        [Signal] public delegate void MapRenderedEventHandler();

        public override void _Ready()
        {
            if (MapManager == null)
            {
                MapManager = GetParent<MapManager>();
            }
            
            MapManager.MapGenerated += OnMapGenerated;
            MapManager.TerrainChanged += OnTerrainChanged;
        }

        public override void _Draw()
        {
            DrawMap();
            DrawGridLines();
            DrawCoordinateLabels();
            DrawMoveRange();
            DrawSelection();
            DrawHover();
            DrawCellOverlays();
        }

        // 绘制地图格子（遍历每个格子，用地形颜色填充）
        private void DrawMap()
        {
            for (int x = 0; x < MapManager.MapWidth; x++)
            {
                for (int y = 0; y < MapManager.MapHeight; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    TerrainData terrain = MapManager.GetTerrain(pos);
                    
                    Rect2 rect = new Rect2(
                        x * MapManager.CellSize,
                        y * MapManager.CellSize,
                        MapManager.CellSize,
                        MapManager.CellSize
                    );
                    
                    DrawRect(rect, terrain.Color);
                    
                    if (TerrainOverlayColor.A > 0)
                    {
                        DrawRect(rect, TerrainOverlayColor);
                    }
                }
            }
        }

        // 绘制格子坐标编号（如 A1, B2），默认关闭
        private void DrawCoordinateLabels()
        {
            if (!ShowCoordinates)
                return;

            for (int x = 0; x < MapManager.MapWidth; x++)
            {
                for (int y = 0; y < MapManager.MapHeight; y++)
                {
                    string label = $"{(char)('A' + x)}{y + 1}";
                    Vector2 labelPos = new Vector2(
                        x * MapManager.CellSize + 2,
                        y * MapManager.CellSize + 2
                    );
                    
                    DrawString(ThemeDB.FallbackFont, labelPos, label);
                }
            }
        }

        // 绘制网格线（垂直和水平）
        private void DrawGridLines()
        {
            for (int x = 0; x <= MapManager.MapWidth; x++)
            {
                Vector2 start = new Vector2(x * MapManager.CellSize, 0);
                Vector2 end = new Vector2(x * MapManager.CellSize, MapManager.MapHeight * MapManager.CellSize);
                DrawLine(start, end, GridLineColor, GridLineWidth);
            }

            for (int y = 0; y <= MapManager.MapHeight; y++)
            {
                Vector2 start = new Vector2(0, y * MapManager.CellSize);
                Vector2 end = new Vector2(MapManager.MapWidth * MapManager.CellSize, y * MapManager.CellSize);
                DrawLine(start, end, GridLineColor, GridLineWidth);
            }
        }

        // 绘制单元格叠加层（如无效点击闪烁效果）
        private void DrawCellOverlays()
        {
            foreach (var pair in _cellOverlays)
            {
                Vector2I pos = pair.Key;
                Color color = pair.Value;
                
                Rect2 rect = new Rect2(
                    pos.X * MapManager.CellSize + 1,
                    pos.Y * MapManager.CellSize + 1,
                    MapManager.CellSize - 2,
                    MapManager.CellSize - 2
                );
                
                DrawRect(rect, color);
            }
        }

        public void AddCellOverlay(Vector2I pos, Color color)
        {
            _cellOverlays[pos] = color;
            QueueRedraw();
        }

        public void RemoveCellOverlay(Vector2I pos)
        {
            _cellOverlays.Remove(pos);
            QueueRedraw();
        }

        public void ClearCellOverlays()
        {
            _cellOverlays.Clear();
            QueueRedraw();
        }

        // 绘制移动范围预览（浅蓝色格子）
        private void DrawMoveRange()
        {
            foreach (var pos in _moveRange)
            {
                Rect2 rect = new Rect2(
                    pos.X * MapManager.CellSize + 1,
                    pos.Y * MapManager.CellSize + 1,
                    MapManager.CellSize - 2,
                    MapManager.CellSize - 2
                );
                DrawRect(rect, MoveRangeColor);
            }
        }

        // 绘制选中高亮（蓝色边框）
        private void DrawSelection()
        {
            if (_selectedCell.HasValue)
            {
                Rect2 rect = new Rect2(
                    _selectedCell.Value.X * MapManager.CellSize,
                    _selectedCell.Value.Y * MapManager.CellSize,
                    MapManager.CellSize,
                    MapManager.CellSize
                );
                DrawRect(rect, SelectionColor);
            }
        }

        // 绘制鼠标悬停高亮
        private void DrawHover()
        {
            if (_hoverCell.HasValue && _hoverCell != _selectedCell)
            {
                Rect2 rect = new Rect2(
                    _hoverCell.Value.X * MapManager.CellSize + 2,
                    _hoverCell.Value.Y * MapManager.CellSize + 2,
                    MapManager.CellSize - 4,
                    MapManager.CellSize - 4
                );
                DrawRect(rect, HighlightColor);
            }
        }

        // 设置选中格子
        public void SetSelectedCell(Vector2I? cell)
        {
            _selectedCell = cell;
            QueueRedraw();
        }

        // 显示移动范围
        public void ShowMoveRange(List<Vector2I> positions)
        {
            _moveRange = new List<Vector2I>(positions);
            QueueRedraw();
        }

        // 清空移动范围
        public void ClearMoveRange()
        {
            _moveRange.Clear();
            QueueRedraw();
        }

        // 设置悬停格子
        public void SetHoverCell(Vector2I? cell)
        {
            _hoverCell = cell;
            QueueRedraw();
        }

        // 清空悬停高亮
        public void ClearHighlights()
        {
            _hoverCell = null;
            QueueRedraw();
        }

        // 缩放地图适配视口
        public void ZoomToFit(Viewport viewport)
        {
            Rect2 mapBounds = MapManager.GetMapBounds();
            Vector2 viewSize = viewport.GetVisibleRect().Size;
            
            float scaleX = viewSize.X / mapBounds.Size.X;
            float scaleY = viewSize.Y / mapBounds.Size.Y;
            float scale = Mathf.Min(scaleX, scaleY) * 0.9f;
            
            GlobalScale = new Vector2(scale, scale);
            GlobalPosition = new Vector2(
                (viewSize.X - mapBounds.Size.X * scale) / 2,
                (viewSize.Y - mapBounds.Size.Y * scale) / 2
            );
        }

        public void Refresh()
        {
            QueueRedraw();
        }

        private void OnMapGenerated()
        {
            QueueRedraw();
        }

        private void OnTerrainChanged(Vector2I position, TerrainType newTerrain)
        {
            QueueRedraw();
        }
    }
}