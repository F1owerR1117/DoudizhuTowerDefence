# 即时斗地主塔防 (DoudizhuTower)

一款 2V1 即时对冲塔防游戏，基于扑克牌型与即时经济驱动的即时战略乱斗。

## 项目信息

- **引擎**: Unity 2023.2.20f1c1
- **语言**: C#
- **分辨率**: 1920 x 1080
- **架构文档**: `ARCHITECTURE.md`（v9.3）
- **网络方案**: Photon Fusion 2

---
演示视频： https://pan.baidu.com/s/1iIvBPrtIaZvq0sbffZoLsQ?pwd=ddzt
## 快速开始

### 1. 打开项目

用 Unity 2023.2 打开项目文件夹。

### 2. 联机配置

项目使用 Photon Fusion 2 进行联机。需要自行注册 App ID：

1. 复制 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset.example`
2. 重命名为 `PhotonServerSettings.asset`
3. 将 `AppIdRealtime` 替换为你的 Photon App ID

### 3. 场景列表

| 场景 | 路径 | 说明 |
|---|---|---|
| MainMenu | `Assets/Scenes/MainMenu.unity` | 主菜单 |
| LevelSelect | `Assets/Scenes/LevelSelect.unity` | 关卡选择 |
| Level_1 ~ Level_5 | `Assets/Scenes/Level_*.unity` | 5 个关卡场景 |
| Bidding | `Assets/Scenes/Bidding.unity` | 叫分期 |
| OnlineLobby | `Assets/Scenes/OnlineLobby.unity` | 联机大厅 |
| DoudizhuTower_Game | `Assets/Scenes/DoudizhuTower_Game.unity` | 游戏主场景 |
| UI_Scene | `Assets/Scenes/UI_Scene.unity` | UI 场景（自动加载） |

### 4. 游戏流程

```
主菜单
├── 单人模式 → 关卡选择（5 关）→ 叫分场景 → 游戏场景
├── 对战模式 → 联机大厅 → 叫分场景 → 游戏场景
├── 图鉴（已实现，6 分类/搜索/解锁/进度）
└── 设置
```

---

## 核心系统

### 战斗系统

- **Combat Gate System**: `IsValidCombatTarget()` 统一门禁，所有战斗判断通过单一入口
- **AttackTimeline**: 时间驱动攻击帧，替代 Animation Event（确定性 + 联机安全）
- **兵种类**: `CardUnit`（4 个 partial 文件：Core/Combat/Movement/Animation）
- **被动技能**: `UnitPassives`（17 种通用被动，Inspector 勾选启用）
- **英雄被动**: 5 种模块化组件（剑圣/铁卫/神射/术士/灵骑），挂载到英雄预制体
- **对象池**: `UnitFactory` + `VFXManager`
- **伤害结算**: `DamageQueue`（帧末批量结算，消除 Update 执行顺序影响）
- **受伤流水线**: 真实伤害→屏障→盾墙→减免→吸收→分担→撕裂→扣血（严格串行）
- **统一 Buff**: 命名 Buff（同名覆盖，异名乘算）+ 从基础值 RecalculateStats

### BOSS 系统

- **激活控制**: `BossController` 管理生命周期（OnStart/OnTimer/OnBuildingDestroyed）
- **未激活隐藏**: Renderer/Collider/HealthBar/BossSkillSystem 全部禁用
- **门禁查询**: `BossController.IsActive` 供 `GetEnemiesFor` 过滤
- **技能系统**: `BossSkillSystem`（6 种效果 + 门禁过滤 + 特效缩放）
- **演出系统**: `BattlePresentationManager` 并行调度镜头/对话/广播/特效

### 领域系统

- **要不起领域**: 地主开启，封印对方手牌中不能管上当前牌型的牌
- **反制护盾**: 农民反击，封印地主手牌
- **炸弹破封**: 炸弹/王炸可直接破封领域
- **封印规则引擎**: `SealRuleEngine` 判定手中可反制牌型

### 经济系统

- 初始金币: 50
- 回金速度: 农民 5/秒，地主 7/秒，每分钟 +1/秒
- 费用公式: `C_n = 10 × 1.17^(n-3) × M_type`
- 骤死期: 回金速度 ×2

### 对话系统

- **DialogueBox**: 打字机效果 + 角色立绘展示 + 全区域点击继续
- **DialogueData**: ScriptableObject 定义对话序列（每行可独立配置立绘尺寸）
- **触发时机**: 关卡开始（`enterDialogue`）、关卡胜利（`victoryDialogue`）、手动触发
- **交互方式**: 点击任意位置 / 空格键 / 回车键 继续
- **与 BOSS 气泡独立**: BossDialogueBubble 用于战斗中气泡，DialogueBox 用于剧情对话

### 联机系统

- **Master 权威架构**: Master 计算，Client 纯投影
- **Event + Snapshot + Tick 三层模型**: 确定性同步（已冻结）
- **真相源收敛**: Deck/Hand/Economy/Buff/Stun/Knockback/HP/Target/Position 9 个系统
- **Client 战斗表现层**: UNIT_ATTACK/UNIT_HIT/UNIT_STUN/UNIT_KNOCKBACK 事件驱动
- **音效双轨**: Master 本地事件 + Client 网络事件
- **断线转 AI**: 保留断线玩家实际金币和剩余手牌
- **SimulatesCombat 门控**: Client 禁止 OnUpdate/TakeDamage/Die，只做视觉行军

---

## 目录结构

```
Assets/
├── Scripts/
│   ├── Config/              # ScriptableObject 配置表
│   │   ├── BiddingConfig.cs       # 叫分配置
│   │   ├── EconomyConfig.cs       # 经济曲线配置
│   │   ├── LevelConfig.cs         # 关卡配置
│   │   ├── UnitStatsConfig.cs     # 兵种数值汇总（CSV 管线中间层）
│   │   ├── CodexEntry.cs          # 图鉴条目
│   │   └── CodexDatabase.cs       # 图鉴数据库
│   ├── Core/                # 纯逻辑层（零 Unity 依赖）
│   │   ├── Battle/          # SoldierStats + Lane/Identity/DamageType 枚举
│   │   ├── Card/            # Card, CardDeck, CardHand, CardTypeDetector (13种牌型)
│   │   ├── Economy/         # EconomySystem, CardCostCalculator
│   │   └── Lifecycle/       # IRuntimeReady 接口
│   ├── Gameplay/            # 运行时管理层
│   │   ├── Battle/          # BattleManager, BossController, DomainSystem, SpawnPool, DamageQueue
│   │   ├── Entities/        # CardUnit, UnitPassives, Projectile, BossSkillSystem
│   │   │   ├── Passives/    # 英雄被动技能组件
│   │   │   │   ├── BlademasterPassive.cs    # 剑圣（攻击概率额外伤害）
│   │   │   │   ├── GuardianPassive.cs       # 铁卫（嘲讽源+伤害减免）
│   │   │   │   ├── SharpshooterPassive.cs   # 神射（优先攻击低血量）
│   │   │   │   ├── SpiritRiderPassive.cs    # 灵骑（友军光环）
│   │   │   │   └── WarlockPassive.cs        # 术士（溅射范围伤害）
│   │   │   ├── HeroUnitConfig.cs            # 英雄配置组件（挂载到预制体）
│   │   │   └── HeroPassiveBase.cs           # 英雄被动基类
│   │   ├── Network/         # FusionService, NetworkManager, NetworkFacade
│   │   ├── Fusion/          # FusionGameManager, CombatSystem, PassiveSystem, IntentBuffer
│   │   ├── Presentation/    # BattlePresentationManager, CameraDirector, BossDialogueBubble
│   │   └── Systems/         # GameBootstrapper, GameStateMachine, SaveSystem, AudioManager
│   └── UI/                  # 界面层
│       ├── Battlefield/     # DomainUIController, LaunchTubeUI, TempSlotUI
│       ├── Bidding/         # BiddingManager, NetworkBiddingManager
│       ├── Codex/           # CodexUIController（图鉴系统）
│       ├── Dialogue/        # DialogueBox, DialogueData, DialogueTrigger（对话系统）
│       ├── Hand/            # HandArea, CardWidget, SelectionValidator
│       ├── LevelSelect/     # LevelSelectController, LevelCard
│       ├── Online/          # OnlineLobbyController
│       └── Panels/          # UnitInfoPanel, PauseMenu, VictoryPanel
├── Scenes/                  # 场景文件
├── Resources/               # ScriptableObject 资产
└── Prefabs/                 # 预制体
    └── Army/ArmyPrefabs/    # 兵种预制体
        ├── OneRank/         # 基础兵种（13个：3~2点数）
        ├── Bomb/            # 炸弹兵种
        ├── Boss/            # BOSS 预制体
        ├── Bait/            # 诱饵兵种
        ├── Cavalry/         # 骑兵兵种
        ├── Tank/            # 坦克兵种
        ├── Drone/           # 无人机兵种
        ├── ConsecutivePair/ # 连对兵种
        └── Bomber/          # 飞机兵种
