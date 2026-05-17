using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Map
{
    public partial class UserInputHandler : Node
    {
        [Export] public MapManager MapManager { get; set; } = null!;
        [Export] public MapRenderer MapRenderer { get; set; } = null!;
        [Export] public float InvalidClickFlashDuration = 0.2f;
        [Export] public Color InvalidClickColor = new Color(1, 0.3f, 0.3f, 0.5f);

        private Unit? _selectedUnit;
        private List<Vector2I> _currentMoveRange = new List<Vector2I>();
        private Pathfinder? _pathfinder;
        private bool _isProcessing = false;
        private Vector2I? _lastInvalidClickPos;

        [Signal] public delegate void UnitSelectedEventHandler(Unit unit);
        [Signal] public delegate void UnitMovedEventHandler(Unit unit, Vector2I fromPos, Vector2I toPos);
        [Signal] public delegate void CellClickedEventHandler(Vector2I cell);
        [Signal] public delegate void CancelActionEventHandler();
        [Signal] public delegate void InvalidActionEventHandler(string message);
        [Signal] public delegate void HoveredCellChangedEventHandler(Vector2I cell);

        public override void _Ready()
        {
            if (MapManager == null)
            {
                MapManager = GetNode<MapManager>("/root/Main/MapManager");
            }

            if (MapRenderer == null)
            {
                MapRenderer = GetNode<MapRenderer>("/root/Main/MapManager/MapRenderer");
            }

            _pathfinder = new Pathfinder(MapManager);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseEvent)
            {
                HandleMouseInput(mouseEvent);
            }
            else if (@event is InputEventKey keyEvent)
            {
                HandleKeyboardInput(keyEvent);
            }
            else if (@event is InputEventMouseMotion motionEvent)
            {
                HandleMouseMotion(motionEvent);
            }
        }

        private void HandleMouseInput(InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.Pressed)
            {
                switch (mouseEvent.ButtonIndex)
                {
                    case MouseButton.Left:
                        HandleLeftClick();
                        break;
                    case MouseButton.Right:
                        HandleRightClick();
                        break;
                }
            }
        }

        // 将鼠标视口坐标转换为地图世界坐标（考虑 Camera2D 偏移和缩放）
        private Vector2 GetMapMousePosition()
        {
            Camera2D camera = GetViewport().GetCamera2D();
            if (camera != null)
            {
                return camera.GlobalPosition +
                    (GetViewport().GetMousePosition() - GetViewport().GetVisibleRect().Size * 0.5f) / camera.Zoom;
            }
            return GetViewport().GetMousePosition();
        }

        // 处理左键点击：选择单位 / 移动单位 / 取消选择
        private void HandleLeftClick()
        {
            if (_isProcessing)
                return;

            Vector2I gridPos = MapManager.WorldToGridInt(GetMapMousePosition());

            if (!MapManager.IsValidPosition(gridPos))
            {
                ShowInvalidAction("点击位置超出地图范围");
                return;
            }

            EmitSignal(SignalName.CellClicked, gridPos);

            Unit? clickedUnit = MapManager.GetUnitAt(gridPos);

            if (_selectedUnit != null)
            {
                if (_selectedUnit.IsMoving)
                {
                    ShowInvalidAction("单位正在移动中，请等待");
                    return;
                }

                if (_currentMoveRange.Contains(gridPos))
                {
                    MoveSelectedUnit(gridPos);
                    return;
                }
                else if (clickedUnit != null && clickedUnit != _selectedUnit)
                {
                    SelectUnit(clickedUnit);
                    return;
                }
                else if (!_currentMoveRange.Contains(gridPos))
                {
                    ShowInvalidAction("该位置不在移动范围内");
                    ShowInvalidClickEffect(gridPos);
                    return;
                }
            }

            if (clickedUnit != null)
            {
                SelectUnit(clickedUnit);
            }
            else
            {
                DeselectUnit();
            }
        }

        private void ShowInvalidAction(string message)
        {
            GD.Print($"无效操作: {message}");
            EmitSignal(SignalName.InvalidAction, message);
        }

        private void ShowInvalidClickEffect(Vector2I pos)
        {
            if (_lastInvalidClickPos.HasValue && _lastInvalidClickPos.Value == pos)
                return;

            _lastInvalidClickPos = pos;
            MapRenderer.AddCellOverlay(pos, InvalidClickColor);

            GetTree().CreateTimer(InvalidClickFlashDuration).Timeout += () =>
            {
                MapRenderer.RemoveCellOverlay(pos);
                _lastInvalidClickPos = null;
            };
        }

        // 处理右键：取消选择
        private void HandleRightClick()
        {
            DeselectUnit();
            EmitSignal(SignalName.CancelAction);
        }

        // 处理鼠标移动：更新悬停高亮
        private void HandleMouseMotion(InputEventMouseMotion motionEvent)
        {
            Vector2I gridPos = MapManager.WorldToGridInt(GetMapMousePosition());

            if (MapManager.IsValidPosition(gridPos))
            {
                MapRenderer.SetHoverCell(gridPos);
                EmitSignal(nameof(HoveredCellChanged), gridPos);
            }
            else
            {
                MapRenderer.ClearHighlights();
                EmitSignal(nameof(HoveredCellChanged), new Vector2I(-1, -1));
            }
        }

        // 处理键盘：Esc 取消选择，WASD/方向键移动单位
        private void HandleKeyboardInput(InputEventKey keyEvent)
        {
            if (!keyEvent.Pressed)
                return;

            switch (keyEvent.Keycode)
            {
                case Key.Escape:
                    DeselectUnit();
                    EmitSignal(SignalName.CancelAction);
                    break;
                case Key.W:
                case Key.Up:
                    MoveSelectedUnitDirection(Vector2I.Up);
                    break;
                case Key.S:
                case Key.Down:
                    MoveSelectedUnitDirection(Vector2I.Down);
                    break;
                case Key.A:
                case Key.Left:
                    MoveSelectedUnitDirection(Vector2I.Left);
                    break;
                case Key.D:
                case Key.Right:
                    MoveSelectedUnitDirection(Vector2I.Right);
                    break;
            }
        }

        // 选择单位：显示选中高亮 + 移动范围预览
        private void SelectUnit(Unit unit)
        {
            DeselectUnit();

            _selectedUnit = unit;
            _currentMoveRange = _pathfinder!.CalculateMoveRange(unit);

            MapRenderer.SetSelectedCell(unit.GridPosition);
            MapRenderer.ShowMoveRange(_currentMoveRange);

            EmitSignal(SignalName.UnitSelected, unit);

            GD.Print($"已选择单位: {unit.UnitName}");
        }

        // 取消选择：清空选中高亮和移动范围
        private void DeselectUnit()
        {
            if (_selectedUnit != null)
            {
                _selectedUnit = null;
                _currentMoveRange.Clear();
                
                MapRenderer.SetSelectedCell(null);
                MapRenderer.ClearMoveRange();
            }
        }

        // 移动选中单位到目标位置（使用 BFS 寻路沿网格移动）
        private void MoveSelectedUnit(Vector2I targetPos)
        {
            if (_selectedUnit == null || _selectedUnit.IsMoving)
                return;

            _isProcessing = true;

            Vector2I fromPos = _selectedUnit.GridPosition;

            List<Vector2I> path = _pathfinder!.FindPath(_selectedUnit, targetPos);
            if (path.Count < 2)
            {
                ShowInvalidAction("无法到达该位置");
                _isProcessing = false;
                return;
            }

            _selectedUnit.MoveCompleted += OnUnitMoveCompleted;
            MapManager.MoveUnitAlongPath(_selectedUnit, path);
            EmitSignal(SignalName.UnitMoved, _selectedUnit, fromPos, targetPos);
        }

        private void OnUnitMoveCompleted()
        {
            if (_selectedUnit != null)
            {
                _selectedUnit.MoveCompleted -= OnUnitMoveCompleted;
            }
            DeselectUnit();
            _isProcessing = false;
        }

        private void MoveSelectedUnitDirection(Vector2I direction)
        {
            if (_selectedUnit == null)
                return;

            Vector2I targetPos = _selectedUnit.GridPosition + direction;

            if (_currentMoveRange.Contains(targetPos))
            {
                MoveSelectedUnit(targetPos);
            }
        }

        public Unit? GetSelectedUnit()
        {
            return _selectedUnit;
        }

        public List<Vector2I> GetCurrentMoveRange()
        {
            return new List<Vector2I>(_currentMoveRange);
        }

        public void ClearSelection()
        {
            DeselectUnit();
        }
    }
}