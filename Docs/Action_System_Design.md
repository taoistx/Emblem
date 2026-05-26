# Action 系统设计文档

## 1. 文档目标

Action 系统负责把“单位要做什么”转化为一次完整、合法、可结算、可表现的游戏行动。它位于 UI、AI 与规则系统之间，是战棋地图上所有单位命令的统一入口。

本文件用于定义：

- Action 系统的职责边界。
- 一次行动从选择到结算的生命周期。
- 移动、攻击、救出、捕获、偷窃、访问等命令的设计方式。
- Action 与 Map、Unit、Item、Combat、Chapter、UI、AI 的协作关系。
- 后续 C# 实现时建议的数据结构和接口。

## 2. Action 系统定位

```text
Player UI / Enemy AI
        |
        v
Action System
        |
        +--> Map       查询格子、移动、占位
        +--> Unit      查询和修改单位状态
        +--> Item      查询和修改背包、耐久、物品归属
        +--> Combat    生成战斗预测与战斗结果
        +--> Chapter   触发访问、撤退、胜败、事件
        +--> Core      通知单位行动结束、回合推进
        +--> UI        返回行动结果供表现层展示
```

Action 系统不直接处理玩家输入，也不播放动画。它只接收一个结构化的行动请求，判断是否合法，执行规则结算，并输出结构化结果。

## 3. 核心职责

Action 系统负责：

- 接收 UI 或 AI 发出的行动请求。
- 判断行动是否合法。
- 串联移动、战斗、物品、章节事件等多个规则模块。
- 修改游戏逻辑状态，例如单位坐标、HP、物品归属、状态标记。
- 生成行动结果，供 UI、动画、日志、测试使用。
- 决定单位行动后是否结束行动，或是否触发再行动。

Action 系统不负责：

- 不负责鼠标、键盘、手柄输入。
- 不负责绘制移动范围和菜单。
- 不负责战斗公式本身。
- 不负责敌军决策评分。
- 不负责具体动画时长和镜头表现。

## 4. 行动生命周期

一次行动建议分为 6 个阶段：

```text
1. Build Request      构建行动请求
2. Validate           合法性校验
3. Preview            生成预览，可选
4. Execute            执行逻辑结算
5. Emit Result        输出行动结果
6. Finalize Unit      处理待机、再行动、事件后续
```

### 4.1 Build Request：构建请求

UI 或 AI 不直接调用具体规则，而是构建一个 `ActionRequest`。

示例：

```text
ActionRequest
├── ActorUnitId
├── ActionType
├── TargetUnitId
├── TargetPosition
├── MovePath
├── ItemInstanceId
└── ExtraParameters
```

同一套请求结构可供玩家和 AI 共用。

### 4.2 Validate：合法性校验

校验阶段只回答“能不能做”，不修改游戏状态。

常见校验：

- 行动者是否存在。
- 行动者是否属于当前行动阵营。
- 行动者是否还未行动。
- 行动者是否处于可行动状态。
- 移动路径是否合法。
- 目标单位或目标格是否存在。
- 武器、道具、命令是否满足使用条件。
- 目标是否在射程、交互范围或访问范围内。

校验结果使用 `ActionValidationResult` 表达，避免只返回 `bool`。

```text
ActionValidationResult
├── IsValid
├── FailureReason
├── FailureCode
└── DebugDetails
```

### 4.3 Preview：生成预览

预览阶段用于 UI 或 AI 评估，不修改游戏状态。

示例：

- 攻击预览：伤害、命中、必杀、追击、反击。
- 移动预览：最终位置、消耗步数、路径。
- 捕获预览：捕获成功条件、战斗能力修正、可夺取物品。
- 偷窃预览：可偷物品列表。

预览必须尽量复用正式结算的计算入口，避免显示结果与实际结果不一致。

### 4.4 Execute：执行结算

执行阶段会修改游戏状态。

可能发生的变化：

