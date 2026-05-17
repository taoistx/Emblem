using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;
using FE5.Map;
using FE5.Combat;
using FE5.Core;

namespace FE5.Test
{
    public partial class TestRunner : Node
    {
        private MapManager? _mapManager;
        private MapRenderer? _mapRenderer;
        private UserInputHandler? _inputHandler;
        private Pathfinder? _pathfinder;
        private Label? _selectedUnitInfo;
        private Label? _turnInfo;
        private Label? _debugInfo;
        private Label? _phaseSubtitle; // 阶段字幕标签
        private TurnManager? _turnManager; // 回合管理器
        private Button? _endTurnButton; // 结束回合按钮
        private int _testMoveRangeIndex = 0;
        private List<UnitStats> _testMoveRangeStats = new List<UnitStats>()
        {
            new UnitStats { HP = 20, MaxHP = 20, Movement = 3 },
            new UnitStats { HP = 20, MaxHP = 20, Movement = 5 },
            new UnitStats { HP = 20, MaxHP = 20, Movement = 7 },
            new UnitStats { HP = 20, MaxHP = 20, Movement = 9 }
        };

        public override void _Ready()
        {
            GD.Print("\n========================================");
            GD.Print("    FE5 网格地图系统测试开始");
            GD.Print("========================================\n");

            _mapManager = GetNode<MapManager>("/root/Main/MapManager");
            _mapRenderer = GetNode<MapRenderer>("/root/Main/MapManager/MapRenderer");
            _inputHandler = GetNode<UserInputHandler>("/root/Main/UserInputHandler");
            _selectedUnitInfo = GetNode<Label>("/root/Main/UI/HUD/SelectedUnitInfo");
            _turnInfo = GetNode<Label>("/root/Main/UI/HUD/TurnInfo");
            _debugInfo = GetNode<Label>("/root/Main/UI/HUD/DebugInfo");

            if (_mapManager != null)
            {
                _pathfinder = new Pathfinder(_mapManager);
            }

            InitializeGame();

            if (_inputHandler != null)
            {
                _inputHandler.InvalidAction += OnInvalidAction;
            }

            RunMoveRangeTests();

            // 初始化回合管理器并连接信号
            _turnManager = GetNode<TurnManager>("/root/Main/TurnManager");
            _endTurnButton = GetNode<Button>("/root/Main/UI/HUD/EndTurnButton");
            _phaseSubtitle = GetNode<Label>("/root/Main/UI/PhaseSubtitle");

            _turnManager.PhaseAnnounced += OnPhaseAnnounced;
            _turnManager.PhaseStarted += OnPhaseStarted;
            _turnManager.PhaseEnded += OnPhaseEnded;

            _endTurnButton.Pressed += OnEndTurnPressed;

            // 从玩家回合开始游戏
            _turnManager.StartGame();

            GD.Print("\n========================================");
            GD.Print("    游戏初始化完成!");
            GD.Print("========================================\n");
        }

        private void RunMoveRangeTests()
        {
            GD.Print("\n=== 移动范围测试 ===");
            
            foreach (var stats in _testMoveRangeStats)
            {
                Vector2I testPos = new Vector2I(10, 7);
                Unit testUnit = new Unit();
                testUnit.GridPosition = testPos;
                testUnit.Initialize("测试单位", UnitFaction.Player, stats);
                
                List<Vector2I> moveRange = _pathfinder?.CalculateMoveRange(testUnit) ?? new List<Vector2I>();
                GD.Print($"移动力 {stats.Movement}: 可到达 {moveRange.Count} 个格子");
                VerifyMoveRange(testUnit, moveRange);
            }
        }

        private void VerifyMoveRange(Unit unit, List<Vector2I> moveRange)
        {
            foreach (var pos in moveRange)
            {
                int distance = Math.Abs(pos.X - unit.GridPosition.X) + Math.Abs(pos.Y - unit.GridPosition.Y);
                if (distance > unit.Stats.Movement)
                {
                    GD.PrintErr($"错误: 位置 ({pos.X},{pos.Y}) 距离起点 {distance}，超过移动力 {unit.Stats.Movement}");
                }
            }
            GD.Print($"验证通过: 所有位置均在移动范围内");
        }