```

---

## 关键脚本速查

| 脚本 | 职责 |
|---|---|
| `BattleManager` | 战场主循环 + `IsValidCombatTarget` 统一门禁 + 胜负判定 + 12 种牌型生成 |
| `CardUnit` | 兵种基类（属性/战斗/移动/动画/统一 Buff） |
| `CardUnit.Combat` | AttackTimeline + ExecuteHit + OnAttackHitFrame + 受伤流水线 |
| `UnitPassives` | 17 种通用被动技能（嘲讽/点杀/人海/冲锋/光环/盾墙/护盾/减速/眩晕/撕裂/震波/燃烧/溅射/死爆/骑兵追击/召唤师/快速连击） |
| `HeroUnitConfig` | 英雄配置组件（觉醒倍率 + 被动技能引用） |
| `HeroPassiveBase` | 英雄被动基类（剑圣/铁卫/神射/术士/灵骑） |
| `BossController` | BOSS 生命周期 + IsActive 门禁 |
| `BossSkillSystem` | BOSS 技能（6 种效果 + 门禁过滤 + 特效缩放） |
| `DomainSystem` | 要不起领域 + 反制护盾状态机 + 炸弹破封 |
| `FusionGameManager` | Fusion 联机游戏管理器（Tick 状态机 + Host 权威同步） |
| `FusionService` | Fusion 网络服务（Runner 生命周期 + 房间管理） |
| `GameBootstrapper` | 自底向上 12 步初始化管线 |
| `DamageQueue` | 帧末批量伤害结算（消除 Update 执行顺序影响） |
| `BuildingAI` | 建筑 AI（每 4s 判定出牌 + 领域决策 + 暂存槽取牌） |
| `DialogueBox` | 对话框 UI（打字机效果 + 立绘 + 全区域点击继续） |
| `DialogueData` | 对话数据 ScriptableObject（定义对话序列） |

---

## 架构原则

1. **四层单向依赖**: UI → Gameplay → Core → Config，Core 禁止 Unity 依赖
2. **战斗系统 = 唯一真相**: `IsValidCombatTarget()` 是所有战斗判断的唯一入口
3. **攻击帧 = 时间驱动**: AttackTimeline 替代 Animation Event（确定性）
4. **控制效果 = 唯一打断源**: 眩晕/死亡/强制控制可打断攻击，目标死亡不打断
5. **BOSS 未激活 = 不存在**: Renderer/Collider/HealthBar/BossSkillSystem 全部禁用
6. **门禁统一**: TryAttack/OnAttackHitFrame/BossSkillSystem/GetEnemiesFor 均经过门禁
7. **网络三层模型**: Event（增量事实）+ Snapshot（权威世界）+ Tick（确定性核心）
8. **Master 唯一写状态**: Client 纯投影层，禁止修改任何游戏状态

---

## 已知待实现功能

| 功能 | 优先级 | 状态 |
|---|---|---|
| 商店系统 | P1 | 按钮已预留，需实现 ShopItemConfig/ShopDatabase/ShopManager/ShopUIController |
| 攻击特效 | P1 | 待实现（UnitVFX 缺少 PlayAttackVFX 方法） |
| 技能特效 | P1 | 待实现 |
| 索敌可视化标记 | P2 | 待实现 |
| 图鉴内容化 | P2 | 待实现（V1 框架已完成） |
| 新 Boss/新牌型/新建筑/新关卡 | P3 | 待实现 |

### 联机模式待修复

| 问题 | 优先级 | 说明 |
|---|---|---|
| ARCH-016: Client 手牌 UI 不同步 | P0 | HandArea 读本地引用，不从 WorldState 同步 |
| ARCH-017: BattleManager→Fusion 状态机未桥接 | P0 | 游戏无法正常结束 |
| ARCH-021: 联机模式游戏场景运行不完整 | P0 | 兵种生成/出牌/经济/战斗核心逻辑未正常运行 |
| ARCH-018: Client 经济/领域 UI 未同步 | P1 | 金币/领域显示错误 |

---

## 牌型与兵种映射

| 牌型 | 兵种类型 | SpawnPool 分组 |
|---|---|---|
| 单张/对子/三条 | 基础兵种（按点数 3~2） | `_rankPrefabs` |
| 三带一 | 主体 3 个 + 诱饵 1 个 | `_baitPrefabs` |
| 三带二 | 主体 3 个 + 骑兵 2 个 | `_cavalryPrefabs` |
| 顺子 5+ | 每张牌一个兵 + 链式加速 | `_rankPrefabs` |
| 连对 | 连对预制体 | `_consecutivePairPrefabs` |
| 炸弹 | 炸弹预制体 | `_bombPrefabs` |
| 四带二 | 坦克 + 无人机 | `_tankPrefabs` + `_dronePrefabs` |
| 飞机 | 轰炸机 + 地毯轰炸 | `_bomberPrefabs` |
| 王炸 | 英雄 5 选 1（预制体自包含） | HeroUnitConfig |

---

## 最近更新

### 2026-07-26

- **对话系统实现**: 新增 DialogueBox + DialogueData + DialogueTrigger
  - 打字机效果（可配置速度）
  - 角色立绘展示（每行可独立配置尺寸）
  - 全区域点击继续 + 键盘快捷键
  - LevelConfig 关联 enterDialogue / victoryDialogue
- **ARCHITECTURE.md v9.3**: 新增 §29 对话系统规范

### 2026-07-24

- **英雄配置重构**: 删除旧 HeroConfig.cs/HeroType.cs，改为预制体自包含模式（HeroUnitConfig）
- **英雄被动技能组件化**: 新增 HeroPassiveBase 基类 + 5 种被动组件
  - BlademasterPassive（剑圣：攻击概率额外伤害）
  - GuardianPassive（铁卫：嘲讽源 + 伤害减免）
  - SharpshooterPassive（神射：优先攻击低血量）
  - WarlockPassive（术士：溅射范围伤害）
  - SpiritRiderPassive（灵骑：友军光环）
- **BattleManager.Heroes 简化**: 移除硬编码英雄属性，使用 HeroUnitConfig 组件
- **ConfigImportExport 更新**: 移除 HeroConfig 导入导出

### 2026-07-22

- **兵种预制体目录重组**: 新增诱饵(Bait)、骑兵(Cavalry)、坦克、无人机、连对、飞机兵种预制体目录
- **PlaneTest 预制体移动**: 从根目录移动到飞机目录
- **关卡配置调整**: Level_03 配置更新，Level_1/3/4 场景调整
- **构建日志抑制器**: BuildLogSuppressor 脚本（编译时日志抑制）

### 2026-07-17

- **Fusion Runner 生命周期修复**: 移除 `Destroy(_runner)`，改为 `Shutdown()` 复用 Runner
- **叫分轮次校验**: `SubmitBid`/`ApplyBid`/`OnBid` 添加 `CurrentBidTurn` 校验
- **Slot 分配修复**: Client 回退逻辑改为匹配 `SlotXPlayerRef`
- **BuildingAI 联机模式保留**: `GameBootstrapper.Awake()` 联机模式跳过禁用
- **ARCHITECTURE.md v9.2**

### 2026-06-17

- **Combat Gate System**: `IsValidCombatTarget()` 统一门禁，三层架构
- **AttackTimeline**: 时间驱动攻击帧替代 Animation Event
- **ARCHITECTURE.md v7.4**