- 单位位置改变。
- 单位 HP 改变。
- 单位死亡、撤退、被捕获。
- 物品耐久减少。
- 物品转移。
- 地图占位改变。
- 章节事件被触发。
- 单位被标记为已行动。

### 4.5 Emit Result：输出结果

所有执行结果都应通过结构化 `ActionResult` 返回。

```text
ActionResult
├── ActionId
├── ActionType
├── ActorUnitId
├── StartedPosition
├── EndedPosition
├── Success
├── EndsUnitTurn
├── TriggeredEvents
├── StateChanges
├── CombatResult
├── ItemChanges
├── UnitChanges
└── PresentationHints
```

`PresentationHints` 只提供表现建议，例如“播放攻击动画”“镜头移动到目标”，但不应该包含规则判断。

### 4.6 Finalize Unit：收尾

行动完成后，Action 系统需要决定：

- 是否标记单位已行动。
- 是否触发再行动。
- 是否触发章节胜败条件。
- 是否强制切换选择状态。
- 是否进入事件演出或对话。

FE5 中再行动星可能让单位行动后恢复可行动状态，因此“执行行动”和“结束单位行动”应该分开处理。

## 5. 行动类型总览

```text
UnitActionType
├── MoveOnly          仅移动
├── Wait              待机
├── Attack            攻击
├── Staff             使用杖
├── UseItem           使用道具
├── Trade             交换
├── Rescue            救出
├── Drop              放下
├── Transfer          交接被救单位
├── Capture           捕获
├── Release           释放俘虏
├── Steal             偷窃
├── Visit             访问
├── Talk              对话
├── OpenDoor          开门
├── OpenChest         开宝箱
├── Escape            撤退
├── Seize             制压
└── CustomChapter     章节自定义行动
```

初期不需要一次实现全部行动，但枚举设计应提前留好扩展空间。

## 6. 通用行动规则

### 6.1 行动者状态要求

单位通常必须满足：

- 存活。
- 未被捕获。
- 不处于无法行动状态。
- 当前回合属于自己的阵营。
- 本回合还未行动，或已触发再行动。

无法行动状态包括：

- 睡眠。
- 石化或禁动。
- 被救出。
- 被俘虏。
- 章节脚本锁定。

### 6.2 移动与行动关系

多数行动允许“先移动，再执行命令”。因此请求中应包含可选 `MovePath`。

执行顺序：

```text
Validate MovePath
Move Actor
Validate Command At New Position
Execute Command
Finalize
```

注意：如果命令校验失败，不应先移动单位。实际实现可在执行前完整校验“移动后命令是否合法”。

### 6.3 行动后待机

大多数行动会结束单位行动：

- 攻击。
- 使用杖。
- 使用道具。
- 救出。
- 放下。
- 捕获。
- 偷窃。
- 访问。
- 开门。
- 等待。

可能不结束行动的情况：

- 纯预览。
- 菜单取消。
- 部分脚本事件。
- 再行动触发后恢复行动权。

## 7. 具体行动设计

### 7.1 MoveOnly：仅移动

用于只移动但不立刻待机的中间状态，也可用于调试和 AI 模拟。

校验：

- 路径从单位当前位置开始。
- 每一步相邻。
- 总移动消耗不超过单位移动力。
- 不穿过不可通行地形。
- 不穿过不可穿越单位。
- 终点未被其它单位占据。

结果：

- 更新地图占位。
- 更新单位坐标。
- 通常不自动结束行动，除非请求明确指定。

### 7.2 Wait：待机

用于结束单位行动。

校验：

- 单位可行动。
- 如果包含移动路径，移动路径合法。

结果：

- 单位移动到终点。
- 标记已行动。
- 检查再行动。

### 7.3 Attack：攻击

攻击是 Action 与 Combat 的主要协作点。

校验：

- 行动者可行动。
- 目标单位存在且为敌对阵营。
- 选择的武器存在于行动者背包。
- 武器可装备且耐久大于 0。
- 目标在武器射程内。
- 如果包含移动路径，则按移动后的位置计算射程。