        private void InitializeGame()
        {
            if (_mapManager == null)
            {
                GD.Print("MapManager 未找到，跳过初始化");
                return;
            }

            _mapManager.GenerateEmptyMap();

            var playerUnits = GetTree().GetNodesInGroup("Units");
            foreach (var node in playerUnits)
            {
                if (node is Unit unit)
                {
                    UnitStats stats = GetUnitStatsByName(unit.UnitName);
                    unit.Initialize(unit.UnitName, unit.Faction, stats);
                    
                    Vector2I gridPos = _mapManager.WorldToGridInt(unit.Position);
                    _mapManager.PlaceUnit(unit, gridPos);
                    
                    GD.Print($"已初始化单位: {unit.UnitName} 到 ({gridPos.X}, {gridPos.Y})");
                }
            }

            if (_inputHandler != null)
            {
                _inputHandler.UnitSelected += OnUnitSelected;
                _inputHandler.UnitMoved += OnUnitMoved;
            }
        }

        private UnitStats GetUnitStatsByName(string unitName)
        {
            switch (unitName)
            {
                case "骑士":
                    return new UnitStats
                    {
                        HP = 35,
                        MaxHP = 35,
                        Strength = 15,
                        Magic = 3,
                        Skill = 12,
                        Speed = 10,
                        Luck = 8,
                        Defense = 10,
                        Build = 12,
                        Movement = 7,
                        WeaponWeight = 12,
                        WeaponMight = 12,
                        WeaponHit = 80,
                        WeaponCrit = 5
                    };
                case "弓箭手":
                    return new UnitStats
                    {
                        HP = 22,
                        MaxHP = 22,
                        Strength = 8,
                        Magic = 2,
                        Skill = 15,
                        Speed = 14,
                        Luck = 6,
                        Defense = 5,
                        Build = 6,
                        Movement = 4,
                        WeaponWeight = 3,
                        WeaponMight = 10,
                        WeaponHit = 95,
                        WeaponCrit = 15
                    };
                case "修女":
                    return new UnitStats
                    {
                        HP = 18, MaxHP = 18,
                        Strength = 3, Magic = 12,
                        Skill = 8, Speed = 10,
                        Luck = 12, Defense = 3,
                        Build = 5, Movement = 5,
                        WeaponWeight = 2, WeaponMight = 8,
                        WeaponHit = 85, WeaponCrit = 0,
                        IsMagicWeapon = true
                    };
                case "盗贼":
                    return new UnitStats
                    {
                        HP = 24, MaxHP = 24,
                        Strength = 10, Magic = 1,
                        Skill = 12, Speed = 16,
                        Luck = 5, Defense = 4,
                        Build = 7, Movement = 6,
                        WeaponWeight = 3, WeaponMight = 7,
                        WeaponHit = 90, WeaponCrit = 10
                    };
                default:
                    return new UnitStats
                    {
                        HP = 20,
                        MaxHP = 20,
                        Strength = 10,
                        Magic = 5,
                        Skill = 10,
                        Speed = 10,
                        Luck = 5,
                        Defense = 5,
                        Build = 8,
                        Movement = 5
                    };
            }
        }

        private void OnUnitSelected(Unit unit)
        {
            if (_selectedUnitInfo != null)
            {
                _selectedUnitInfo.Text = $"{unit.UnitName} - HP:{unit.Stats.HP}/{unit.Stats.MaxHP} " +
                    $"ATK:{unit.Stats.CalculateAttackPower()} DEF:{unit.Stats.Defense} " +
                    $"MV:{unit.Stats.Movement}";
            }
            GD.Print($"已选择单位: {unit.UnitName}");
        }

        private void OnUnitMoved(Unit unit, Vector2I fromPos, Vector2I toPos)
        {
            GD.Print($"单位 {unit.UnitName} 从 ({fromPos.X}, {fromPos.Y}) 移动到 ({toPos.X}, {toPos.Y})");
        }

        private void OnInvalidAction(string message)
        {
            if (_debugInfo != null)
            {
                _debugInfo.Text = $"无效操作: {message}";
                GetTree().CreateTimer(1.5f).Timeout += () =>
                {
                    if (_debugInfo != null)
                        _debugInfo.Text = "";
                };
            }
        }

        // 阶段字幕显示：显示大字、禁用操作
        private void OnPhaseAnnounced(string subtitle, float duration)
        {
            if (_phaseSubtitle != null)
            {
                _phaseSubtitle.Text = subtitle;
                _phaseSubtitle.Visible = true;
            }

            if (_endTurnButton != null)
                _endTurnButton.Disabled = true;

            if (_inputHandler != null)
                _inputHandler.ProcessMode = ProcessModeEnum.Disabled; // 字幕期间禁止操作
        }

