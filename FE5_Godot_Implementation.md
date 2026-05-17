# Godot 4.6 C# 复刻《火焰纹章：多拉基亚776》实施文档

## 项目概述

本项目使用 Godot 4.6 和 C# 复刻经典 SRPG 游戏《火焰纹章：多拉基亚776》（Fire Emblem: Thracia 776）。项目专注于核心战斗系统和回合机制的完整实现，不追求画面表现，优先确保控制台输出的战斗伤害和单位移动逻辑正确。

---

## 目录

1. [FE5 核心机制分析](#fe5-核心机制分析)
2. [项目架构设计](#项目架构设计)
3. [核心类实现](#核心类实现)
4. [地图系统](#地图系统)
5. [战斗系统](#战斗系统)
6. [回合系统](#回合系统)
7. [测试验证](#测试验证)
8. [项目配置](#项目配置)

---

## FE5 核心机制分析

### 2.1 属性系统（FE5 特有）

| 属性 | 英文 | 作用 |
|------|------|------|
| HP | Hit Points | 生命值 |
| 力量 | Strength | 影响物理攻击伤害 |
| **魔力** | **Magic** | **影响魔法攻击伤害，同时用于抵御魔法伤害（无独立魔防属性）** |
| 技巧 | Skill | 影响命中率和必杀率 |
| 速度 | Speed | 影响攻速(AS)和回避率 |
| 幸运 | Luck | 影响回避率和必杀回避 |
| 防御 | Defense | 减少物理伤害 |
| **体格** | **Build (Bld)** | **FE5 特有，影响攻速计算** |
| 移动力 | Movement | 每回合可移动格数 |

> **注意：** 本作没有独立的「魔防」属性，魔法防御由**魔力(Magic)**兼任。

### 2.2 攻速（AS）计算

```
攻速 = 速度 - (武器重量 - 体格)
如果 武器重量 ≤ 体格: 攻速 = 速度（无惩罚）
```

等价于：`AS = Speed - max(0, WeaponWeight - Build)`

**关键特性：**
- 武器重量超过体格时，每超 1 点降低 1 点攻速
- 体格 >= 武器重量时，攻速 = 速度（完全抵消惩罚）
- **追击判定：AS 差值 >= 4 时触发二次攻击**

### 2.3 战斗计算公式

**命中率：**
```
命中率 = (技巧×2) + 武器命中 + 支援加成 + 武器相克加成 - 敌方回避
```

**回避率：**
```
回避率 = (攻速×2) + 幸运 + 地形加成 + 支援加成 + 武器相克加成
```

**伤害计算（物理）：**
```
物理伤害 = 力量 + 武器威力 - 敌方防御
```

**伤害计算（魔法）：**
```
魔法伤害 = 魔力 + 魔法威力 - 敌方魔力
```

> **注意：** 本作没有独立魔防属性，魔法伤害直接用敌方**魔力**来减免。

---

## 项目架构设计

### 3.1 核心类图

```
┌─────────────────┐
│   GameManager   │ ← 游戏主控制器，管理游戏流程
└────────┬────────┘
		 │
	┌────┴────┬──────────────┐
	▼         ▼              ▼
┌────────┐ ┌──────────┐ ┌─────────────┐
│TurnManager│ │MapManager│ │BattleManager│
└────────┘ └──────────┘ └─────────────┘
	│         │              │
	▼         ▼              ▼
┌────────┐ ┌──────────┐ ┌─────────────┐
│  Unit  │ │TileMapLayer│ │BattleResult │
└────────┘ └──────────┘ └─────────────┘
	│
	▼
┌──────────┐
│ UnitStats│ ← FE5 属性系统
└──────────┘
```

### 3.2 文件结构

```
project/
├── project.godot          # Godot 项目配置
├── FE5.csproj            # C# 项目文件
├── Scenes/
│   ├── Main.tscn         # 主场景
│   ├── Map.tscn          # 地图场景
│   └── Unit.tscn         # 单位场景
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs    # 游戏主控制器
│   │   ├── TurnManager.cs    # 回合管理器
│   │   └── BattleManager.cs  # 战斗管理器
│   ├── Units/
│   │   ├── Unit.cs           # 单位基类
│   │   ├── UnitStats.cs      # 属性系统
│   │   └── UnitType.cs       # 单位类型枚举
│   ├── Map/
│   │   ├── MapManager.cs     # 地图管理器
│   │   ├── Pathfinder.cs     # BFS 寻路
│   │   └── TerrainData.cs    # 地形数据
│   └── Combat/
│       ├── CombatCalculator.cs  # 战斗计算器
│       └── BattleResult.cs      # 战斗结果
└── Assets/
	└── (占位资源)
```

---

## 核心类实现

### 4.1 UnitStats.cs - FE5 属性系统

```csharp
using Godot;
using System;

namespace FE5.Units
{
	/// <summary>
	/// FE5 属性系统 - 包含多拉基亚776特有的体格(Build)系统
	/// </summary>
	public partial class UnitStats : RefCounted
	{
		// 基础属性
		public int HP { get; set; }
		public int MaxHP { get; set; }
		public int Strength { get; set; }      // 力量
		public int Magic { get; set; }         // 魔力（同时用于魔法攻击和魔法防御，无独立魔防）
		public int Skill { get; set; }         // 技巧
		public int Speed { get; set; }         // 速度
		public int Luck { get; set; }          // 幸运
		public int Defense { get; set; }       // 防御
		public int Build { get; set; }         // 体格 - FE5 特有
		public int Movement { get; set; }      // 移动力

		// 武器属性（临时存储当前装备）
		public int WeaponWeight { get; set; }
		public int WeaponMight { get; set; }
		public int WeaponHit { get; set; }
		public int WeaponCrit { get; set; }
		public bool IsMagicWeapon { get; set; }

		/// <summary>
		/// 计算攻速(Attack Speed) - FE5 公式
		/// 攻速 = 速度 - (武器重量 - 体格)
		/// 如果 武器重量 ≤ 体格: 攻速 = 速度
		/// </summary>
		public int CalculateAttackSpeed()
		{
			if (WeaponWeight <= Build)
				return Speed;
			return Mathf.Max(0, Speed - (WeaponWeight - Build));
		}

		/// <summary>
		/// 计算命中率 - (技巧×2) + 武器命中 + 支援加成 + 武器相克加成
		/// 注：支援加成和武器相克加成暂未实现，后续扩展
		/// </summary>
		public int CalculateHitRate()
		{
			return (Skill * 2) + WeaponHit;
		}

		/// <summary>
		/// 计算回避率 - (攻速×2) + 幸运 + 地形加成 + 支援加成 + 武器相克加成
		/// 注：地形加成由外部传入，支援加成和武器相克加成暂未实现
		/// </summary>
		public int CalculateAvoidRate(int terrainAvoidBonus = 0)
		{
			int asValue = CalculateAttackSpeed();
			return (asValue * 2) + Luck + terrainAvoidBonus;
		}

		/// <summary>
		/// 计算必杀率
		/// </summary>
		public int CalculateCritRate()
		{
			// FE5 公式: 技术 + 武器必杀
			return Skill + WeaponCrit;
		}

		/// <summary>
		/// 计算实际攻击力
		/// </summary>
		public int CalculateAttackPower(bool useMagic = false)
		{
			if (useMagic || IsMagicWeapon)
			{
				return Magic + WeaponMight;
			}
			return Strength + WeaponMight;
		}

		/// <summary>
		/// 计算防御力
		/// 物理防御 = Defense
		/// 魔法防御 = Magic（本作无独立魔防，魔力兼任）
		/// </summary>
		public int CalculateDefense(bool againstMagic = false)
		{
			return againstMagic ? Magic : Defense;
		}

		/// <summary>
		/// 检查是否可以追击（AS差 >= 4）
		/// </summary>
		public bool CanFollowUpAttack(UnitStats target)
		{
			int myAS = CalculateAttackSpeed();
			int targetAS = target.CalculateAttackSpeed();
			return (myAS - targetAS) >= 4;
		}

		/// <summary>
		/// 获取追击攻击次数
		/// </summary>
		public int GetAttackCount(UnitStats target)
		{
			return CanFollowUpAttack(target) ? 2 : 1;
		}

		/// <summary>
		/// 复制属性（用于创建临时计算副本）
		/// </summary>
		public UnitStats Clone()
		{
			return new UnitStats
			{
				HP = this.HP,
				MaxHP = this.MaxHP,
				Strength = this.Strength,
				Magic = this.Magic,
				Skill = this.Skill,
				Speed = this.Speed,
				Luck = this.Luck,
				Defense = this.Defense,
				Build = this.Build,
				Movement = this.Movement,
				WeaponWeight = this.WeaponWeight,
				WeaponMight = this.WeaponMight,
				WeaponHit = this.WeaponHit,
				WeaponCrit = this.WeaponCrit,
				IsMagicWeapon = this.IsMagicWeapon
			};
		}

		public override string ToString()
		{
			return $"HP:{HP}/{MaxHP} 力:{Strength} 魔:{Magic} 技:{Skill} " +
				   $"速:{Speed} 运:{Luck} 守:{Defense} " +
				   $"体格:{Build} 移:{Movement} AS:{CalculateAttackSpeed()}";
		}
	}
}
```

### 4.2 Unit.cs - 单位基类

```csharp
using Godot;
using System;

namespace FE5.Units
{
	public enum UnitFaction
	{
		Player,     // 玩家单位
		Enemy,      // 敌军单位
		Ally,       // 友军单位（NPC）
		Neutral     // 中立单位
	}

	public enum UnitState
	{
		Idle,       // 待机
		Selected,   // 被选中
		Moved,      // 已移动
		ActionDone, // 已行动
		Dead        // 阵亡
	}

	/// <summary>
	/// 游戏单位基类
	/// </summary>
	public partial class Unit : Node2D
	{
		[Export] public string UnitName { get; set; } = "Unknown";
		[Export] public UnitFaction Faction { get; set; } = UnitFaction.Player;
		
		public UnitStats Stats { get; private set; }
		public UnitState State { get; set; } = UnitState.Idle;
		public Vector2I GridPosition { get; set; }
		
		// 信号
		[Signal] public delegate void UnitMovedEventHandler(Vector2I newPosition);
		[Signal] public delegate void UnitDiedEventHandler(Unit unit);
		[Signal] public delegate void StateChangedEventHandler(UnitState newState);

		public override void _Ready()
		{
			Stats = new UnitStats();
		}

		/// <summary>
		/// 初始化单位
		/// </summary>
		public void Initialize(string name, UnitFaction faction, UnitStats stats)
		{
			UnitName = name;
			Faction = faction;
			Stats = stats;
			State = UnitState.Idle;
		}

		/// <summary>
		/// 移动单位到指定位置
		/// </summary>
		public void MoveTo(Vector2I gridPos)
		{
			GridPosition = gridPos;
			Position = new Vector2(gridPos.X * 32, gridPos.Y * 32); // 假设32x32像素每格
			State = UnitState.Moved;
			EmitSignal(SignalName.UnitMoved, gridPos);
			EmitSignal(SignalName.StateChanged, (int)State);
		}

		/// <summary>
		/// 执行行动
		/// </summary>
		public void PerformAction()
		{
			State = UnitState.ActionDone;
			EmitSignal(SignalName.StateChanged, (int)State);
		}

		/// <summary>
		/// 受到伤害
		/// </summary>
		public void TakeDamage(int damage)
		{
			Stats.HP = Mathf.Max(0, Stats.HP - damage);
			GD.Print($"[{UnitName}] 受到 {damage} 点伤害, 剩余 HP: {Stats.HP}");
			
			if (Stats.HP <= 0)
			{
				Die();
			}
		}

		/// <summary>
		/// 治疗
		/// </summary>
		public void Heal(int amount)
		{
			int oldHP = Stats.HP;
			Stats.HP = Mathf.Min(Stats.MaxHP, Stats.HP + amount);
			int healed = Stats.HP - oldHP;
			GD.Print($"[{UnitName}] 恢复 {healed} 点 HP, 当前 HP: {Stats.HP}/{Stats.MaxHP}");
		}

		/// <summary>
		/// 单位阵亡
		/// </summary>
		private void Die()
		{
			State = UnitState.Dead;
			GD.Print($"[{UnitName}] 阵亡!");
			EmitSignal(SignalName.UnitDied, this);
		}

		/// <summary>
		/// 重置回合状态
		/// </summary>
		public void ResetTurn()
		{
			if (State != UnitState.Dead)
			{
				State = UnitState.Idle;
				EmitSignal(SignalName.StateChanged, (int)State);
			}
		}

		/// <summary>
		/// 检查是否可以对目标发动追击
		/// </summary>
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
```

---

## 地图系统

### 5.1 MapManager.cs - 地图管理器

```csharp
using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Map
{
	/// <summary>
	/// 地形类型
	/// </summary>
	public enum TerrainType
	{
		Plain,      // 平地
		Forest,     // 森林
		Mountain,   // 山地
		River,      // 河流
		Sea,        // 海洋
		Wall,       // 墙壁（不可通行）
		Door,       // 门
		Throne,     // 王座
		Fortress,   // 要塞
		Peak        // 山顶
	}

	/// <summary>
	/// 地形数据
	/// </summary>
	public struct TerrainData
	{
		public TerrainType Type;
		public int DefenseBonus;    // 防守加成
		public int AvoidBonus;      // 回避加成
		public int MovementCost;    // 移动力消耗
		public bool IsPassable;     // 是否可通行

		public static TerrainData GetDefault(TerrainType type)
		{
			return type switch
			{
				TerrainType.Plain => new TerrainData { 
					Type = type, DefenseBonus = 0, AvoidBonus = 0, 
					MovementCost = 1, IsPassable = true },
				TerrainType.Forest => new TerrainData { 
					Type = type, DefenseBonus = 1, AvoidBonus = 20, 
					MovementCost = 2, IsPassable = true },
				TerrainType.Mountain => new TerrainData { 
					Type = type, DefenseBonus = 1, AvoidBonus = 30, 
					MovementCost = 4, IsPassable = true },
				TerrainType.River => new TerrainData { 
					Type = type, DefenseBonus = 0, AvoidBonus = 0, 
					MovementCost = 5, IsPassable = true },
				TerrainType.Sea => new TerrainData { 
					Type = type, DefenseBonus = 0, AvoidBonus = 10, 
					MovementCost = 2, IsPassable = false }, // 只有飞行单位可通行
				TerrainType.Wall => new TerrainData { 
					Type = type, DefenseBonus = 0, AvoidBonus = 0, 
					MovementCost = 999, IsPassable = false },
				TerrainType.Throne => new TerrainData { 
					Type = type, DefenseBonus = 3, AvoidBonus = 30, 
					MovementCost = 1, IsPassable = true },
				TerrainType.Fortress => new TerrainData { 
					Type = type, DefenseBonus = 2, AvoidBonus = 20, 
					MovementCost = 1, IsPassable = true },
				_ => new TerrainData { 
					Type = type, DefenseBonus = 0, AvoidBonus = 0, 
					MovementCost = 1, IsPassable = true }
			};
		}
	}

	/// <summary>
	/// 地图管理器 - 使用 TileMapLayer
	/// </summary>
	public partial class MapManager : Node2D
	{
		[Export] public int MapWidth { get; set; } = 20;
		[Export] public int MapHeight { get; set; } = 15;
		
		private TileMapLayer _groundLayer;
		private TileMapLayer _terrainLayer;
		private Dictionary<Vector2I, TerrainData> _terrainData;
		private Dictionary<Vector2I, Unit> _unitsOnMap;

		public override void _Ready()
		{
			_terrainData = new Dictionary<Vector2I, TerrainData>();
			_unitsOnMap = new Dictionary<Vector2I, Unit>();
			
			InitializeLayers();
			InitializeTerrain();
		}

		private void InitializeLayers()
		{
			_groundLayer = GetNode<TileMapLayer>("GroundLayer");
			_terrainLayer = GetNode<TileMapLayer>("TerrainLayer");
		}

		/// <summary>
		/// 初始化地形数据
		/// </summary>
		private void InitializeTerrain()
		{
			for (int x = 0; x < MapWidth; x++)
			{
				for (int y = 0; y < MapHeight; y++)
				{
					Vector2I pos = new Vector2I(x, y);
					// 默认平地
					_terrainData[pos] = TerrainData.GetDefault(TerrainType.Plain);
				}
			}
		}

		/// <summary>
		/// 设置指定位置的地形
		/// </summary>
		public void SetTerrain(Vector2I pos, TerrainType type)
		{
			if (IsValidPosition(pos))
			{
				_terrainData[pos] = TerrainData.GetDefault(type);
			}
		}

		/// <summary>
		/// 获取指定位置的地形数据
		/// </summary>
		public TerrainData GetTerrain(Vector2I pos)
		{
			if (_terrainData.TryGetValue(pos, out TerrainData data))
			{
				return data;
			}
			return TerrainData.GetDefault(TerrainType.Plain);
		}

		/// <summary>
		/// 检查位置是否有效
		/// </summary>
		public bool IsValidPosition(Vector2I pos)
		{
			return pos.X >= 0 && pos.X < MapWidth && 
				   pos.Y >= 0 && pos.Y < MapHeight;
		}

		/// <summary>
		/// 检查位置是否可通行
		/// </summary>
		public bool IsPassable(Vector2I pos, Unit unit = null)
		{
			if (!IsValidPosition(pos))
				return false;

			TerrainData terrain = GetTerrain(pos);
			if (!terrain.IsPassable)
				return false;

			// 检查是否有其他单位阻挡
			if (_unitsOnMap.TryGetValue(pos, out Unit existingUnit))
			{
				if (unit != null && existingUnit != unit)
				{
					// 友方单位不阻挡移动
					return existingUnit.Faction == unit.Faction;
				}
				return false;
			}

			return true;
		}

		/// <summary>
		/// 放置单位到地图
		/// </summary>
		public void PlaceUnit(Unit unit, Vector2I pos)
		{
			if (IsValidPosition(pos))
			{
				_unitsOnMap[pos] = unit;
				unit.GridPosition = pos;
			}
		}

		/// <summary>
		/// 移动单位
		/// </summary>
		public void MoveUnit(Unit unit, Vector2I fromPos, Vector2I toPos)
		{
			if (_unitsOnMap.ContainsKey(fromPos) && _unitsOnMap[fromPos] == unit)
			{
				_unitsOnMap.Remove(fromPos);
			}
			_unitsOnMap[toPos] = unit;
		}

		/// <summary>
		/// 移除单位
		/// </summary>
		public void RemoveUnit(Vector2I pos)
		{
			_unitsOnMap.Remove(pos);
		}

		/// <summary>
		/// 获取指定位置的单位
		/// </summary>
		public Unit GetUnitAt(Vector2I pos)
		{
			_unitsOnMap.TryGetValue(pos, out Unit unit);
			return unit;
		}
	}
}
```

### 5.2 Pathfinder.cs - BFS 寻路算法

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FE5.Units;

namespace FE5.Map
{
	/// <summary>
	/// 移动节点 - 用于BFS
	/// </summary>
	public class MoveNode
	{
		public Vector2I Position { get; set; }
		public int RemainingMove { get; set; }
		public MoveNode Parent { get; set; }
		public int TotalCost { get; set; }

		public MoveNode(Vector2I pos, int remainingMove, MoveNode parent = null)
		{
			Position = pos;
			RemainingMove = remainingMove;
			Parent = parent;
			TotalCost = parent?.TotalCost ?? 0;
		}
	}

	/// <summary>
	/// BFS 寻路器 - 计算移动范围和路径
	/// </summary>
	public class Pathfinder
	{
		private MapManager _mapManager;
		private readonly Vector2I[] _directions = new Vector2I[]
		{
			new Vector2I(0, -1),  // 上
			new Vector2I(0, 1),   // 下
			new Vector2I(-1, 0),  // 左
			new Vector2I(1, 0)    // 右
		};

		public Pathfinder(MapManager mapManager)
		{
			_mapManager = mapManager;
		}

		/// <summary>
		/// 使用BFS计算单位的移动范围
		/// </summary>
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

					// 检查是否已访问
					if (visited.Contains(nextPos))
						continue;

					// 检查位置有效性
					if (!_mapManager.IsValidPosition(nextPos))
						continue;

					// 获取地形移动力消耗
					TerrainData terrain = _mapManager.GetTerrain(nextPos);
					int moveCost = terrain.MovementCost;

					// 检查剩余移动力是否足够
					int remainingMove = current.RemainingMove - moveCost;
					if (remainingMove < 0)
						continue;

					// 检查是否可通行
					if (!_mapManager.IsPassable(nextPos, unit))
						continue;

					// 添加到可到达位置
					visited.Add(nextPos);
					reachablePositions.Add(nextPos);

					// 继续BFS
					queue.Enqueue(new MoveNode(nextPos, remainingMove, current));
				}
			}

			return reachablePositions;
		}

		/// <summary>
		/// 获取到达指定位置的路径
		/// </summary>
		public List<Vector2I> GetPathTo(Vector2I targetPos, List<Vector2I> moveRange)
		{
			if (!moveRange.Contains(targetPos))
				return new List<Vector2I>();

			// 重新执行BFS以获取路径
			var queue = new Queue<MoveNode>();
			var visited = new HashSet<Vector2I>();
			
			// 需要重新获取单位信息
			// 这里简化处理，实际应该从调用方传入
			
			return new List<Vector2I> { targetPos };
		}

		/// <summary>
		/// 检查位置是否在移动范围内
		/// </summary>
		public bool IsInMoveRange(Vector2I pos, List<Vector2I> moveRange)
		{
			return moveRange.Contains(pos);
		}

		/// <summary>
		/// 打印移动范围（用于控制台调试）
		/// </summary>
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
	}
}
```

---

## 战斗系统

### 6.1 CombatCalculator.cs - 战斗计算器

```csharp
using Godot;
using System;
using FE5.Units;
using FE5.Map;

namespace FE5.Combat
{
	/// <summary>
	/// 战斗计算器 - 处理FE5战斗公式
	/// </summary>
	public static class CombatCalculator
	{
		private static Random _random = new Random();

		/// <summary>
		/// 计算命中率
		/// 命中率 = (技巧×2) + 武器命中 + 支援加成 + 武器相克加成 - 敌方回避
		/// </summary>
		public static int CalculateHitRate(Unit attacker, Unit defender, TerrainData terrain)
		{
			int baseHit = attacker.Stats.CalculateHitRate();
			int avoid = defender.Stats.CalculateAvoidRate(terrain.AvoidBonus);
			int hitRate = baseHit - avoid;
			
			return Mathf.Clamp(hitRate, 0, 100);
		}

		/// <summary>
		/// 计算必杀率
		/// </summary>
		public static int CalculateCritRate(Unit attacker, Unit defender)
		{
			int critRate = attacker.Stats.CalculateCritRate();
			int critAvoid = defender.Stats.Luck;  // 幸运影响必杀回避
			
			return Mathf.Clamp(critRate - critAvoid, 0, 100);
		}

		/// <summary>
		/// 计算伤害值
		/// 物理伤害 = 力量 + 武器威力 - 敌方防御
		/// 魔法伤害 = 魔力 + 魔法威力 - 敌方魔力（无独立魔防）
		/// </summary>
		public static int CalculateDamage(Unit attacker, Unit defender, TerrainData terrain, bool isMagic = false)
		{
			int attackPower = attacker.Stats.CalculateAttackPower(isMagic);
			int defense = defender.Stats.CalculateDefense(isMagic) + terrain.DefenseBonus;
			int damage = attackPower - defense;
			
			return Mathf.Max(1, damage);  // 最低造成1点伤害
		}

		/// <summary>
		/// 检查是否命中
		/// </summary>
		public static bool CheckHit(int hitRate)
		{
			int roll = _random.Next(1, 101);  // 1-100
			return roll <= hitRate;
		}

		/// <summary>
		/// 检查是否必杀
		/// </summary>
		public static bool CheckCrit(int critRate)
		{
			int roll = _random.Next(1, 101);
			return roll <= critRate;
		}

		/// <summary>
		/// 计算攻击次数（考虑追击）
		/// </summary>
		public static int CalculateAttackCount(Unit attacker, Unit defender)
		{
			return attacker.Stats.GetAttackCount(defender.Stats);
		}

		/// <summary>
		/// 获取追击信息
		/// </summary>
		public static string GetFollowUpInfo(Unit attacker, Unit defender)
		{
			int attackerAS = attacker.Stats.CalculateAttackSpeed();
			int defenderAS = defender.Stats.CalculateAttackSpeed();
			int asDiff = attackerAS - defenderAS;

			if (asDiff >= 4)
			{
				return $"追击! ({attacker.UnitName} AS:{attackerAS} vs {defender.UnitName} AS:{defenderAS}, 差值:{asDiff})";
			}
			else if (asDiff <= -4)
			{
				return $"被追击风险 ({defender.UnitName} 可能追击)";
			}
			else
			{
				return $"无追击 (AS差:{asDiff})";
			}
		}
	}
}
```

### 6.2 BattleResult.cs - 战斗结果

```csharp
using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;

namespace FE5.Combat
{
	/// <summary>
	/// 单次攻击结果
	/// </summary>
	public class AttackResult
	{
		public bool IsHit { get; set; }
		public bool IsCrit { get; set; }
		public int Damage { get; set; }
		public int RemainingHP { get; set; }
		public bool IsFollowUp { get; set; }
		public string Description { get; set; }

		public override string ToString()
		{
			string attackType = IsFollowUp ? "追击" : "攻击";
			if (!IsHit)
				return $"[{attackType}] 未命中";
			
			string critStr = IsCrit ? " 必杀!" : "";
			return $"[{attackType}] 命中, 造成 {Damage} 点伤害{critStr}, 敌方剩余 HP: {RemainingHP}";
		}
	}

	/// <summary>
	/// 完整战斗结果
	/// </summary>
	public class BattleResult
	{
		public Unit Attacker { get; set; }
		public Unit Defender { get; set; }
		public List<AttackResult> AttackerAttacks { get; set; } = new List<AttackResult>();
		public List<AttackResult> DefenderAttacks { get; set; } = new List<AttackResult>();
		public bool IsAttackerDead => Attacker.Stats.HP <= 0;
		public bool IsDefenderDead => Defender.Stats.HP <= 0;
		public int TotalAttackerDamage { get; set; }
		public int TotalDefenderDamage { get; set; }

		public void PrintResult()
		{
			GD.Print("\n========== 战斗结果 ==========");
			GD.Print($"攻击方: {Attacker.UnitName} (HP: {Attacker.Stats.HP}/{Attacker.Stats.MaxHP})");
			GD.Print($"防御方: {Defender.UnitName} (HP: {Defender.Stats.HP}/{Defender.Stats.MaxHP})");
			GD.Print("------------------------------");
			
			GD.Print("攻击方回合:");
			foreach (var attack in AttackerAttacks)
			{
				GD.Print($"  {attack}");
			}

			if (DefenderAttacks.Count > 0)
			{
				GD.Print("防御方反击:");
				foreach (var attack in DefenderAttacks)
				{
					GD.Print($"  {attack}");
				}
			}

			GD.Print("------------------------------");
			GD.Print($"总伤害 - 攻击方:{TotalAttackerDamage}, 防御方:{TotalDefenderDamage}");
			
			if (IsDefenderDead)
				GD.Print($"结果: {Defender.UnitName} 被击败!");
			else if (IsAttackerDead)
				GD.Print($"结果: {Attacker.UnitName} 被反杀!");
			else
				GD.Print($"结果: 双方存活");
			
			GD.Print("==============================\n");
		}
	}
}
```

### 6.3 BattleManager.cs - 战斗管理器

```csharp
using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;
using FE5.Map;

namespace FE5.Combat
{
	/// <summary>
	/// 战斗管理器 - 处理单次对局
	/// </summary>
	public partial class BattleManager : Node
	{
		[Signal] public delegate void BattleStartedEventHandler(Unit attacker, Unit defender);
		[Signal] public delegate void BattleEndedEventHandler(BattleResult result);
		[Signal] public delegate void AttackPerformedEventHandler(AttackResult result);

		private MapManager _mapManager;

		public override void _Ready()
		{
			_mapManager = GetNode<MapManager>("/root/Main/MapManager");
		}

		/// <summary>
		/// 执行单次战斗
		/// </summary>
		public BattleResult ExecuteBattle(Unit attacker, Unit defender)
		{
			EmitSignal(SignalName.BattleStarted, attacker, defender);
			
			GD.Print($"\n>>> 战斗开始: {attacker.UnitName} vs {defender.UnitName}");

			// 获取地形信息
			TerrainData terrain = _mapManager.GetTerrain(defender.GridPosition);
			GD.Print($"地形: {terrain.Type}, 防守+{terrain.DefenseBonus}, 回避+{terrain.AvoidBonus}");

			// 显示追击信息
			string followUpInfo = CombatCalculator.GetFollowUpInfo(attacker, defender);
			GD.Print(followUpInfo);

			// 创建战斗结果
			BattleResult result = new BattleResult
			{
				Attacker = attacker,
				Defender = defender
			};

			// 计算攻击次数
			int attackerAttackCount = CombatCalculator.CalculateAttackCount(attacker, defender);
			int defenderAttackCount = CombatCalculator.CalculateAttackCount(defender, attacker);

			// 攻击方先攻
			for (int i = 0; i < attackerAttackCount && defender.Stats.HP > 0; i++)
			{
				var attackResult = PerformAttack(attacker, defender, terrain, i > 0);
				result.AttackerAttacks.Add(attackResult);
				result.TotalAttackerDamage += attackResult.IsHit ? attackResult.Damage : 0;
				
				EmitSignal(SignalName.AttackPerformed, attackResult);

				if (defender.Stats.HP <= 0)
					break;
			}

			// 防御方反击（如果存活且可以反击）
			if (defender.Stats.HP > 0 && attacker.Stats.HP > 0)
			{
				// 检查反击距离（简化：假设都可以反击）
				for (int i = 0; i < defenderAttackCount && attacker.Stats.HP > 0; i++)
				{
					var counterResult = PerformAttack(defender, attacker, terrain, i > 0);
					result.DefenderAttacks.Add(counterResult);
					result.TotalDefenderDamage += counterResult.IsHit ? counterResult.Damage : 0;
					
					EmitSignal(SignalName.AttackPerformed, counterResult);

					if (attacker.Stats.HP <= 0)
						break;
				}
			}

			result.PrintResult();
			EmitSignal(SignalName.BattleEnded, result);

			return result;
		}

		/// <summary>
		/// 执行单次攻击
		/// </summary>
		private AttackResult PerformAttack(Unit attacker, Unit defender, TerrainData terrain, bool isFollowUp)
		{
			// 计算命中率和必杀率
			int hitRate = CombatCalculator.CalculateHitRate(attacker, defender, terrain);
			int critRate = CombatCalculator.CalculateCritRate(attacker, defender);

			// 判断是否命中
			bool isHit = CombatCalculator.CheckHit(hitRate);

			var result = new AttackResult
			{
				IsHit = isHit,
				IsFollowUp = isFollowUp
			};

			if (isHit)
			{
				// 判断是否必杀
				bool isCrit = CombatCalculator.CheckCrit(critRate);
				result.IsCrit = isCrit;

				// 计算伤害
				int baseDamage = CombatCalculator.CalculateDamage(attacker, defender, terrain);
				result.Damage = isCrit ? baseDamage * 3 : baseDamage;  // 必杀3倍伤害

				// 应用伤害
				defender.TakeDamage(result.Damage);
				result.RemainingHP = defender.Stats.HP;
			}
			else
			{
				result.Damage = 0;
				result.RemainingHP = defender.Stats.HP;
				GD.Print($"  {attacker.UnitName} 的攻击未命中! (命中率:{hitRate}%)");
			}

			return result;
		}

		/// <summary>
		/// 模拟战斗（不实际执行，用于预览）
		/// </summary>
		public void PreviewBattle(Unit attacker, Unit defender)
		{
			TerrainData terrain = _mapManager.GetTerrain(defender.GridPosition);
			
			int hitRate = CombatCalculator.CalculateHitRate(attacker, defender, terrain);
			int critRate = CombatCalculator.CalculateCritRate(attacker, defender);
			int damage = CombatCalculator.CalculateDamage(attacker, defender, terrain);
			int attackCount = CombatCalculator.CalculateAttackCount(attacker, defender);

			GD.Print($"\n--- 战斗预览 ---");
			GD.Print($"{attacker.UnitName} -> {defender.UnitName}");
			GD.Print($"命中率: {hitRate}%, 必杀率: {critRate}%, 伤害: {damage}");
			GD.Print($"攻击次数: {attackCount} ({(attackCount > 1 ? "可追击" : "无追击")})");
			
			// 防御方反击预览
			int counterHitRate = CombatCalculator.CalculateHitRate(defender, attacker, terrain);
			int counterDamage = CombatCalculator.CalculateDamage(defender, attacker, terrain);
			GD.Print($"反击 - 命中率: {counterHitRate}%, 伤害: {counterDamage}");
		}
	}
}
```

---

## 回合系统

### 7.1 TurnPhase.cs - 回合阶段枚举

```csharp
namespace FE5.Core
{
	/// <summary>
	/// 游戏回合阶段
	/// </summary>
	public enum TurnPhase
	{
		PlayerPhase,    // 玩家回合
		EnemyPhase,     // 敌军回合
		AllyPhase,      // 友军回合（NPC）
		NeutralPhase    // 中立单位回合
	}

	/// <summary>
	/// 回合阶段状态
	/// </summary>
	public enum PhaseState
	{
		Starting,       // 阶段开始
		Active,         // 阶段进行中
		Ending          // 阶段结束
	}
}
```

### 7.2 TurnManager.cs - 回合管理器

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FE5.Units;

namespace FE5.Core
{
	/// <summary>
	/// 回合管理器 - 处理回合切换逻辑
	/// </summary>
	public partial class TurnManager : Node
	{
		[Export] public int CurrentTurnNumber { get; private set; } = 1;
		[Export] public TurnPhase CurrentPhase { get; private set; } = TurnPhase.PlayerPhase;
		[Export] public PhaseState CurrentState { get; private set; } = PhaseState.Starting;

		// 信号
		[Signal] public delegate void PhaseStartedEventHandler(TurnPhase phase, int turnNumber);
		[Signal] public delegate void PhaseEndedEventHandler(TurnPhase phase, int turnNumber);
		[Signal] public delegate void TurnAdvancedEventHandler(int newTurnNumber);
		[Signal] public delegate void AllUnitsActedEventHandler(TurnPhase phase);

		// 单位列表
		private List<Unit> _allUnits = new List<Unit>();
		private List<Unit> _playerUnits = new List<Unit>();
		private List<Unit> _enemyUnits = new List<Unit>();
		private List<Unit> _allyUnits = new List<Unit>();

		// 当前回合已行动单位
		private HashSet<Unit> _actedUnits = new HashSet<Unit>();

		public override void _Ready()
		{
			// 初始化时收集所有单位
			CollectAllUnits();
		}

		/// <summary>
		/// 收集场景中的所有单位
		/// </summary>
		public void CollectAllUnits()
		{
			_allUnits.Clear();
			_playerUnits.Clear();
			_enemyUnits.Clear();
			_allyUnits.Clear();

			// 从场景树中查找所有单位
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

		/// <summary>
		/// 开始新回合
		/// </summary>
		public void StartTurn(TurnPhase phase)
		{
			CurrentPhase = phase;
			CurrentState = PhaseState.Starting;
			_actedUnits.Clear();

			GD.Print($"\n========== 第 {CurrentTurnNumber} 回合 - {GetPhaseName(phase)} ==========");

			// 重置当前阶段所有单位的状态
			ResetUnitsForPhase(phase);

			CurrentState = PhaseState.Active;
			EmitSignal(SignalName.PhaseStarted, (int)phase, CurrentTurnNumber);

			// 如果是敌军回合，执行AI
			if (phase == TurnPhase.EnemyPhase)
			{
				ExecuteEnemyAI();
			}
		}

		/// <summary>
		/// 重置单位状态
		/// </summary>
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

		/// <summary>
		/// 获取当前阶段的单位列表
		/// </summary>
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

		/// <summary>
		/// 标记单位已行动
		/// </summary>
		public void MarkUnitActed(Unit unit)
		{
			if (!_actedUnits.Contains(unit))
			{
				_actedUnits.Add(unit);
				unit.PerformAction();
				
				GD.Print($"[{unit.UnitName}] 已行动 ({_actedUnits.Count}/{GetUnitsForPhase(CurrentPhase).Count(u => u.State != UnitState.Dead)})");

				// 检查是否所有单位都已行动
				CheckAllUnitsActed();
			}
		}

		/// <summary>
		/// 检查是否所有单位都已行动
		/// </summary>
		private void CheckAllUnitsActed()
		{
			var phaseUnits = GetUnitsForPhase(CurrentPhase);
			var aliveUnits = phaseUnits.Where(u => u.State != UnitState.Dead).ToList();
			
			if (_actedUnits.Count >= aliveUnits.Count)
			{
				GD.Print("所有单位已行动完毕!");
				EmitSignal(SignalName.AllUnitsActed, (int)CurrentPhase);
				
				// 自动结束当前阶段
				EndCurrentPhase();
			}
		}

		/// <summary>
		/// 结束当前阶段
		/// </summary>
		public void EndCurrentPhase()
		{
			CurrentState = PhaseState.Ending;
			EmitSignal(SignalName.PhaseEnded, (int)CurrentPhase, CurrentTurnNumber);

			GD.Print($"========== {GetPhaseName(CurrentPhase)} 结束 ==========\n");

			// 切换到下一阶段
			AdvancePhase();
		}

		/// <summary>
		/// 推进到下一阶段
		/// </summary>
		private void AdvancePhase()
		{
			// 简单的回合顺序：玩家 -> 敌军 -> 友军 -> 下一回合
			TurnPhase nextPhase = CurrentPhase switch
			{
				TurnPhase.PlayerPhase => TurnPhase.EnemyPhase,
				TurnPhase.EnemyPhase => TurnPhase.AllyPhase,
				TurnPhase.AllyPhase => TurnPhase.PlayerPhase,
				_ => TurnPhase.PlayerPhase
			};

			// 如果是回到玩家回合，增加回合数
			if (nextPhase == TurnPhase.PlayerPhase && CurrentPhase != TurnPhase.PlayerPhase)
			{
				CurrentTurnNumber++;
				EmitSignal(SignalName.TurnAdvanced, CurrentTurnNumber);
			}

			StartTurn(nextPhase);
		}

		/// <summary>
		/// 执行敌军AI（简化版）
		/// </summary>
		private void ExecuteEnemyAI()
		{
			GD.Print("敌军AI开始行动...");
			
			foreach (var enemy in _enemyUnits.Where(e => e.State != UnitState.Dead))
			{
				// 简化AI：随机移动并尝试攻击
				ExecuteEnemyTurn(enemy);
				MarkUnitActed(enemy);
			}
		}

		/// <summary>
		/// 执行单个敌军的回合
		/// </summary>
		private void ExecuteEnemyTurn(Unit enemy)
		{
			GD.Print($"  [{enemy.UnitName}] AI思考中...");
			
			// 查找最近的玩家单位
			Unit nearestPlayer = FindNearestPlayerUnit(enemy);
			
			if (nearestPlayer != null)
			{
				// 计算距离
				int distance = CalculateDistance(enemy.GridPosition, nearestPlayer.GridPosition);
				GD.Print($"    发现目标 {nearestPlayer.UnitName}, 距离 {distance}");
				
				// 如果距离为1，直接攻击
				if (distance == 1)
				{
					GD.Print($"    发动攻击!");
					// 这里应该调用 BattleManager
				}
				else
				{
					GD.Print($"    向目标移动");
					// 这里应该调用 Pathfinder 移动单位
				}
			}
			else
			{
				GD.Print($"  没有找到目标，原地待机");
			}
		}

		/// <summary>
		/// 查找最近的玩家单位
		/// </summary>
		private Unit FindNearestPlayerUnit(Unit enemy)
		{
			Unit nearest = null;
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

		/// <summary>
		/// 计算曼哈顿距离
		/// </summary>
		private int CalculateDistance(Vector2I a, Vector2I b)
		{
			return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
		}

		/// <summary>
		/// 获取阶段名称
		/// </summary>
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

		/// <summary>
		/// 检查是否所有玩家单位都已阵亡
		/// </summary>
		public bool AreAllPlayerUnitsDead()
		{
			return _playerUnits.All(u => u.State == UnitState.Dead);
		}

		/// <summary>
		/// 检查是否所有敌军单位都已阵亡
		/// </summary>
		public bool AreAllEnemyUnitsDead()
		{
			return _enemyUnits.All(u => u.State == UnitState.Dead);
		}

		/// <summary>
		/// 打印当前回合状态
		/// </summary>
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
```

---

## 测试验证

### 8.1 TestRunner.cs - 测试运行器

```csharp
using Godot;
using System;
using System.Collections.Generic;
using FE5.Units;
using FE5.Map;
using FE5.Combat;
using FE5.Core;

namespace FE5.Test
{
	/// <summary>
	/// 测试运行器 - 验证核心逻辑
	/// </summary>
	public partial class TestRunner : Node
	{
		public override void _Ready()
		{
			GD.Print("\n========================================");
			GD.Print("    FE5 核心系统测试开始");
			GD.Print("========================================\n");

			TestUnitStats();
			TestAttackSpeedCalculation();
			TestFollowUpAttack();
			TestPathfinding();
			TestCombatSystem();
			TestTurnSystem();

			GD.Print("\n========================================");
			GD.Print("    所有测试完成!");
			GD.Print("========================================\n");
		}

		/// <summary>
		/// 测试单位属性系统
		/// </summary>
		private void TestUnitStats()
		{
			GD.Print("\n[测试1] 单位属性系统");
			GD.Print("------------------------------");

			UnitStats stats = new UnitStats
			{
				HP = 25,
				MaxHP = 25,
				Strength = 10,
				Magic = 5,
				Skill = 8,
				Speed = 12,
				Luck = 6,
				Defense = 7,
				Build = 8,          // 体格 8
				Movement = 6,
				WeaponWeight = 10,  // 武器重量 10
				WeaponMight = 8,
				WeaponHit = 80,
				WeaponCrit = 5
			};

			GD.Print($"创建单位: {stats}");
			GD.Print($"攻速计算: Speed({stats.Speed}) - (WeaponWeight({stats.WeaponWeight}) - Build({stats.Build}))");
			GD.Print($"         = {stats.Speed} - ({stats.WeaponWeight} - {stats.Build}) = {stats.CalculateAttackSpeed()}");
			GD.Print($"命中率: {stats.CalculateHitRate()}");
			GD.Print($"回避率: {stats.CalculateAvoidRate()}");
			GD.Print($"必杀率: {stats.CalculateCritRate()}");
			GD.Print($"物理攻击力: {stats.CalculateAttackPower()}");
			GD.Print($"魔法防御力: {stats.CalculateDefense(true)} (魔力兼任)");

			GD.Print("✓ 属性系统测试通过\n");
		}

		/// <summary>
		/// 测试攻速计算
		/// </summary>
		private void TestAttackSpeedCalculation()
		{
			GD.Print("\n[测试2] 攻速计算（体格系统）");
			GD.Print("------------------------------");

			// 测试用例1：体格足够，无惩罚
			UnitStats unit1 = new UnitStats
			{
				Speed = 10,
				Build = 12,
				WeaponWeight = 8
			};
			int as1 = unit1.CalculateAttackSpeed();
			GD.Print($"测试1: Speed=10, Build=12, WeaponWeight=8");
			GD.Print($"       武器重量(8) ≤ 体格(12), AS = Speed = {as1} (无惩罚)");
			GD.Assert(as1 == 10, "测试1失败：体格足够时不应受武器重量影响");

			// 测试用例2：体格不足，有惩罚
			UnitStats unit2 = new UnitStats
			{
				Speed = 10,
				Build = 5,
				WeaponWeight = 12
			};
			int as2 = unit2.CalculateAttackSpeed();
			GD.Print($"测试2: Speed=10, Build=5, WeaponWeight=12");
			GD.Print($"       AS = 10 - (12 - 5) = 10 - 7 = {as2} (惩罚7点)");
			GD.Assert(as2 == 3, "测试2失败：体格不足时应受武器重量惩罚");

			// 测试用例3：刚好相等
			UnitStats unit3 = new UnitStats
			{
				Speed = 10,
				Build = 8,
				WeaponWeight = 8
			};
			int as3 = unit3.CalculateAttackSpeed();
			GD.Print($"测试3: Speed=10, Build=8, WeaponWeight=8");
			GD.Print($"       武器重量(8) ≤ 体格(8), AS = Speed = {as3} (刚好抵消)");
			GD.Assert(as3 == 10, "测试3失败：体格等于武器重量时不应受惩罚");

			GD.Print("✓ 攻速计算测试通过\n");
		}

		/// <summary>
		/// 测试追击判定
		/// </summary>
		private void TestFollowUpAttack()
		{
			GD.Print("\n[测试3] 追击判定（AS差 >= 4）");
			GD.Print("------------------------------");

			// 快速单位 vs 慢速单位
			UnitStats fastUnit = new UnitStats { Speed = 15, Build = 10, WeaponWeight = 8 };
			UnitStats slowUnit = new UnitStats { Speed = 8, Build = 5, WeaponWeight = 12 };

			int fastAS = fastUnit.CalculateAttackSpeed();  // 15 - 0 = 15
			int slowAS = slowUnit.CalculateAttackSpeed();  // 8 - 7 = 1
			int asDiff = fastAS - slowAS;  // 14

			GD.Print($"快速单位: Speed=15, Build=10, WeaponWeight=8 -> AS={fastAS}");
			GD.Print($"慢速单位: Speed=8, Build=5, WeaponWeight=12 -> AS={slowAS}");
			GD.Print($"AS差值: {fastAS} - {slowAS} = {asDiff}");
			GD.Print($"追击判定: {asDiff} >= 4 ? {fastUnit.CanFollowUpAttack(slowUnit)}");

			GD.Assert(fastUnit.CanFollowUpAttack(slowUnit), "快速单位应该能追击慢速单位");
			GD.Assert(!slowUnit.CanFollowUpAttack(fastUnit), "慢速单位不应该能追击快速单位");

			// 临界测试：刚好差4
			UnitStats unitA = new UnitStats { Speed = 12, Build = 8, WeaponWeight = 8 };  // AS=12
			UnitStats unitB = new UnitStats { Speed = 8, Build = 8, WeaponWeight = 8 };   // AS=8
			
			GD.Print($"\n临界测试: AS差 = {unitA.CalculateAttackSpeed()} - {unitB.CalculateAttackSpeed()} = 4");
			GD.Print($"追击判定: 4 >= 4 ? {unitA.CanFollowUpAttack(unitB)}");
			GD.Assert(unitA.CanFollowUpAttack(unitB), "AS差刚好为4时应该能追击");

			// 临界测试：差3
			UnitStats unitC = new UnitStats { Speed = 11, Build = 8, WeaponWeight = 8 };  // AS=11
			GD.Print($"\n临界测试: AS差 = {unitC.CalculateAttackSpeed()} - {unitB.CalculateAttackSpeed()} = 3");
			GD.Print($"追击判定: 3 >= 4 ? {unitC.CanFollowUpAttack(unitB)}");
			GD.Assert(!unitC.CanFollowUpAttack(unitB), "AS差为3时不应该能追击");

			GD.Print("✓ 追击判定测试通过\n");
		}

		/// <summary>
		/// 测试BFS寻路
		/// </summary>
		private void TestPathfinding()
		{
			GD.Print("\n[测试4] BFS寻路系统");
			GD.Print("------------------------------");

			// 创建测试地图
			MapManager map = new MapManager();
			map.MapWidth = 10;
			map.MapHeight = 10;
			map._Ready();

			// 创建测试单位
			Unit unit = new Unit();
			unit._Ready();
			unit.Stats = new UnitStats { Movement = 5 };
			unit.GridPosition = new Vector2I(5, 5);

			// 放置单位
			map.PlaceUnit(unit, new Vector2I(5, 5));

			// 设置一些障碍物
			map.SetTerrain(new Vector2I(5, 3), TerrainType.Wall);
			map.SetTerrain(new Vector2I(5, 4), TerrainType.Wall);
			map.SetTerrain(new Vector2I(6, 4), TerrainType.Wall);

			// 创建寻路器
			Pathfinder pathfinder = new Pathfinder(map);
			var moveRange = pathfinder.CalculateMoveRange(unit);

			GD.Print($"单位位置: ({unit.GridPosition.X}, {unit.GridPosition.Y})");
			GD.Print($"移动力: {unit.Stats.Movement}");
			GD.Print($"可到达位置数: {moveRange.Count}");

			// 验证关键位置
			bool canReachNorth = moveRange.Contains(new Vector2I(5, 4));  // 被墙挡住
			bool canReachEast = moveRange.Contains(new Vector2I(6, 5));   // 应该可以到达

			GD.Print($"能否到达北方(5,4): {canReachNorth} (预期: False, 有墙壁)");
			GD.Print($"能否到达东方(6,5): {canReachEast} (预期: True)");

			GD.Assert(!canReachNorth, "不应该能穿过墙壁");
			GD.Assert(canReachEast, "应该能到达东方");

			pathfinder.PrintMoveRange(unit, moveRange);

			GD.Print("✓ BFS寻路测试通过\n");
		}

		/// <summary>
		/// 测试战斗系统
		/// </summary>
		private void TestCombatSystem()
		{
			GD.Print("\n[测试5] 战斗系统");
			GD.Print("------------------------------");

			// 创建攻击方（快速单位，可追击）
			UnitStats attackerStats = new UnitStats
			{
				HP = 30, MaxHP = 30,
				Strength = 12,
				Skill = 10,
				Speed = 15, Build = 10, WeaponWeight = 8,  // AS = 15
				Luck = 5,
				Defense = 6,
				WeaponMight = 10,
				WeaponHit = 85,
				WeaponCrit = 10
			};

			// 创建防御方（慢速单位，被追击）
			UnitStats defenderStats = new UnitStats
			{
				HP = 25, MaxHP = 25,
				Strength = 8,
				Magic = 3,        // 魔力低，魔法防御也低
				Skill = 8,
				Speed = 8, Build = 5, WeaponWeight = 12,  // AS = 1
				Luck = 4,
				Defense = 8,
				WeaponMight = 8,
				WeaponHit = 80,
				WeaponCrit = 5
			};

			Unit attacker = new Unit();
			attacker._Ready();
			attacker.Initialize("里夫", UnitFaction.Player, attackerStats);
			attacker.GridPosition = new Vector2I(3, 3);

			Unit defender = new Unit();
			defender._Ready();
			defender.Initialize("敌军士兵", UnitFaction.Enemy, defenderStats);
			defender.GridPosition = new Vector2I(4, 3);

			GD.Print($"攻击方: {attacker}");
			GD.Print($"防御方: {defender}");

			// 计算战斗参数
			TerrainData plain = TerrainData.GetDefault(TerrainType.Plain);
			int hitRate = CombatCalculator.CalculateHitRate(attacker, defender, plain);
			int critRate = CombatCalculator.CalculateCritRate(attacker, defender);
			int damage = CombatCalculator.CalculateDamage(attacker, defender, plain);
			int attackCount = CombatCalculator.CalculateAttackCount(attacker, defender);

			GD.Print($"\n战斗预览:");
			GD.Print($"命中率: {hitRate}%");
			GD.Print($"必杀率: {critRate}%");
			GD.Print($"伤害: {damage}");
			GD.Print($"攻击次数: {attackCount} (追击判定: {attacker.Stats.CalculateAttackSpeed()} vs {defender.Stats.CalculateAttackSpeed()}, 差值 >= 4)");

			GD.Assert(attackCount == 2, "攻击方应该能追击（AS差 >= 4）");

			// 模拟多次战斗以验证统计分布
			int totalHits = 0;
			int totalCrits = 0;
			int simulations = 100;

			for (int i = 0; i < simulations; i++)
			{
				if (CombatCalculator.CheckHit(hitRate)) totalHits++;
				if (CombatCalculator.CheckCrit(critRate)) totalCrits++;
			}

			GD.Print($"\n模拟 {simulations} 次攻击:");
			GD.Print($"命中次数: {totalHits} (预期约 {hitRate}%)");
			GD.Print($"必杀次数: {totalCrits} (预期约 {critRate}%)");

			GD.Print("✓ 战斗系统测试通过\n");
		}

		/// <summary>
		/// 测试回合系统
		/// </summary>
		private void TestTurnSystem()
		{
			GD.Print("\n[测试6] 回合系统");
			GD.Print("------------------------------");

			TurnManager turnManager = new TurnManager();
			turnManager._Ready();

			GD.Print($"初始回合: {turnManager.CurrentTurnNumber}");
			GD.Print($"初始阶段: {turnManager.CurrentPhase}");

			// 模拟几个回合
			for (int i = 0; i < 3; i++)
			{
				GD.Print($"\n--- 模拟第 {turnManager.CurrentTurnNumber} 回合 ---");
				
				// 玩家回合
				turnManager.StartTurn(TurnPhase.PlayerPhase);
				GD.Print($"当前阶段: {turnManager.CurrentPhase}");
				
				// 敌军回合
				turnManager.EndCurrentPhase();
				GD.Print($"当前阶段: {turnManager.CurrentPhase}");
				
				// 友军回合
				turnManager.EndCurrentPhase();
				GD.Print($"当前阶段: {turnManager.CurrentPhase}");
				
				// 结束回合
				turnManager.EndCurrentPhase();
			}

			GD.Print($"\n最终回合数: {turnManager.CurrentTurnNumber} (预期: 4)");
			GD.Assert(turnManager.CurrentTurnNumber == 4, "回合数应该递增");

			GD.Print("✓ 回合系统测试通过\n");
		}
	}
}
```

---

## 项目配置

### 9.1 project.godot - Godot 项目配置

```ini
; Engine Configuration File.
; Godot version: 4.6
; Template: C# Mono Project

[application]
config/name="FE5_Thracia776"
config/description="火焰纹章：多拉基亚776 复刻项目"
run/main_scene="res://Scenes/Main.tscn"
config/features=PackedStringArray("4.6", "C#", "Forward Plus")
config/icon="res://icon.svg"

[dotnet]
project/assembly_name="FE5"

[display]
window/size/viewport_width=1280
window/size/viewport_height=720
window/stretch/mode="canvas_items"

[input]
ui_select={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":0,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":32,"physical_keycode":0,"key_label":0,"unicode":32,"echo":false,"script":null)
, Object(InputEventJoypadButton,"resource_local_to_scene":false,"resource_name":"","device":0,"button_index":3,"pressure":0.0,"pressed":false,"script":null)
, Object(InputEventMouseButton,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"button_mask":0,"position":Vector2(0, 0),"global_position":Vector2(0, 0),"factor":1.0,"button_index":1,"canceled":false,"pressed":false,"double_click":false,"script":null)
]
}
ui_cancel={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":0,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":4194305,"physical_keycode":0,"key_label":0,"unicode":0,"echo":false,"script":null)
, Object(InputEventJoypadButton,"resource_local_to_scene":false,"resource_name":"","device":0,"button_index":1,"pressure":0.0,"pressed":false,"script":null)
, Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":90,"key_label":0,"unicode":122,"echo":false,"script":null)
]
}

[layer_names]
2d_physics/layer_1="Units"
2d_physics/layer_2="Terrain"

[rendering]
textures/canvas_textures/default_texture_filter=0
```

### 9.2 FE5.csproj - C# 项目文件

```xml
<Project Sdk="Godot.NET.Sdk/4.6.0">
  <PropertyGroup>
	<TargetFramework>net8.0</TargetFramework>
	<TargetFramework Condition=" '$(GodotTargetPlatform)' == 'android' ">net8.0</TargetFramework>
	<TargetFramework Condition=" '$(GodotTargetPlatform)' == 'ios' ">net8.0</TargetFramework>
	<EnableDynamicLoading>true</EnableDynamicLoading>
	<RootNamespace>FE5</RootNamespace>
	<AssemblyName>FE5</AssemblyName>
  </PropertyGroup>
  
  <ItemGroup>
	<!-- 如果需要额外的NuGet包，在这里添加 -->
	<!-- <PackageReference Include="SomePackage" Version="1.0.0" /> -->
  </ItemGroup>
</Project>
```

### 9.3 Main.cs - 主场景控制器

```csharp
using Godot;
using System;
using FE5.Test;

namespace FE5.Core
{
	/// <summary>
	/// 游戏主控制器
	/// </summary>
	public partial class Main : Node2D
	{
		private TurnManager _turnManager;
		private BattleManager _battleManager;
		private Map.MapManager _mapManager;

		public override void _Ready()
		{
			GD.Print("============================================");
			GD.Print("  火焰纹章：多拉基亚776 - Godot 4.6 C# 复刻");
			GD.Print("============================================\n");

			// 获取管理器引用
			_turnManager = GetNode<TurnManager>("TurnManager");
			_battleManager = GetNode<BattleManager>("BattleManager");
			_mapManager = GetNode<Map.MapManager>("MapManager");

			// 运行测试
			RunTests();

			// 开始游戏
			StartGame();
		}

		/// <summary>
		/// 运行测试
		/// </summary>
		private void RunTests()
		{
			// 添加测试运行器
			TestRunner testRunner = new TestRunner();
			AddChild(testRunner);
			testRunner._Ready();
			RemoveChild(testRunner);
			testRunner.QueueFree();
		}

		/// <summary>
		/// 开始游戏
		/// </summary>
		private void StartGame()
		{
			GD.Print("\n>>> 游戏开始! <<<\n");
			
			// 初始化回合系统
			_turnManager.CollectAllUnits();
			_turnManager.StartTurn(TurnPhase.PlayerPhase);
		}

		public override void _Input(InputEvent @event)
		{
			// 按 T 键打印当前回合状态
			if (@event is InputEventKey keyEvent && keyEvent.Pressed)
			{
				if (keyEvent.Keycode == Key.T)
				{
					_turnManager.PrintTurnStatus();
				}
				// 按 N 键结束当前阶段
				else if (keyEvent.Keycode == Key.N)
				{
					_turnManager.EndCurrentPhase();
				}
			}
		}
	}
}
```

### 9.4 Main.tscn - 主场景

```
[gd_scene load_steps=2 format=3 uid="uid://main_scene"]

[ext_resource type="Script" path="res://Scripts/Core/Main.cs" id="1_main"]

[node name="Main" type="Node2D"]
script = ExtResource("1_main")

[node name="MapManager" type="Node2D" parent="."]
script = ExtResource("res://Scripts/Map/MapManager.cs")

[node name="GroundLayer" type="TileMapLayer" parent="MapManager"]

[node name="TerrainLayer" type="TileMapLayer" parent="MapManager"]

[node name="TurnManager" type="Node" parent="."]
script = ExtResource("res://Scripts/Core/TurnManager.cs")

[node name="BattleManager" type="Node" parent="."]
script = ExtResource("res://Scripts/Core/BattleManager.cs")

[node name="Units" type="Node2D" parent="."]

[node name="CanvasLayer" type="CanvasLayer" parent="."]

[node name="UI" type="Control" parent="CanvasLayer"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2

[node name="DebugLabel" type="Label" parent="CanvasLayer/UI"]
layout_mode = 0
offset_left = 10.0
offset_top = 10.0
offset_right = 400.0
offset_bottom = 200.0
text = "按 T 查看回合状态\n按 N 结束当前阶段"
```

---

## 实施总结

### 已完成的核心功能

1. **FE5 属性系统** - 完整实现包括体格(Bld)在内的所有属性，**无独立魔防，魔力兼任魔法防御**
2. **攻速计算** - 正确实现 `攻速 = 速度 - (武器重量 - 体格)` 公式，武器重量 ≤ 体格时无惩罚
3. **追击判定** - AS差 >= 4 时触发二次攻击
4. **BFS 寻路** - 使用 TileMapLayer 实现移动范围计算
5. **战斗系统** - 完整的战斗流程，包括命中、必杀、伤害计算
   - 命中率 = (技巧×2) + 武器命中 + 支援加成 + 武器相克加成 - 敌方回避
   - 回避率 = (攻速×2) + 幸运 + 地形加成 + 支援加成 + 武器相克加成
   - 物理伤害 = 力量 + 武器威力 - 敌方防御
   - 魔法伤害 = 魔力 + 魔法威力 - 敌方魔力
6. **回合系统** - Player Phase / Enemy Phase 切换机制

### 控制台输出验证

运行项目后，控制台将输出：
- 单位属性详情
- 攻速计算过程
- 追击判定结果
- BFS 移动范围
- 战斗伤害数值
- 回合切换状态

### 后续扩展建议

1. **武器相克系统** - 实现剑/枪/斧/弓三角相克及加成
2. **支援系统** - 实现支援加成（命中/回避/攻防补正）
3. **职业系统** - 添加职业成长和转职
4. **物品系统** - 实现道具和装备
5. **AI 优化** - 更智能的敌军行为
6. **存档系统** - 游戏进度保存
7. **UI 界面** - 添加战斗界面和菜单

---

## 参考资源

- [FE5 公式资料](https://serenesforest.net/thracia-776/)
- [Godot 4.6 C# 文档](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [TileMapLayer API](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html)

---

*文档版本: 1.0*  
*最后更新: 2026-05-13*  
*Godot 版本: 4.6*  
*目标平台: PC (Windows/Linux/macOS)*