执行：

```text
Move Actor If Needed
CombatCalculator.BuildForecast
BattleManager.ResolveBattle
Apply Combat Result
Consume Durability
Apply Experience
Check Death
Finalize Unit Action
```

结果：

- `CombatResult`
- HP 变化。
- 死亡或撤退。
- 武器耐久变化。
- 经验与升级结果。
- 可能触发击杀事件、Boss 死亡事件。

注意：

- Combat 负责战斗公式和攻击序列。
- Action 负责把战斗结果应用到地图状态和章节状态。

### 7.4 Staff：使用杖

杖可以视为非普通攻击的目标行动。

校验：

- 单位可行动。
- 道具是杖。
- 单位武器等级满足要求。
- 目标类型正确：友军、敌军、空格、范围等。
- 目标在射程内。
- 耐久大于 0。

执行：

- 计算杖命中或必定成功。
- 应用效果：治疗、传送、睡眠、沉默、开锁等。
- 消耗耐久。
- 给予经验和武器经验。
- 触发事件。

设计建议：

- 杖效果不要全部写在 Action 内。
- 可用 `StaffEffectResolver` 或物品效果系统处理具体效果。

### 7.5 UseItem：使用道具

校验：

- 单位拥有该道具。
- 道具可使用。
- 使用目标合法。
- 当前章节或状态允许使用。

执行：

- 应用道具效果。
- 消耗使用次数或移除物品。
- 触发升级、回复、状态解除等结果。

常见道具：

- 药。
- 能力药。
- 转职道具。
- 钥匙。
- 特殊剧情道具。

### 7.6 Trade：交换

交换通常不结束行动，具体规则可按项目需要决定。FE 系列里交换常用于行动前调整物品。

校验：

- 目标是相邻友军。
- 双方都可交换。
- 交换后背包容量不超限。
- 物品没有被锁定。

执行：

- 调整双方背包。
- 返回物品变化结果。

设计建议：

- 交换本身可以不结束单位行动。
- 如果交换发生在移动后，是否允许继续行动需要明确。建议第一版只允许当前位置相邻交换，不与移动合并。

### 7.7 Rescue：救出

救出是 FE5 核心机制之一。

校验：

- 目标是相邻友军或可救单位。
- 行动者没有正在救出其它单位。
- 目标没有正在救出其它单位。
- 目标不是大型单位或不可救出单位。
- 行动者体格或规则条件允许救出目标。
- 目标状态允许被救出。

执行：

- 目标从地图占位移除。
- 目标状态变为 `Rescued`。
- 行动者记录 `CarriedUnitId`。
- 行动者可能受到属性修正。
- 行动者通常结束行动。

FE5 重点：

- 救出会影响行动者战斗能力，通常表现为部分属性下降。
- 被救单位不能行动、不能被直接攻击。
- 救出与捕获共用“携带单位”的部分模型，但语义不同。

### 7.8 Drop：放下

校验：

- 行动者正在救出或携带单位。
- 目标格相邻。
- 目标格可通行且未被占据。
- 被放下单位可以站在该地形上。

执行：

- 被携带单位恢复地图占位。
- 清除 `Rescued` 或 `Captured` 携带状态。
- 设置被放下单位坐标。
- 行动者通常结束行动。

注意：

- 放下俘虏和放下友军可能触发不同后续逻辑。
- 如果俘虏是敌人，放下后是否恢复敌对单位，需要单独规则。

### 7.9 Transfer：交接

交接用于把被救单位转交给相邻友军。

校验：

- 行动者正在携带单位。
- 目标友军相邻。
- 目标友军没有携带单位。
- 目标友军满足携带条件。

执行：

- 行动者清除 `CarriedUnitId`。
- 目标友军设置 `CarriedUnitId`。
- 被携带单位保持离地图状态。
- 是否结束行动按项目规则决定。建议第一版设为结束行动，降低复杂度。