        // 阶段正式激活：隐藏字幕、根据阵营控制输入权限
        private void OnPhaseStarted(TurnPhase phase, int turnNumber)
        {
            if (_phaseSubtitle != null)
                _phaseSubtitle.Visible = false;

            if (_turnInfo != null)
                _turnInfo.Text = $"第 {turnNumber} 回合 - {GetPhaseDisplayName(phase)}";

            bool isPlayerPhase = (phase == TurnPhase.PlayerPhase);

            if (_endTurnButton != null)
                _endTurnButton.Disabled = !isPlayerPhase;

            if (_inputHandler != null)
                _inputHandler.ProcessMode = isPlayerPhase ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;

            if (!isPlayerPhase)
                _inputHandler?.ClearSelection(); // 切换到AI阶段时清除选中

            GD.Print($"阶段开始: {GetPhaseDisplayName(phase)} (第 {turnNumber} 回合)");
        }

        private void OnPhaseEnded(TurnPhase phase, int turnNumber)
        {
            GD.Print($"阶段结束: {GetPhaseDisplayName(phase)} (第 {turnNumber} 回合)");
        }

        // 点击"结束回合"按钮：结束当前玩家阶段
        private void OnEndTurnPressed()
        {
            if (_turnManager != null && _turnManager.CurrentPhase == TurnPhase.PlayerPhase)
            {
                GD.Print("玩家点击结束回合");
                _turnManager.EndCurrentPhase();
            }
        }

        private string GetPhaseDisplayName(TurnPhase phase)
        {
            return phase switch
            {
                TurnPhase.PlayerPhase => "玩家回合",
                TurnPhase.EnemyPhase => "敌军回合",
                TurnPhase.AllyPhase => "友军回合",
                TurnPhase.NeutralPhase => "中立回合",
                _ => "未知"
            };
        }

        public override void _Process(double delta)
        {
            if (Input.IsActionJustPressed("ui_accept"))
            {
                GD.Print("\n=== 地图状态 ===");
                if (_mapManager != null)
                {
                    GD.Print($"地图: {_mapManager.MapWidth}x{_mapManager.MapHeight}");
                    GD.Print($"单位数量: {_mapManager.GetAllUnitsPositions().Count}");
                }
            }

            if (Input.IsActionJustPressed("ui_select"))
            {
                TestBoundaryMovement();
            }

            if (Input.IsActionJustPressed("ui_cancel"))
            {
                TestContinuousMovement();
            }
        }

        private void TestBoundaryMovement()
        {
            GD.Print("\n=== 边界移动测试 ===");
            
            if (_mapManager == null || _pathfinder == null)
                return;

            Unit testUnit = new Unit();
            UnitStats stats = new UnitStats { HP = 20, MaxHP = 20, Movement = 5 };
            testUnit.Initialize("边界测试", UnitFaction.Player, stats);

            Vector2I[] boundaryPositions = {
                new Vector2I(0, 0),
                new Vector2I(_mapManager.MapWidth - 1, 0),
                new Vector2I(0, _mapManager.MapHeight - 1),
                new Vector2I(_mapManager.MapWidth - 1, _mapManager.MapHeight - 1)
            };

            foreach (var pos in boundaryPositions)
            {
                testUnit.GridPosition = pos;
                List<Vector2I> moveRange = _pathfinder.CalculateMoveRange(testUnit);
                
                bool allValid = true;
                foreach (var targetPos in moveRange)
                {
                    if (!_mapManager.IsValidPosition(targetPos))
                    {
                        allValid = false;
                        GD.PrintErr($"边界测试失败: 位置 ({targetPos.X},{targetPos.Y}) 无效");
                        break;
                    }
                }
                
                GD.Print($"边界位置 ({pos.X},{pos.Y}): {moveRange.Count} 个可移动位置, 全部有效: {allValid}");
            }
        }

        private void TestContinuousMovement()
        {
            GD.Print("\n=== 连续移动测试 ===");
            
            if (_mapManager == null)
                return;

            var units = GetTree().GetNodesInGroup("Units");
            foreach (var node in units)
            {
                if (node is Unit unit && unit.State == UnitState.Idle)
                {
                    Vector2I originalPos = unit.GridPosition;
                    
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2I newPos = new Vector2I(
                            Math.Min(_mapManager.MapWidth - 1, originalPos.X + i + 1),
                            originalPos.Y
                        );
                        
                        if (_mapManager.IsPassable(newPos, unit))
                        {
                            _mapManager.MoveUnit(unit, unit.GridPosition, newPos, false);
                            GD.Print($"连续移动 {unit.UnitName}: ({unit.GridPosition.X},{unit.GridPosition.Y})");
                        }
                    }
                    
                    GD.Print($"{unit.UnitName} 连续移动测试完成");
                }
            }
        }
    }
}