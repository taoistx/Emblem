using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FE5.Units;

namespace FE5.Core
{
    public partial class TurnManager : Node
    {
        [Export] public int CurrentTurnNumber { get; private set; } = 1;
        [Export] public TurnPhase CurrentPhase { get; private set; } = TurnPhase.PlayerPhase;
        [Export] public PhaseState CurrentState { get; private set; } = PhaseState.Starting;

        [Signal] public delegate void PhaseStartedEventHandler(TurnPhase phase, int turnNumber);
        [Signal] public delegate void PhaseEndedEventHandler(TurnPhase phase, int turnNumber);
        [Signal] public delegate void TurnAdvancedEventHandler(int newTurnNumber);
        [Signal] public delegate void AllUnitsActedEventHandler(TurnPhase phase);
        [Signal] public delegate void PhaseAnnouncedEventHandler(string subtitle, float duration); // 阶段字幕通知

        private bool _isAnnouncing = false; // 正在显示阶段字幕中
        private List<Unit> _allUnits = new List<Unit>();
        private List<Unit> _playerUnits = new List<Unit>();
        private List<Unit> _enemyUnits = new List<Unit>();
        private List<Unit> _allyUnits = new List<Unit>();

        private HashSet<Unit> _actedUnits = new HashSet<Unit>();

        public override void _Ready()
        {
            CollectAllUnits();
        }

        public void CollectAllUnits()
        {
            _allUnits.Clear();
            _playerUnits.Clear();
            _enemyUnits.Clear();
            _allyUnits.Clear();

            var units = GetTree().GetNodesInGroup("Units");
            foreach (var node in units)
            {
                if (node is Unit unit)
                {
                    _allUnits.Add(unit);

                    switch (unit.Faction)
                    {
                        case UnitFaction.Player:
                            _playerUnits.Add(unit);
                            break;
                        case UnitFaction.Enemy:
                            _enemyUnits.Add(unit);
                            break;
                        case UnitFaction.Ally:
                            _allyUnits.Add(unit);
                            break;
                    }
                }
            }

            GD.Print($"回合管理器初始化完成 - 玩家:{_playerUnits.Count}, 敌军:{_enemyUnits.Count}, 友军:{_allyUnits.Count}");
        }

        public void StartGame()
        {
            CollectAllUnits();
            CurrentTurnNumber = 1;
            StartTurn(TurnPhase.PlayerPhase); // 从玩家回合开始游戏
        }

        // 显示阶段字幕 → 等待0.5秒 → 进入阶段
        public void StartTurn(TurnPhase phase)
        {
            CurrentPhase = phase;
            CurrentState = PhaseState.Starting;
            _actedUnits.Clear();

            GD.Print($"\n========== 第 {CurrentTurnNumber} 回合 - {GetPhaseName(phase)} ==========");

            string subtitle = GetAnnouncementText(phase);
            GD.Print($"[阶段提示] {subtitle}");

            EmitSignal(SignalName.PhaseAnnounced, subtitle, 0.5f);

            GetTree().CreateTimer(0.5f).Timeout += () =>
            {
                _isAnnouncing = false;
                BeginPhase(phase);
            };
        }

        // 字幕结束后正式激活阶段
        private void BeginPhase(TurnPhase phase)
        {
            ResetUnitsForPhase(phase); // 重置该阵营单位状态
            CurrentState = PhaseState.Active;
            EmitSignal(SignalName.PhaseStarted, (int)phase, CurrentTurnNumber);

            var aliveUnits = GetUnitsForPhase(phase).Where(u => u.State != UnitState.Dead).ToList();
            int aliveCount = aliveUnits.Count;

            // AI 阶段自动执行，玩家阶段等待"结束回合"按钮
            if (phase == TurnPhase.AllyPhase)
            {
                if (aliveCount > 0)
                    ExecuteAllyAI();
                else
                    EndCurrentPhase();
            }
            else if (phase == TurnPhase.EnemyPhase)
            {
                if (aliveCount > 0)
                    ExecuteEnemyAI();
                else
                    EndCurrentPhase();
            }
        }

        private void ResetUnitsForPhase(TurnPhase phase)
        {
            List<Unit> unitsToReset = GetUnitsForPhase(phase);

            foreach (var unit in unitsToReset)
            {
                if (unit.State != UnitState.Dead)
                {
                    unit.ResetTurn();
                }
            }

            GD.Print($"已重置 {unitsToReset.Count} 个单位的状态");
        }

        private List<Unit> GetUnitsForPhase(TurnPhase phase)
        {
            return phase switch
            {
                TurnPhase.PlayerPhase => _playerUnits,
                TurnPhase.EnemyPhase => _enemyUnits,
                TurnPhase.AllyPhase => _allyUnits,
                _ => new List<Unit>()
            };
        }

        public void MarkUnitActed(Unit unit)
        {
            if (!_actedUnits.Contains(unit))
            {
                _actedUnits.Add(unit);
                unit.PerformAction();

                GD.Print($"[{unit.UnitName}] 已行动 ({_actedUnits.Count}/{GetUnitsForPhase(CurrentPhase).Count(u => u.State != UnitState.Dead)})");

                CheckAllUnitsActed();
            }
        }

        private void CheckAllUnitsActed()
        {
            var phaseUnits = GetUnitsForPhase(CurrentPhase);
            var aliveUnits = phaseUnits.Where(u => u.State != UnitState.Dead).ToList();

            if (_actedUnits.Count >= aliveUnits.Count)
            {
                GD.Print("所有单位已行动完毕!");
                EmitSignal(SignalName.AllUnitsActed, (int)CurrentPhase);

                EndCurrentPhase();
            }
        }