### 7.10 Capture：捕获

捕获是 FE5 的关键特色，应作为独立行动而不是攻击的一个布尔参数。

校验：

- 目标是相邻或武器射程内敌军。
- 行动者没有携带单位。
- 目标可被捕获。
- 行动者满足捕获体格条件。
- 使用武器可用于捕获。
- 目标不是特殊免疫单位，例如部分 Boss。

执行：

```text
Move Actor If Needed
Apply Capture Combat Modifier
Resolve Battle
If Target Defeated And CaptureAllowed:
    Set Target Captured
    Remove Target From Map
    Set Actor CarriedUnitId
Else:
    Apply Normal Battle Result
Finalize
```

FE5 重点：

- 捕获时行动者战斗能力会下降，常见处理是属性或攻速减半。
- 捕获成功后敌人不死亡，而是进入被携带状态。
- 捕获敌人后可以夺取其物品。
- 敌方也可能捕获我方单位。

设计建议：

- 使用 `CaptureService` 处理捕获合法性和成功后的状态转换。
- Combat 只需要知道本次战斗使用了捕获修正。
- Action 负责把“击倒目标”解释为“捕获目标”。

### 7.11 Release：释放俘虏

用于释放被捕获敌人或处理剧情释放。

校验：

- 行动者携带的是俘虏。
- 目标格合法。

执行：

- 清除携带关系。
- 目标单位恢复或离开地图，取决于规则。

设计选项：

- 第一版可暂不做普通释放，只做“夺取物品后放下”。
- 后续章节事件可直接移除俘虏并记录状态。

### 7.12 Steal：偷窃

偷窃是 FE5 的重要战术系统。

校验：

- 行动者拥有偷窃能力。
- 目标是相邻敌人。
- 行动者速度满足要求。
- 行动者体格可以偷取目标物品。
- 目标物品允许被偷。
- 行动者背包有空位。

执行：

- 从目标背包移除物品。
- 加入行动者背包。
- 记录物品变化。
- 行动者结束行动。

FE5 重点：

- 不是所有物品都能偷。
- 武器和道具是否可偷，需要按具体规则定义。
- 体格是偷窃判定的重要参数。

### 7.13 Visit：访问

用于村庄、房屋、特殊地点。

校验：

- 单位位于可访问格，或与目标格相邻。
- 访问点尚未被访问。
- 单位阵营或角色满足条件。

执行：

- 触发章节事件。
- 给予物品、金钱、情报或角色。
- 标记访问点已访问。
- 行动者结束行动。

### 7.14 Talk：对话

校验：

- 对话双方满足章节配置。
- 双方距离满足要求，通常相邻。
- 对话尚未发生或允许重复。

执行：

- 触发对话事件。
- 可能改变阵营、给予物品、开启后续事件。
- 行动者是否结束行动由事件定义。建议默认结束。

### 7.15 OpenDoor / OpenChest：开门与宝箱

校验：

- 目标格是门或宝箱。
- 单位拥有钥匙、开锁杖或盗贼能力。
- 距离合法。

执行：

- 消耗钥匙或耐久。
- 修改地图交互对象状态。
- 宝箱给予物品。
- 触发章节事件。
- 行动者结束行动。

### 7.16 Escape：撤退

FE5 中撤退章节很重要，应独立设计。

校验：

- 单位位于撤退点。
- 当前章节允许撤退。
- 单位没有被强制禁止撤退。

执行：

- 单位离开地图。
- 记录单位已撤退。
- 如果主角撤退，触发章节特殊结算。
- 检查胜败条件。

FE5 重点：

- 主角先撤退可能导致未撤退友军被俘。
- 撤退顺序本身就是章节策略的一部分。

### 7.17 Seize：制压

校验：

- 单位是允许制压的角色，通常是主角。
- 单位位于制压点。
- 章节目标是制压。

执行：

- 触发章节胜利。
- 进入章节结束流程。

## 8. 数据结构建议

