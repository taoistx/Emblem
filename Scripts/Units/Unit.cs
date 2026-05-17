using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FE5.Units
{
    public enum UnitFaction
    {
        Player,
        Enemy,
        Ally,
        Neutral
    }

    public enum UnitState
    {
        Idle,
        Selected,
        Moved,
        ActionDone,
        Dead
    }

    public partial class Unit : CharacterBody2D
    {
        [Export] public string UnitName { get; set; } = "Unknown";
        [Export] public UnitFaction Faction { get; set; } = UnitFaction.Player;
        [Export] public float MoveSpeed { get; set; } = 8.0f;

        public UnitStats Stats { get; private set; } = null!;
        public UnitState State { get; set; } = UnitState.Idle;
        public Vector2I GridPosition { get; set; }
        public bool IsMoving { get; private set; } = false;

        private Tween? _moveTween;

        [Signal] public delegate void UnitMovedEventHandler(Vector2I newPosition);
        [Signal] public delegate void UnitDiedEventHandler(Unit unit);
        [Signal] public delegate void StateChangedEventHandler(UnitState newState);
        [Signal] public delegate void MoveStartedEventHandler();
        [Signal] public delegate void MoveCompletedEventHandler();

        public override void _Ready()
        {
            Stats = new UnitStats();
            CreateUnitSprite();
        }

        private void CreateUnitSprite()
        {
            ColorRect rect = new ColorRect();
            rect.Size = new Vector2(24, 24);
            rect.Position = new Vector2(-12, -12);
            
            switch (Faction)
            {
                case UnitFaction.Player:
                    rect.Color = new Color(0.2f, 0.4f, 0.8f);
                    break;
                case UnitFaction.Enemy:
                    rect.Color = new Color(0.8f, 0.2f, 0.2f);
                    break;
                case UnitFaction.Ally:
                    rect.Color = new Color(0.2f, 0.8f, 0.4f);
                    break;
                default:
                    rect.Color = new Color(0.6f, 0.6f, 0.6f);
                    break;
            }
            
            AddChild(rect);

            Label nameLabel = new Label();
            nameLabel.Text = UnitName.Substring(0, Math.Min(2, UnitName.Length));
            nameLabel.Position = new Vector2(-10, -28);
            nameLabel.Size = new Vector2(20, 12);
            nameLabel.AddThemeFontSizeOverride("font_size", 10);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            AddChild(nameLabel);
        }

        public void Initialize(string name, UnitFaction faction, UnitStats stats)
        {
            UnitName = name;
            Faction = faction;
            Stats = stats;
            State = UnitState.Idle;

            ColorRect? rect = GetNodeOrNull<ColorRect>("ColorRect");
            if (rect != null)
            {
                switch (faction)
                {
                    case UnitFaction.Player:
                        rect.Color = new Color(0.2f, 0.4f, 0.8f);
                        break;
                    case UnitFaction.Enemy:
                        rect.Color = new Color(0.8f, 0.2f, 0.2f);
                        break;
                    case UnitFaction.Ally:
                        rect.Color = new Color(0.2f, 0.8f, 0.4f);
                        break;
                }
            }

            Label? label = GetNodeOrNull<Label>("Label");
            if (label != null)
            {
                label.Text = name.Substring(0, Math.Min(2, name.Length));
            }
        }

        // 立即传送到目标格子（无动画）
        public void MoveTo(Vector2I gridPos)
        {
            GridPosition = gridPos;
            Position = new Vector2(gridPos.X * 32 + 16, gridPos.Y * 32 + 16);
            State = UnitState.Moved;
            EmitSignal(SignalName.UnitMoved, gridPos);
            EmitSignal(SignalName.StateChanged, (int)State);
        }

        // 直线移动到目标格子（逐格动画，不经过寻路）
        public void MoveToWithAnimation(Vector2I targetGridPos, int cellSize = 32)
        {
            if (IsMoving)
                return;

            IsMoving = true;
            EmitSignal(SignalName.MoveStarted);

            Vector2 targetWorldPos = new Vector2(
                targetGridPos.X * cellSize + cellSize / 2,
                targetGridPos.Y * cellSize + cellSize / 2
            );

            float distance = Position.DistanceTo(targetWorldPos);
            float duration = distance / (cellSize * MoveSpeed);

            if (_moveTween != null && _moveTween.IsValid())
            {
                _moveTween.Kill();
            }

            _moveTween = CreateTween();
            _moveTween.TweenProperty(this, "position", targetWorldPos, duration);
            _moveTween.SetEase(Tween.EaseType.Out);
            _moveTween.SetTrans(Tween.TransitionType.Sine);
            _moveTween.TweenCallback(Callable.From(() => OnMoveAnimationComplete(targetGridPos)));
        }

        private void OnMoveAnimationComplete(Vector2I targetGridPos)
        {
            GridPosition = targetGridPos;
            IsMoving = false;
            State = UnitState.Moved;
            EmitSignal(SignalName.MoveCompleted);
            EmitSignal(SignalName.UnitMoved, targetGridPos);
            EmitSignal(SignalName.StateChanged, (int)State);
        }

        // 沿 BFS 路径逐格移动（匀速平滑插值）
        public void MoveAlongPath(List<Vector2I> path, int cellSize = 32)
        {
            if (IsMoving || path.Count < 2)
                return;

            IsMoving = true;
            EmitSignal(SignalName.MoveStarted);

            if (_moveTween != null && _moveTween.IsValid())
                _moveTween.Kill();

            _moveTween = CreateTween();

            foreach (Vector2I step in path.Skip(1))
            {
                Vector2 targetPos = new Vector2(
                    step.X * cellSize + cellSize / 2,
                    step.Y * cellSize + cellSize / 2
                );
                _moveTween.TweenProperty(this, "position", targetPos, 1.0f / MoveSpeed)
                    .SetTrans(Tween.TransitionType.Linear);
            }

            _moveTween.TweenCallback(Callable.From(() => OnMoveAlongPathComplete(path[^1])));
        }

        private void OnMoveAlongPathComplete(Vector2I targetGridPos)
        {
            GridPosition = targetGridPos;
            IsMoving = false;
            State = UnitState.Moved;
            EmitSignal(SignalName.MoveCompleted);
            EmitSignal(SignalName.UnitMoved, targetGridPos);
            EmitSignal(SignalName.StateChanged, (int)State);
        }

        // 取消移动动画
        public void CancelMove()
        {
            if (_moveTween != null && _moveTween.IsValid())
            {
                _moveTween.Kill();
            }
            IsMoving = false;
            EmitSignal(SignalName.MoveCompleted);
        }

        public void PerformAction()
        {
            State = UnitState.ActionDone;
            EmitSignal(SignalName.StateChanged, (int)State);
        }

        public void TakeDamage(int damage)
        {
            Stats.HP = Mathf.Max(0, Stats.HP - damage);
            GD.Print($"[{UnitName}] 受到 {damage} 点伤害, 剩余 HP: {Stats.HP}");

            if (Stats.HP <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            int oldHP = Stats.HP;
            Stats.HP = Mathf.Min(Stats.MaxHP, Stats.HP + amount);
            int healed = Stats.HP - oldHP;
            GD.Print($"[{UnitName}] 恢复 {healed} 点 HP, 当前 HP: {Stats.HP}/{Stats.MaxHP}");
        }

        private void Die()
        {
            State = UnitState.Dead;
            GD.Print($"[{UnitName}] 阵亡!");
            EmitSignal(SignalName.UnitDied, this);
            QueueFree();
        }

        public void ResetTurn()
        {
            if (State != UnitState.Dead)
            {
                State = UnitState.Idle;
                EmitSignal(SignalName.StateChanged, (int)State);
            }
        }

        public bool CanFollowUp(Unit target)
        {
            return Stats.CanFollowUpAttack(target.Stats);
        }

        public override string ToString()
        {
            return $"[{UnitName}] {Faction} - {Stats} - 位置:{GridPosition} - 状态:{State}";
        }
    }
}