        public void EndCurrentPhase()
        {
            if (CurrentState != PhaseState.Active)
                return;

            CurrentState = PhaseState.Ending;
            EmitSignal(SignalName.PhaseEnded, (int)CurrentPhase, CurrentTurnNumber);

            GD.Print($"========== {GetPhaseName(CurrentPhase)} 结束 ==========\n");

            AdvancePhase();
        }

        // 推进到下一阶段：玩家 → 友军 → 敌军 → 玩家（循环）
        private void AdvancePhase()
        {
            TurnPhase nextPhase = CurrentPhase switch
            {
                TurnPhase.PlayerPhase => TurnPhase.AllyPhase,
                TurnPhase.AllyPhase => TurnPhase.EnemyPhase,
                TurnPhase.EnemyPhase => TurnPhase.PlayerPhase,
                _ => TurnPhase.PlayerPhase
            };

            if (nextPhase == TurnPhase.PlayerPhase && CurrentPhase != TurnPhase.PlayerPhase)
            {
                CurrentTurnNumber++; // 回到玩家回合时增加回合数
                EmitSignal(SignalName.TurnAdvanced, CurrentTurnNumber);
            }

            StartTurn(nextPhase); // 开始下一阶段（先显示字幕）
        }

        // 敌军 AI：遍历存活敌军，逐个执行行动
        private void ExecuteEnemyAI()
        {
            GD.Print("敌军AI开始行动...");

            foreach (var enemy in _enemyUnits.Where(e => e.State != UnitState.Dead).ToList())
            {
                if (CurrentPhase != TurnPhase.EnemyPhase)
                    break;
                ExecuteEnemyTurn(enemy);
                MarkUnitActed(enemy);
            }

            if (CurrentState == PhaseState.Active) // 防止多单位时重复结束
                EndCurrentPhase();
        }

        // 友军 AI：遍历存活友军，简单寻找最近敌军后结束
        private void ExecuteAllyAI()
        {
            GD.Print("友军AI开始行动...");

            foreach (var ally in _allyUnits.Where(a => a.State != UnitState.Dead).ToList())
            {
                if (CurrentPhase != TurnPhase.AllyPhase)
                    break;

                GD.Print($"  [{ally.UnitName}] AI思考中...");

                Unit? nearestEnemy = FindNearestEnemyUnit(ally);
                if (nearestEnemy != null)
                {
                    int distance = CalculateDistance(ally.GridPosition, nearestEnemy.GridPosition);
                    GD.Print($"    发现敌 {nearestEnemy.UnitName}, 距离 {distance}");
                }
                else
                {
                    GD.Print($"  没有找到目标，原地待机");
                }

                MarkUnitActed(ally);
            }

            if (CurrentState == PhaseState.Active)
                EndCurrentPhase();
        }

        // 单个敌军 AI 决策：寻找最近玩家单位
        private void ExecuteEnemyTurn(Unit enemy)
        {
            GD.Print($"  [{enemy.UnitName}] AI思考中...");

            Unit? nearestPlayer = FindNearestPlayerUnit(enemy);

            if (nearestPlayer != null)
            {
                int distance = CalculateDistance(enemy.GridPosition, nearestPlayer.GridPosition);
                GD.Print($"    发现目标 {nearestPlayer.UnitName}, 距离 {distance}");

                if (distance == 1)
                {
                    GD.Print($"    发动攻击!");
                }
                else
                {
                    GD.Print($"    向目标移动");
                }
            }
            else
            {
                GD.Print($"  没有找到目标，原地待机");
            }
        }

        private Unit? FindNearestPlayerUnit(Unit enemy)
        {
            Unit? nearest = null;
            int minDistance = int.MaxValue;

            foreach (var player in _playerUnits.Where(p => p.State != UnitState.Dead))
            {
                int distance = CalculateDistance(enemy.GridPosition, player.GridPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = player;
                }
            }

            return nearest;
        }

        private Unit? FindNearestEnemyUnit(Unit ally)
        {
            Unit? nearest = null;
            int minDistance = int.MaxValue;

            foreach (var enemy in _enemyUnits.Where(e => e.State != UnitState.Dead))
            {
                int distance = CalculateDistance(ally.GridPosition, enemy.GridPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private int CalculateDistance(Vector2I a, Vector2I b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        private string GetPhaseName(TurnPhase phase)
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

        // 获取各阶段字幕文本
        private string GetAnnouncementText(TurnPhase phase)
        {
            return phase switch
            {
                TurnPhase.PlayerPhase => "我方行动",
                TurnPhase.EnemyPhase => "敌军行动",
                TurnPhase.AllyPhase => "友军行动",
                _ => "行动阶段"
            };
        }

        public bool AreAllPlayerUnitsDead()
        {
            return _playerUnits.All(u => u.State == UnitState.Dead);
        }

        public bool AreAllEnemyUnitsDead()
        {
            return _enemyUnits.All(u => u.State == UnitState.Dead);
        }

        public void PrintTurnStatus()
        {
            GD.Print($"\n--- 当前回合状态 ---");
            GD.Print($"回合数: {CurrentTurnNumber}, 当前阶段: {GetPhaseName(CurrentPhase)}");

            var phaseUnits = GetUnitsForPhase(CurrentPhase);
            var aliveUnits = phaseUnits.Where(u => u.State != UnitState.Dead).ToList();

            GD.Print($"当前阶段单位: {aliveUnits.Count} 存活 / {phaseUnits.Count} 总数");
            GD.Print($"已行动: {_actedUnits.Count} / {aliveUnits.Count}");

            foreach (var unit in aliveUnits)
            {
                string status = _actedUnits.Contains(unit) ? "已行动" : "待命中";
                GD.Print($"  [{unit.UnitName}] {status} - HP:{unit.Stats.HP}/{unit.Stats.MaxHP}");
            }
        }
    }
}