### 8.1 ActionRequest

```csharp
public sealed class ActionRequest
{
    public string ActorUnitId { get; init; }
    public UnitActionType ActionType { get; init; }
    public string TargetUnitId { get; init; }
    public Vector2I? TargetPosition { get; init; }
    public IReadOnlyList<Vector2I> MovePath { get; init; }
    public string ItemInstanceId { get; init; }
    public Dictionary<string, Variant> ExtraParameters { get; init; }
}
```

说明：

- `TargetUnitId` 与 `TargetPosition` 可根据行动类型选择使用。
- `MovePath` 为空时表示原地行动。
- `ExtraParameters` 用于章节特殊行动，不应滥用到通用规则里。

### 8.2 ActionResult

```csharp
public sealed class ActionResult
{
    public string ActionId { get; init; }
    public UnitActionType ActionType { get; init; }
    public string ActorUnitId { get; init; }
    public Vector2I StartedPosition { get; init; }
    public Vector2I EndedPosition { get; init; }
    public bool Success { get; init; }
    public bool EndsUnitTurn { get; init; }
    public IReadOnlyList<StateChange> StateChanges { get; init; }
    public BattleResult CombatResult { get; init; }
    public IReadOnlyList<ItemChange> ItemChanges { get; init; }
    public IReadOnlyList<ChapterEventResult> TriggeredEvents { get; init; }
    public PresentationHints PresentationHints { get; init; }
}
```

### 8.3 StateChange

```csharp
public enum StateChangeType
{
    UnitMoved,
    UnitHpChanged,
    UnitDied,
    UnitCaptured,
    UnitRescued,
    UnitDropped,
    UnitStatusAdded,
    UnitStatusRemoved,
    ItemAdded,
    ItemRemoved,
    ItemDurabilityChanged,
    TileStateChanged,
    ChapterFlagChanged
}
```

`StateChange` 主要用于测试、日志、动画播放和未来的回放系统。

### 8.4 ActionValidator

建议将校验拆成可组合的规则。

```text
ActionValidator
├── ValidateActorCanAct
├── ValidateMovePath
├── ValidateTarget
├── ValidateRange
├── ValidateItem
├── ValidateInventorySpace
├── ValidateChapterRule
└── ValidateActionSpecificRule
```

这样捕获、偷窃、救出等复杂行动可以复用通用校验。

## 9. 服务类建议

```text
ActionManager
├── MoveService
├── AttackActionHandler
├── StaffActionHandler
├── ItemActionHandler
├── TradeService
├── RescueService
├── CaptureService
├── StealService
├── VisitService
├── EscapeService
└── ActionFinalizer
```

### 9.1 ActionManager

统一入口：

- `Validate(ActionRequest request)`
- `Preview(ActionRequest request)`
- `Execute(ActionRequest request)`

### 9.2 ActionHandler

每类行动可由独立 Handler 处理。

```text
IActionHandler
├── ActionType
├── Validate(request, context)
├── Preview(request, context)
└── Execute(request, context)
```

这样新增章节特殊行动时不需要修改一个巨大的 `switch`。

### 9.3 ActionContext

Action 执行时需要访问多个系统，可通过上下文聚合依赖。

```text
ActionContext
├── TurnManager
├── MapManager
├── UnitRepository
├── ItemRepository
├── CombatCalculator
├── BattleManager
├── ChapterController
└── RandomProvider
```

## 10. 与其它模块的协作

### 10.1 与 UI 协作

UI 负责：

- 显示可选命令。
- 选择目标。
- 展示预览。
- 播放行动结果。

Action 负责：

- 返回有哪些命令可用。
- 校验请求。
- 生成预览。
- 执行请求。

建议 UI 不直接读写单位和物品状态，而是通过 Action 和查询接口获得结果。

### 10.2 与 AI 协作

AI 可以使用与 UI 相同的 Action 接口。

AI 流程：

```text
Generate Candidate Actions
Preview Each Candidate
Score Candidate
Execute Best Action
```

这样 AI 不需要重新实现攻击、捕获、偷窃等规则。

### 10.3 与 Combat 协作

Combat 负责：

- 战斗预测。
- 攻击顺序。
- 命中、伤害、必杀、追击。
- 经验计算的战斗输入。

Action 负责：

- 战斗前移动。
- 战斗是否合法。
- 应用战斗结果。
- 处理死亡、捕获、事件。

### 10.4 与 Chapter 协作

Action 执行后需要通知 Chapter：

- 单位死亡。
- Boss 死亡。
- 单位进入区域。
- 访问村庄。
- 打开宝箱。
- 撤退或制压。

Chapter 可以返回事件结果，例如对话、增援、胜利、失败。

### 10.5 与 Save 协作

Action 不直接写存档，但它输出的 `StateChanges` 可以帮助 Save 或 Replay 系统记录变化。

## 11. 事务与回滚

早期可以不实现完整回滚，但应避免半途失败导致状态损坏。

建议：

- 执行前做完整校验。
- 执行中每一步集中记录 `StateChange`。
- 复杂行动先在临时结果里计算，再一次性应用。
- 后续若需要悔棋、回放、网络同步，可基于 `StateChange` 增强。

对于第一版，重点保证：

- 命令失败时不修改状态。
- 移动后攻击非法时不发生移动。
- 战斗结算中死亡、耐久、经验、事件按固定顺序处理。

## 12. 推荐结算顺序

攻击类行动：

```text
1. Validate full request
2. Apply movement
3. Build combat forecast
4. Resolve battle
5. Apply HP changes
6. Apply death/capture/rescue state
7. Apply durability changes
8. Apply experience and level up
9. Trigger chapter events
10. Finalize actor action
11. Return ActionResult
```

非攻击交互：

```text
1. Validate full request
2. Apply movement if allowed
3. Apply interaction result
4. Trigger chapter events
5. Finalize actor action
6. Return ActionResult
```

## 13. 初期实现范围

第一阶段建议只实现：

- `Wait`
- `MoveOnly`
- `Attack`
- `UseItem`
- `Trade`

第二阶段加入：

- `Rescue`
- `Drop`
- `Transfer`
- `Staff`

第三阶段加入 FE5 特色：

- `Capture`
- `Steal`
- `Escape`
- `Seize`

第四阶段补充章节交互：

- `Visit`
- `Talk`
- `OpenDoor`
- `OpenChest`
- `CustomChapter`

## 14. 测试用例建议

基础行动：

- 单位移动到合法格子成功。
- 单位不能穿过敌军。
- 单位不能移动到已占据格。
- 待机会标记单位已行动。

攻击：

- 移动后射程内攻击成功。
- 移动后射程外攻击失败且不移动。
- 武器耐久为 0 时不能攻击。
- 击杀目标后目标从地图占位移除。

救出：

- 可救友军成功离开地图占位。
- 已携带单位时不能再救出。
- 不满足体格条件时不能救出。

捕获：

- 捕获行动使用战斗修正。
- 捕获成功后目标不死亡，而是进入俘虏状态。
- 捕获失败时按普通战斗伤害结算。

偷窃：

- 速度或体格不足时失败。
- 背包满时失败。
- 成功后物品归属改变。

章节行动：

- 主角在制压点可以制压。
- 非主角不能制压。
- 单位在撤退点可以撤退。
- 访问村庄后不能重复访问。

## 15. 待确认设计问题

以下问题需要在具体实现前确认：

- 交换是否结束行动。
- 移动后是否允许交换。
- 交接是否结束行动。
- 捕获时具体采用哪种属性修正规则。
- 敌军捕获我方单位后，章节结束时如何处理俘虏。
- 释放俘虏是普通行动，还是只作为章节脚本行为。
- 杖命中是否完整复刻 FE5，还是第一版先做必定成功。
- 再行动触发是在所有行动后统一检查，还是只对特定行动检查。

