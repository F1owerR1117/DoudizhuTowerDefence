# DoudizhuTower — 架构落地规范 v9.4

> 本文档是《即时斗地主塔防》的编码宪法，**必须与代码实际状态保持一致**。
> 实现核对日期：2026-11-10。标有“目标/历史记录”的章节属于设计约束或旧排查记录，不代表当前运行时已实现或通过验收。

---

## 目录

### 参考速查

| 章节 | 说明 |
|:---|:---|
| [术语对照表](#术语对照表) | 类名↔概念↔所在层↔说明 |
| [实施状态总览](#实施状态总览) | P0/P1/P2 功能完成度 |
| [费用与牌值常量参考](#费用与牌值常量参考) | 点数映射/费用公式/牌型系数 |
| [附录 A：Prefab 结构清单](#附录-aprefab-结构清单) | Prefabs/ 目录树 |

### 核心架构

| # | 章节 |
|:---|:---|
| 1 | [核心架构原则](#1-核心架构原则) |
| 2 | [目录结构与职责边界](#2-目录结构与职责边界) |
| 3 | [跨层焊接协议：观察者模式](#3-跨层焊接协议观察者模式) |

### 系统设计

| # | 章节 |
|:---|:---|
| 4 | [工业级初始化装配套管线](#4-工业级初始化装配套管线) |
| 6 | [异步计时器队列规范](#6-异步计时器队列规范) |
| 8 | [牌型检测器：Core 层最高优先级模块](#8-牌型检测器core-层最高优先级模块) |
| 9 | [兵种实体规范](#9-兵种实体规范) |
| 10 | [AI 对手系统](#10-ai-对手系统) |
| 17 | [伤害批量结算系统（DamageQueue）](#17-伤害批量结算系统damagequeue) |
| 18 | [传送飞筒系统](#18-传送飞筒系统) |
| 19 | [要不起领域系统（DomainSystem）](#19-要不起领域系统domainsystem) |
| 20 | [存档系统（SaveSystem）](#20-存档系统savesystem) |
| 21 | [叫分期系统](#21-叫分期系统biddingmanager--networkbiddingmanager--biddingconfig) |
| 23 | [BOSS 系统（BossController + BuildingAI）](#23-boss-系统bosscontroller--buildingai) |
| 28 | [Combat Gate System（战斗统一门禁）](#28-combat-gate-system战斗统一门禁) |
| 29 | [对话系统（DialogueBox）](#29-对话系统dialoguebox) |

### 视觉与交互

| # | 章节 |
|:---|:---|
| 5 | [坐标系约定（可编辑地图）](#5-坐标系约定可编辑地图) |
| 5a | [2.5D 沙盘视觉规范 v2.2](#5a-25d-沙盘视觉规范-v22) |
| 11 | [UI 层规范](#11-ui-层规范) |
| 14 | [动画状态系统](#14-动画状态系统) |
| 16 | [音频优先级管理系统](#16-音频优先级管理系统) |

### 联机与同步

| # | 章节 |
|:---|:---|
| 13 | [网络联机系统](#13-网络联机系统) |
| 22 | [跨场景数据传递（GameSession — 已废弃降级）](#22-跨场景数据传递gamesession) |
| 25 | [Event+Snapshot+Tick 三层确定性模型 v2.0](#25-event-snapshot-tick-三层确定性模型-v20final-lock) |
| 26 | [Truth Source Convergence（真相源收敛）](#26-truth-source-convergence真相源收敛) |
| 27 | [Client 战斗表现层（Event-Driven Presentation）](#27-战斗表现层event-driven-presentation) |

### 规范与债务

| # | 章节 |
|:---|:---|
| 7 | [致命地雷检查表](#7-致命地雷检查表) |
| 12 | [Config 配置表规范](#12-config-配置表规范) |
| 15 | [性能约束（联机防御编码协议）](#15-性能约束联机防御编码协议) |
| 24 | [架构债务登记](#24-架构债务登记) |

---

## 术语对照表

| 类名 | 别名/概念 | 所在层 | 说明 |
|:---|:---|:---|:---|
| `Card` (struct) | 卡牌 | Core/Card | 值对象，含 Rank + Suit |
| `CardRank` (enum) | 点数 | Core/Card | 3~2, Joker |
| `CardDeck` | 牌堆 | Core/Card | 54张 + 弃牌堆 + 洗牌 |
| `CardHand` | 手牌容器 | Core/Card | 纯数据容器，无 UI。`NotifyHandModified()` 供网络同步直接操作列表后触发 `OnHandChanged` |
| `HandArea` | 手牌 UI | UI/Hand | 持有 CardHand 引用的 MonoBehaviour |
| `CardTypeDetector` | 牌型检测器 | Core/Card | 核心算法，零 Unity 依赖 |
| `EconomySystem` | 经济系统(逻辑) | Core/Economy | 纯 C#，零 MonoBehaviour |
| `EconomyManager` | 经济系统(焊接) | Gameplay/Systems | 桥接 Core → UI 事件 + 骤死期双倍回金 |
| `CardUnit` | 兵种/建筑实体 | Gameplay/Entities | MonoBehaviour 基类，同时实现 IBuildingTarget。`_isBuilding=true` 时为建筑。`SimulatesCombat` 控制是否参与战斗模拟（Master=true, Client=false） |
| `UnitPassives` | 兵种被动 | Gameplay/Entities | 17 种通用被动（含召唤师），Inspector 勾选启用。溅射以 ClosestPoint 为圆心，召唤物继承召唤师 FollowPath |
| `BurnZone` | 燃烧区域 | Gameplay/Entities | UnitPassives 内嵌类（`public class BurnZone : MonoBehaviour`），UnitPassives 和 BattleManager 共用 |
| `BattleManager` | 战场管理器 | Gameplay/Battle | 主循环 + 牌型生成 + 全局唯一 UnitId 分配（`_globalUnitId` + `Dictionary<int, CardUnit>` O(1) 查找）+ `TriggerDefeat()` internal |
| `SpawnPool` | 出兵池 | Gameplay/Battle | 每基地独立预制体映射（8 组 ×13 槽，含 Tooltip 注释） |
| `BuildingAI` | 建筑 AI | Gameplay/Battle | 挂载到建筑/BOSS 上独立运行，集成领域决策 + 暂存槽自动取牌，`Update` 在空手牌时仍执行摸牌逻辑 |
| `Projectile` | 投射物 | Gameplay/Entities | 子弹/箭矢（线性+抛物线弹道，ClosestPoint 边缘命中，`_explosionRadius>0` 时以命中点为圆心范围爆炸，全额伤害）|
| `IBuildingTarget` | 可攻击目标接口 | Gameplay/Battle | CardUnit(_isBuilding) 实现，兵种攻击目标队列使用 |
| `Identity` | 阵营身份枚举 | Core/Battle | SoldierStats.cs 内嵌枚举（FarmerA/FarmerB/Landlord），取代原 FactionTag |
| `UnitHeight` | 单位高度枚举 | Gameplay/Entities | CardUnit.cs 内嵌 `[Flags]` 枚举（Ground/Air），用于高度系统判定（CanAttackHeight/CanBlockHeight） |
| `DamageType` | 伤害类型枚举 | Core/Battle | SoldierStats.cs 内嵌枚举（Physical/Special/Bomb/Burn/True） |
| `DamageFloatText` | 伤害飘字 | UI/Floating | 世界空间 TMP 飘字组件 |
| `FloatingTextPool` | 飘字对象池 | UI/Floating | 自动订阅 BattleManager.OnUnitSpawned |
| `GetUnitEdgeDistance` | 单位间边缘距离 | Gameplay/Entities | bounds.Intersects + ClosestPoint 退化兜底 |
| `IsBlockedAt` | 碰撞箱阻挡检测 | Gameplay/Entities | Physics2D.OverlapBox 移动前预判 |
| `UnitInfoPanel` | 兵种信息面板 | UI/Panels | 点选兵种显示属性面板（世界空间，跟随目标，面向摄像机）|
| `UnitSelector` | 兵种点选器 | Gameplay/Entities | Physics2D.OverlapPoint 点击检测，左键选中/取消 |
| `DomainSystem` | 领域系统 | Gameplay/Battle | 要不起领域 + 反制护盾 + 炸弹破封 + 封印手牌牌型 |
| `SealRuleEngine` | 封印规则引擎 | Gameplay/Battle | 判定手牌中可反制指定牌型的卡牌 |
| `CardTypeCompare` | 牌型比较器 | Gameplay/Battle | HasCounterInHand + CanCounter 两张牌型对比 |
| `BossController` | BOSS 控制器 | Gameplay/Battle | BOSS 生命周期（OnStart/OnTimer/OnBuildingDestroyed 触发），初始隐藏（Renderer/Collider/HealthBar/BossSkillSystem 全部禁用），ActivateBoss 恢复显示，IsActive 供门禁查询，阵营自动纠正，召唤师能力 |
| `BossSkillSystem` | BOSS 技能系统 | Gameplay/Entities | HP 阶段/定时/击杀触发，6 种效果（AoeDamage/AoeStun/Heal/Knockback/Buff/Dash），施法不可选取+清除 CC，冲刺用碰撞箱宽度扫描 |
| `UnitAudio` | 兵种音频 | Gameplay/Entities | 挂载子物体（解耦），`GetComponentInParent<CardUnit>` 获取引用，订阅事件 + 按 Clip 并发限制 + 屏幕可见性裁剪 |
| `UnitVFX` | 兵种特效 | Gameplay/Entities | 调用 VFXManager 对象池生成粒子特效 |
| `AudioManager` | 音频管理器 | Gameplay/Systems | 单例，4 通道优先级音效（UI/CombatHigh/Combat/CombatLow）+ BGM，DontDestroyOnLoad |
| `VFXManager` | 特效管理器 | Gameplay/Systems | 单例粒子特效对象池，DontDestroyOnLoad |
| `UIManager` | UI 管理器 | Gameplay/Systems | 跨场景单例，管理 UI_Scene 加载 + PauseMenu/VictoryPanel 引用 + EventSystem 去重 |
| `SceneLoader` | 场景加载器 | Gameplay/Systems | 场景切换工具（RestartGame/LoadMainMenu/QuitGame） |
| `UnitFlipper` | 朝向翻转 | Gameplay/Entities | 根据移动/攻击方向翻转 SpriteRenderer |
| `AttackEventRelay` | 攻击事件中继 | Gameplay/Entities | 子物体 Animator Event → 父物体 CardUnit.OnAttackHitFrame |
| `HeroUnitConfig` | 英雄单位配置 | Gameplay/Entities | MonoBehaviour，挂载到英雄预制体，包含英雄所有配置（属性/觉醒倍率/被动技能） |
| `HeroPassiveBase` | 英雄被动技能基类 | Gameplay/Entities | 抽象基类，所有英雄被动技能继承自此 |
| `BlademasterPassive` | 剑圣被动 | Gameplay/Entities/Passives | 攻击时概率造成额外伤害 |
| `GuardianPassive` | 铁卫被动 | Gameplay/Entities/Passives | 嘲讽源 + 伤害减免 |
| `SharpshooterPassive` | 神射被动 | Gameplay/Entities/Passives | 优先攻击血量最低的敌人 |
| `WarlockPassive` | 术士被动 | Gameplay/Entities/Passives | 攻击时溅射范围伤害 |
| `SpiritRiderPassive` | 灵骑被动 | Gameplay/Entities/Passives | 友军光环（攻速 + 移速加成） |
| `DomainUIController` | 领域 UI 统一控制器 | UI/Battlefield | 合并覆盖层 + 反击按钮 + 按钮视觉状态 + 冷却效果，单一入口管理所有领域 UI |
| `ButtonAudio` | 按钮音效 | UI/Audio | 自动为 Button 添加点击/悬停音效（走 UI 优先级通道） |
| `ButtonEffect` | 按钮特效 | UI/Components | 悬停放大 + 按压缩小动画 |
| `CoolDownEffect` | 冷却特效 | UI/Components | 可复用钟表式冷却视觉组件 |
| `DamageQueue` | 伤害批量结算队列 | Gameplay/Battle | 同帧伤害入队，帧末统一结算 HP 和死亡，消除 Update 执行顺序影响 |
| `GameTimerUI` | 对局计时 UI | UI/HUD | 显示游戏已运行时间（正计时），骤死期变色提示 |
| `VictoryPanel` | 胜利/结算面板 | UI/Panels | 对局结束时显示胜负、时长、奖励，支持单人/联机模式 |
| `VictoryStats` | 结算数据 | UI/Panels | struct，含 gameDuration/cardsPlayed/unitsSpawned/unitsKilled/goldEarned + 联机结算公式字段 |
| `UnitPassivesGizmosOverlay` | 被动范围叠加 | Editor | Scene View 实线（逻辑范围）+ 虚线（VFX 覆盖）+ 数值标注 |
| `UnitPassivesEditorWindow` | 被动技能调试窗口 | Editor | 编辑/运行时模式下查看和调整所有被动参数，无需进入 Play 模式 |
| `GameSession` | 游戏会话数据 | Gameplay/Systems | 静态类，仅存储叫分结果缓存（BidMultiplier/HasResult/LandlordSlot/AISlots），已废弃降级 |
| `SaveSystem` | 存档系统 | Gameplay/Systems | 基于 PlayerPrefs，存储金币/首次胜利/对局统计 |
| `BiddingManager` | 叫分期控制器 | UI/Bidding | 叫分场景主控：倒计时 + AI 叫分 + 玩家叫分 + 跳转 |
| `BiddingConfig` | 叫分配置 | Config | ScriptableObject，可配叫分时长/AI 策略/超时处理 |
| `MainMenuController` | 主菜单控制器 | UI | 单人/对战/商店/图鉴/设置/退出按钮管理 |
| `LevelConfig` | 关卡配置 | Config | ScriptableObject，存储关卡名称/描述/缩略图/场景名/难度/解锁状态/排序权重 |
| `UnitStatsConfig` | 兵种数值汇总 | Config | ScriptableObject，CSV 管线中间层，集中管理所有兵种基础属性 |
| `CsvIO` | CSV 读写工具 | Editor | 支持引号字段、UTF-8 BOM，ReadCsv/WriteCsv |
| `ConfigImportExport` | 配置导入导出窗口 | Editor | Tools → 配置数据管理，Units/Heroes/Economy/Bidding/Levels 双向同步 |
| `LevelCard` | 关卡卡片 | UI/LevelSelect | 单个关卡卡片组件（缩略图 + 信息 + 动态缩放） |
| `LevelSelectController` | 关卡选择控制器 | UI/LevelSelect | 轮播式关卡选择（中心最大，两侧缩小，拖拽滑动 + 吸附） |
| `FollowTarget` | 特效跟随组件 | Gameplay/Entities | 使 VFX 特效跟随目标 Transform 移动（君王光环/嘲讽光环） |
| `INetworkService` | 网络服务接口 | Gameplay/Network | 抽象接口，定义连接/房间/消息/场景同步 API + `OnMasterSwitched` 事件 |
| `FusionService` | Fusion 实现 | Gameplay/Network | `INetworkService` 的 Fusion 2 实现；管理 NetworkRunner、Host 建房与 Client 加房。通用 SendTo* 消息方法在此实现中为空操作并已弃用 |
| `NetworkManager` | 网络管理器 | Gameplay/Network | 单例，持有 INetworkService 引用，DontDestroyOnLoad |
| `OnlineLobbyController` | 联机大厅控制器 | UI/Online | 联机模式选择（单排/创建房间/加入房间）+ 房间管理 |
| `NetworkBiddingManager` | 联机叫分控制器 | UI/Bidding | 通过 INetworkService 抽象层通信，支持 Fusion 和本地联机 |
| `BiddingSceneBootstrap` | 叫分场景引导 | UI/Bidding | 检测联机房间状态，自动切换单机/联机叫分管理器 |
| `NetworkProtocol` | 网络协议常量 | Gameplay/Network | 事件 Key 定义 + Card/CardTypeResult 序列化 + 玩家槽位工具 |
| `NetworkLogger` | 网络日志 | Gameplay/Network | 将网络相关日志写入 `Logs/network_log_slotN.txt` 文件 |
| `NetworkDebugPanel` | 网络调试面板 | Gameplay/Network | 游戏内左上角显示槽位/手牌/金币/单位数/最近事件 |
| `LocalNetworkHub` | 本地联机消息路由 | Gameplay/Network | 静态进程内路由中心；维护玩家、准备状态、Master 角色与定向消息 |
| `LocalNetworkService` | 本地联机服务 | Gameplay/Network | `INetworkService` 的进程内模拟实现，不连接 Photon Cloud；用于本地联机流程测试 |
| `LocalTestLauncher` | 本地联机测试启动器 | Editor | Editor 窗口（Tools → 本地联机测试），创建多玩家 LocalNetworkService |
| `GameSnapshot` | Snapshot 层 | Gameplay/Network | 某 Tick 下的完整权威状态（§25），可完全重建游戏，不依赖 Event 历史 |
| `GameEvent` | Event 层 | Gameplay/Network | 不可变操作记录（§25），append-only，必须携带 Tick，不可直接修改状态 |
| `CodexUIController` | 图鉴 UI | UI/Codex | 图鉴浏览界面（分类/搜索/详情展示） |
| `CodexEntry` | 图鉴条目 | Config | ScriptableObject，存储单个图鉴条目（Id/DisplayName/Category/Icon/Description） |
| `CodexDatabase` | 图鉴数据库 | Config | ScriptableObject，按分类组织 CodexEntry 条目，支持运行时查询 |
| `UnitDebugToolWindow` | 兵种调试工具 | Editor | Editor 窗口（Tools → 技能可视化 → 综合调试工具），整合属性/被动/音效/特效/动画配置 |
| `FusionGameManager` | Fusion 游戏管理器 | Gameplay/Fusion | 替代 NetworkGameManager，基于 Tick 状态机 + 双缓冲战斗系统 |
| `FusionGameState` | Fusion 世界状态 | Gameplay/Fusion | WorldState struct，[Networked] 同步，唯一真相源 |
| `FusionBattleManager` | Fusion 战场管理器 | Gameplay/Fusion | Host 权威战斗逻辑 |
| `CombatSystem` | Fusion 战斗系统 | Gameplay/Fusion | FindTarget → AttackTick → ApplyDamage 流水线 |
| `PassiveSystem` | Fusion 被动系统 | Gameplay/Fusion | 16 种通用被动的 Fusion 实现 |
| `CardUnitView` | Fusion 单位视图 | Gameplay/Fusion | Client 侧视觉表现（行军/动画/血条） |
| `IntentBuffer` | 输入意图缓冲 | Gameplay/Fusion | Client 输入 → Host 处理的桥梁 |
| `UnitSyncManager` | 单位同步管理器 | Gameplay/Fusion | Fusion 网络对象生命周期管理 |
| `PlayerInputHandler` | 玩家输入处理 | Gameplay/Fusion | Fusion 输入采集 + 发送 |
| `ViewBinder` | 视图绑定器 | Gameplay/Fusion | Fusion 状态 → UI 视图的映射 |
| `DesyncDetector` | 失步检测器 | Gameplay/Fusion | Client/Host 状态偏差检测 |
| `IdentityService` | 身份服务 | Gameplay/Fusion/Identity | Singleton Facade，场景显式绑定 |
| `NetworkFacade` | 网络门面 | Gameplay/Network | 统一 Photon/Fusion/本地联机的调用入口 |
| `BattlePresentationManager` | 战斗演出管理器 | Gameplay/Presentation | 统一调度所有演出序列（镜头/对话/广播/特效并行执行），管理 CameraDirector.IsBusy |
| `PresentationSequence` | 演出序列配置 | Gameplay/Presentation | MonoBehaviour，挂在场景中，包含镜头/对话/广播/特效动作列表 |
| `CameraDirector` | 镜头导演 | Gameplay/Presentation | 镜头切换/聚焦/震动/缩放，IsBusy 标志供 CameraController 跳过 Update |
| `IRuntimeReady` | 运行时就绪接口 | Core/Lifecycle | 控制 Update 是否执行游戏逻辑 |
| `ShopItemConfig` | 商品配置 | Config | ScriptableObject，定义单个商品（ID/名称/图标/价格/类型） |
| `ShopDatabase` | 商品数据库 | Config | ScriptableObject，商品集合 |
| `ShopManager` | 商店管理器 | Systems | 购买逻辑（余额检查/扣款/解锁，基于 SaveSystem 持久化） |
| `ShopUIController` | 商店 UI | UI | 商品网格展示 + 购买确认 + 余额显示 |
| `DialogueData` | 对话数据 | UI/Dialogue | ScriptableObject，定义对话序列（说话人/立绘/内容） |
| `DialogueLine` | 单条对话 | UI/Dialogue | 单条对话数据（含立绘尺寸、打字速度、名称颜色） |
| `DialogueBox` | 对话框控制器 | UI/Dialogue | 打字机效果 + 立绘展示 + 全区域点击继续 |
| `DialogueTrigger` | 场景对话触发器 | UI/Dialogue | 场景进入后自动触发对话（支持延迟和单次触发） |

## 实施状态总览

### P0（已实现 ✅）

| 模块 | 状态 | 位置 |
|:---|:---|:---|
| 领域系统（要不起领域 + 反制护盾） | DomainSystem + SealRuleEngine + CardTypeCompare + DomainUIController | Gameplay/Battle/ + UI/Battlefield/ |
| 传送飞筒 + 暂存槽 | LaunchTubeUI（拖拽传牌，6s CD） + TempSlotUI（玩家暂存槽/队友只读暂存槽，队友 AI 自动取牌） | UI/Battlefield/ |
| BOSS 系统 | BossController（生命周期+召唤师）+ BossSkillSystem（HP 阶段/定时/击杀触发，6 种效果，冲刺+不可选取+清除 CC）+ 阵营自动纠正+初始隐藏 | Gameplay/Battle/ + Gameplay/Entities/ |
| 兵种音频系统 | UnitAudio + AudioManager 单例（4 通道优先级音效 + BGM） | Gameplay/Entities/ + Gameplay/Systems/ |
| 兵种特效系统 | UnitVFX + VFXManager 单例对象池 | Gameplay/Entities/ + Gameplay/Systems/ |
| 兵种点选系统 | UnitSelector + UnitInfoPanel（世界空间信息面板） | Gameplay/Entities/ + UI/Panels/ |
| 英雄配置重构 | HeroUnitConfig 组件挂载到英雄预制体，移除对全局 HeroConfig 的依赖 | Gameplay/Entities/ |
| 对话系统 | DialogueBox（打字机+立绘+全区域点击）+ DialogueData + DialogueTrigger + LevelConfig 关联 | UI/Dialogue/ + Config/ |
| 按钮音效/特效 | ButtonAudio + ButtonEffect + CoolDownEffect | UI/Audio/ + UI/Components/ |
| 被动系统重构 | CardTypePassives 删除，全部功能移入 UnitPassives + SpawnPool | UnitPassives.cs |
| 16 种通用被动 | 嘲讽/点杀/人海/冲锋/光环/盾墙/护盾/减速/眩晕/撕裂/震波/燃烧/溅射/死爆/骑兵追击/召唤师 | UnitPassives.cs |
| SpawnPool 预制体数组 | 8 组 ×13 槽（基础/诱饵/骑兵/连对/炸弹/坦克/无人机/轰炸机） | SpawnPool.cs |
| 四带二无人机 | 按点数区分型号移除，改为对应预制体属性 | SpawnPool.cs |
| 坦克硬编码 Buff 移除 | BuffBomb/BuffConsecutivePair/BuffFourWithTwoTank 全部删除 | BattleManager.cs |
| 飞机轰炸重做 | 只轰炸同路线，参数可调，CalcBombType 删除 | BattleManager.cs |
| 弹型效果 | 从 BattleManager 移入 Projectile 作为子弹通用特效（爆炸以命中点为圆心，全额伤害） | Projectile.cs |
| 英雄独特被动 | 剑圣/铁卫/神射/术士/灵骑全部实现（模块化被动技能组件） | Gameplay/Entities/Passives/ |
| 战斗飘字（伤害数字） | DamageFloatText + FloatingTextPool 对象池 | UI/Floating/ |
| IBuildingTarget 接口 | CardUnit(_isBuilding) 实现，统一攻击目标接口 | Gameplay/Battle/ |
| 动态基地列表 | 任意数量建筑拖入 baseBuildings 数组 | BattleManager + GameBootstrapper |
| 阵营系统重构 | Identity 枚举（FarmerA/FarmerB/Landlord）定义在 SoldierStats.cs 中 | SoldierStats.cs |
| 弹道边缘瞄准 | Projectile.GetTargetPos() 使用 Collider2D.ClosestPoint 瞄准碰撞箱边缘 | Projectile.cs |
| ClosestPoint 索敌 + GetEdgeDistance | 统一边缘距离计算，支持任何形状碰撞箱 | CardUnit.cs |
| 对象池血条重置 | UnitHealthBar.OnDisable 解绑 + 重置 _initialized | UnitHealthBar.cs |
| 预置兵种初始化 | CardUnit.Start 自动初始化 + 激活血条 | CardUnit.cs |
| 暴君税赋阵营修正 | TryGiveKillGold 检查凶手阵营 == 玩家阵营 | BattleManager.cs |
| 动画状态系统 | State(int) + Trigger + Bool 三层参数解耦 | CardUnit.cs + UnitPassives.cs |
| 性能优化 | 静态字典→实例字段、协程→计时器、FindObjs→OverlapCircle+ContactFilter2D | CardUnit.cs + UnitPassives.cs |
| 建筑碰撞箱 O(1) 快取 | IBuildingTarget.BuildingCollider 在 Awake 快取，消灭 GetComponent | CardUnit.cs（原 Installation.cs 已删除） |
| GetWorldRadius 删除 | 圆形近似补偿已删除，改用 ClosestPoint 通用边缘距 | CardUnit.cs + IBuildingTarget.cs（原 Installation.cs 已删除） |
| NonAlloc 升级 | OverlapCircleNonAlloc → OverlapCircle + ContactFilter2D | UnitPassives.cs + CardUnit.cs |
| B1: SpiritRider 光环防指数叠加 | 保存原始属性字典，进入范围应用一次、离开恢复，杜绝每秒复合 | BattleManager.cs |
| B2: 减速光环防 OriginalMoveSpeed 覆盖 | 仅首次 `≈0` 时保存原始移速，恢复后清零以便下次重新捕获 | UnitPassives.cs + CardUnit.cs |
| B3: 撕裂易伤正式生效 | `TakeDamage` 扣除血量前调用 `GetTearMultiplier(this)` | CardUnit.cs |
| B4: AI 组合枚举上限 | k 从大到小遍历（高价优先），上限 1000 次评估防帧率尖刺 | BuildingAI.cs |
| B5: 分担伤害改用缓存列表 | `RedistributeDamage` 移除 `FindObjectsByType`，改用 `_allUnits` | BattleManager.cs |
| B6: 暴君税赋合并循环 | `FindAndMarkForDeath` + `TryGiveKillGold` 合并为一次遍历 | BattleManager.cs |
| B7: 选牌超限拒绝 + 视觉反馈 | `ToggleCard` 返回 `false` 拒绝，`PulseRejection()` 红闪抖动动画 | SelectionValidator.cs + CardWidget.cs |
| B8: 推离向量 NaN 防御 | 零向量 `sqrMagnitude < 0.001f` 时改用 `Random.insideUnitCircle` | UnitPassives.cs |
| B9: 对象池全状态重置 | `OnPoolSpawn/Despawn` 补全 `_bonusDamage`、`OriginalMoveSpeed` 等 | CardUnit.cs |
| B10: BurnZone NonAlloc + Tick | 缓存 `Collider2D[64]` + 0.25s 检测间隔，消除每帧分配 | UnitPassives.cs |
| B11: 轰炸协程 NonAlloc | `BombingRunCoroutine` 使用 `_overlapCache[128]` | BattleManager.cs |
| B12: 费用表重复条目清理 | `GetCostTable` 去除重复的 `CardRank.Joker` | CardCostCalculator.cs |
| B13: 未识别牌型错误日志 | `DeployCards` default 分支输出 `Debug.LogError` 而非静默降级 | BattleManager.cs |
| B14: GameBootstrapper 空值保护 | `handArea` 为 null 时 `return` 防 NullReferenceException | GameBootstrapper.cs |
| B15: 初始化扫描条件化 | `Initialize` 中 `FindObjectsByType` 仅在 `usePhysicsPush=true` 时执行 | CardUnit.cs |
| TryAttack 冷却期防罚站 | `TryAttack` 通过 `_isAttacking` 标志防止重复攻击，冷却期允许继续行军 | CardUnit.cs |
| OnUpdate 攻击冷却站桩 | `_isAttacking=true` 时 OnUpdate 顶部直接 `return`（站桩等冷却），嘲讽可打断 | CardUnit.cs |
| 纯数学步进架构 | 全面移除 Rigidbody2D 依赖，所有移动改为 `transform.position` / `Translate` / `MoveTowards` | CardUnit.cs |
| 碰撞箱边缘检测统一 | 新增 `GetUnitEdgeDistance(CardUnit)` 统一 unit-to-unit 边缘距离，`bounds.Intersects` + ClosestPoint 退化兜底 | CardUnit.cs |
| 碰撞箱阻挡系统 | `IsBlockedAt(Vector3)` + `_blockBuffer[32]` + `Physics2D.OverlapBox`，移动前预判敌方碰撞箱重叠即停止 | CardUnit.cs |
| 嘲讽索敌边缘检测修复 | `FindNearestTauntSourceFor` 改用 `bounds.Intersects` + ClosestPoint 退化兜底，修复碰撞箱重叠时 dist 为负的 bug | CardUnit.cs |
| Gizmo 膨胀矩形攻击范围 | BoxCollider2D 攻击范围 Gizmo 改为碰撞箱膨胀 `_range` 的圆角矩形（4 边 + 4 角圆弧），精确匹配边缘判定 | CardUnit.cs |
| CollisionRadius 修正 | 改用 `Mathf.Min(size.x, size.y) / 2f`，取较小维度半径，避免巨型碰撞箱数值溢出 | CardUnit.cs |
| Card 唯一实例 ID | `_instanceId` 全局自增，`Equals`/`GetHashCode` 基于实例 ID，Reshuffle 后同点同花色牌可同时选取 | Card.cs |
| 音频优先级通道 | 4 个 AudioSource（UI=0/CombatHigh=64/Combat=128/CombatLow=200），UI 音效不被战斗挤掉 | AudioManager.cs |
| UnitAudio 按 Clip 并发限制 | `Dictionary<AudioClip, int>` 分组计数，不同兵种音效互不干扰 | UnitAudio.cs |
| UnitAudio 屏幕可见性裁剪 | Viewport 可见性检查，屏幕外兵种不播放攻击/技能音效 | UnitAudio.cs |
| UnitAudio 配额泄漏修复 | `OnDisable` 取消协程 + 立即归还 `_pendingClips` 配额 | UnitAudio.cs |
| 领域/反制护盾被破解音效 | `domainBrokenClip` + `counterShieldBrokenClip`，区别于自然过期音效 | AudioManager.cs + DomainSystem.cs |
| 对局计时器 + 骤死期 | GameStateMachine 自动阶段转换（Playing→SuddenDeath→GameOver）+ GameTimerUI 正计时显示 + OnTimeUp 触发结算 + 骤死期双倍回金速度（`EconomyConfig.suddenDeathMultiplier`） | GameStateMachine.cs + GameTimerUI.cs + GameBootstrapper.cs + EconomyManager.cs |
| 炸弹自动破封领域 | 农民出更大炸弹自动关闭领域（不触发反制护盾），作为反制护盾 CD 时的额外战术 | DomainSystem.cs |
| 炸弹击破反制护盾 | 地主出更大炸弹击破反制护盾，反制护盾被破解播放 `PlayCounterShieldBroken()` | DomainSystem.cs |
| 伤害批量结算（方案 C） | `DamageQueue` 静态队列 + `CardUnit.LateUpdate` 帧末结算，消除 Update 执行顺序对战斗结果的影响 | DamageQueue.cs + CardUnit.Combat.cs |
| 传送飞筒队友传送 | 飞筒检查队友暂存槽 → 牌进入队友暂存槽 → 队友 AI 延迟自动取牌 | GameBootstrapper.cs + BuildingAI.cs |
| 暂存槽只读模式 | `handArea=null` 时自动隐藏交互按钮，队友暂存槽只展示不操作 | TempSlotUI.cs |
| 地主隐藏队友暂存槽 | 地主身份隐藏 launchTubeUI + tempSlotUI + teammateTempSlotUI | GameBootstrapper.cs |
| CardSpriteDB 清理 | TempSlotUI 改用 CardWidget 预制体显示，移除 CardSpriteDB 回退逻辑 | TempSlotUI.cs + GameBootstrapper.cs |
| Installation/BaseController 移除 | 统一为 CardUnit(_isBuilding)，建筑自动获得批量伤害/护盾/减伤支持 | 删除 Installation.cs + BaseController.cs，更新 8 个引用文件 |
| CardUnit 建筑功能 | `_regenPerSecond` 回血 + `MaxHP`/`HPRatio` 属性 + `InitBuildingHP` + 建筑静止逻辑 | CardUnit.cs |
| 被动系统建筑过滤 | `FindNearestEnemy`/`UpdateKingAura`/`UpdateSlowAura`/`ApplySwarm` 排除 `_isBuilding` 单位 | CardUnit.Combat.cs + UnitPassives.cs |
| 叫分期系统 | BiddingManager + BiddingConfig + 叫分场景（30s 倒计时 + AI 叫分 + 跳转） | UI/Bidding/ + Config/ |
| 联机叫分系统 | NetworkBiddingManager（3 人网络轮流叫分 + AI 槽位 + 断线处理）+ BiddingSceneBootstrap（自动切换单机/联机）| UI/Bidding/ |
| 网络协议层 | NetworkProtocol（事件 Key 常量 + Card/CardTypeResult 序列化 + 玩家槽位工具）| Gameplay/Network/ |
| 网络接口扩展 | INetworkService 新增：IsInRoom/IsMasterClient/SendToMaster/SendToPlayer/LocalActorNumber/GetPlayerActorNumbers/OnCustomEvent 等 | Gameplay/Network/ |
| 联机游戏管理器 | FusionGameManager（Tick 状态机 + Host 权威出牌/摸牌/经济/领域） | Gameplay/Fusion/ |
| 金币同步 | FusionGameManager 通过 WorldState.PlayerX.Gold/IncomeRate 同步，Client 直接读取 | FusionGameManager.cs |
| 领域同步 | FusionGameManager 通过 WorldState.Game.DomainActive/DomainType 同步领域状态 | FusionGameManager.cs |
| 摸牌协议 | Client 调用 IntentBuffer.SubmitDrawCard()，Host 执行并更新 WorldState | FusionGameManager.cs |
| 牌堆同步 | WorldState.Game.DeckCount 记录剩余牌数，Host 递减 | FusionGameManager.cs |
| 状态版本控制 | Fusion [Networked] 属性自动版本控制，替代旧 StateVersion 机制 | FusionGameState.cs |
| 网络日志 | DesyncLogger + NetworkDebugPanel 替代旧 Network Trace Log | Gameplay/Network/ |
| 时间同步 | Fusion Runner.Tick（固定 100 tick/s）替代旧 PhotonNetwork.Time | FusionGameManager.cs |
| 胜利同步 | WorldState.Game.Phase 同步游戏结束状态 | FusionGameManager.cs |
| 存档系统 | SaveSystem（PlayerPrefs）存储金币/首次胜利/对局统计 | Gameplay/Systems/ |
| 完胜判定 | 玩家基地满血 → gameStateCoefficient = 1.5 | GameBootstrapper.cs |
| 叫分配置外置 | BiddingConfig ScriptableObject（叫分时长/AI 策略/超时处理） | Config/ |
| CSV 数据管线 | CsvIO + ConfigImportExport + UnitStatsConfig（Units/Heroes/Economy/Bidding/Levels 双向同步） | Editor/ + Config/ |
| 联机基地映射 | GameSession.PlayerBaseMapping 支持 3 人映射 + 随机分配 | Gameplay/Systems/ |
| 领域出牌校验 | PlayValidator 拒绝不能管上领域的牌 + _playerClickedCounter 区分玩家/AI | GameBootstrapper.cs + DomainSystem.cs |
| 主菜单场景 | MainMenuController（单人/对战/商店/图鉴/设置/退出） | UI/ |
| 关卡选择系统 | LevelSelectController + LevelCard + LevelConfig（轮播式选择，支持扩展） | UI/LevelSelect/ + Config/ |
| 联机网络层 | INetworkService + FusionService + NetworkManager（Photon Fusion） | Gameplay/Network/ |
| 联机大厅 | OnlineLobbyController（单排/创建房间/加入房间/匹配/准备） | UI/Online/ |
| 联机断线重连 | FusionService（Photon Fusion 内置断线恢复 + NetworkRunner 重连） | Gameplay/Network/ |
| 召唤师被动 | UnitPassives.enableSummoner（定时召唤 + 击杀召唤，Animation Event 驱动） | UnitPassives.cs |
| 击杀事件 | CardUnit.OnKillEvent + Summoner 引用 + 击杀归属到召唤师 | CardUnit.cs |
| 伤害飘字修正 | OnDamageCalculated 事件（含撕裂加成），与 OnTakeDamageEvent 分离 | CardUnit.Combat.cs + FloatingTextPool.cs |
| 撕裂效果修复 | TearTimer 移至 CardUnit 自管理 + TearDamagePerStack 可配置 | CardUnit.cs + UnitPassives.cs |
| 领域 UI 合并 | DomainUIController 合并 DomainOverlay + DomainCoolDownUI | UI/Battlefield/ |
| 领域按钮修复 | 反击按钮始终可见 + interactable 状态管理 + pending 可取消 | DomainUIController.cs |
| 眩晕打断攻击 | InterruptAttack() 公共方法 + 眩晕/嘲讽/召唤统一打断逻辑 | CardUnit.cs |
| 嘲讽优先级修复 | 嘲讽高于建筑锁定 + 嘲讽可打断攻击 + 被阻挡时降级攻击阻挡者 | CardUnit.cs |
| 盾墙缓存优化 | _shieldWallUnits 静态列表，Awake 注册 OnDestroy 注销，遍历范围从全部单位缩小到盾墙单位 | UnitPassives.cs |
| 连对生成修复 | 连对每个对子各生成一个兵种（去重点数） | BattleManager.cs |
| 召唤物完整生成 | SpawnSummonedUnit 走完整生成流程（对象池/注册/路径/目标/敌方列表），直接继承召唤师 FollowPath | BattleManager.cs |
| 多目标攻击系统 | `_maxTargets`/`_multiTargetRadius`/`MaxTargets` + `FindAllTargets()` + `OnAttackHitFrame()` 多目标分支 + `OnPerTargetAttackEvent`（溅射/眩晕按目标独立触发） | CardUnit.cs + CardUnit.Combat.cs + UnitPassives.cs |
| 快速连击被动 | `enableBurstAttack`（连击 N 次后自我眩晕冷却），`_burstHitCounter` 对象池重置 | UnitPassives.cs |
| 路线锁定系统 | `RoutePath._locked`/`Unlock()`/`Lock()` + `RouteGroup` 跳过锁定路线 + `GetRoute()`/`SwitchToFirstUnlocked()` | RoutePath.cs + RouteGroup.cs |
| BOSS 路线解锁 | `BossController.ActivateBoss()` 解锁 `_bossRoute` + `_playerRouteToBoss` | BossController.cs |
| BuildingAI 路线压力检测 | `ChooseLane()` 根据敌方金币权重 + 玩家路线权重 + 防守需求选择最优路线 | BuildingAI.cs |

| 特效缩放统一 | 震波/光环/燃烧/嘲讽特效缩放系数从 `/3f` 改为 `/2f` | UnitVFX.cs |
| 君王光环特效跟随 | `PlayKingAura` 改用 `Transform` 参数 + `FollowTarget` 组件跟随释放者 | UnitVFX.cs + UnitPassives.cs |
| 燃烧特效半径 | `PlayBurn` 新增 `radius` 参数，特效缩放匹配实际火海范围 | UnitVFX.cs + UnitPassives.cs |
| 召唤师攻击中兼容 | 攻击中不打断，直接生成召唤物（`StartSummon` 检查 `IsAttacking`） | UnitPassives.Summon.cs |
| BOSS 路径缓存修复 | `ActivateBoss` 销毁回调中 `route.CachePositions()`，防止 BOSS 回池后路径点失效 | BattleManager.cs |
| 伤害分担修复 | `RedistributeDamage` 改用 `SharedDamageOverride` 替代 `ShareRedirected` 跳过，主目标承受 60% + 其他各 20% = 100% | BattleManager.Spawning.cs + CardUnit.Combat.cs |

| 联机暂停修复 | 联机模式下 `PauseMenu` 不设置 `Time.timeScale = 0` | PauseMenu.cs |
| `CardHand.NotifyHandModified` | 公共方法，供网络同步直接操作列表后触发 `OnHandChanged` | CardHand.cs |
| 网络调试工具 | `NetworkLogger`（日志写入文件）+ `NetworkDebugPanel`（游戏内状态面板） | Gameplay/Network/ |
| 高度系统修复 | MoveTowardEnemyBase + MoveTowardTarget 添加 CanAttackHeight 检查 | CardUnit.Movement.cs |
| 阻挡逻辑修复 | IsBlockedAt 从 this.CanBlockHeight 改为 other.CanBlockHeight | CardUnit.Movement.cs |
| 溅射圆心修复 | EmitSplash 圆心从 target.transform.position 改为 Collider2D.ClosestPoint（攻击者→目标碰撞箱最近点），大型建筑边缘可溅射 | UnitPassives.cs |
| 召唤物路线修复 | SpawnSummonedUnit 直接继承 summoner.FollowPath，删除 FindBaseFor，修复基地切换分路后召唤物走错路 | BattleManager.Spawning.cs |
| UnitAudio 解耦 | UnitAudio 移至子物体，`RequireComponent` 移除，`GetComponent` → `GetComponentInParent`，UnitPassives 改用 `GetComponentInChildren` | UnitAudio.cs + UnitPassives.cs |
| BOSS 阵营自动纠正 | `GameBootstrapper` Step 5b 强制 `SetLandlord(!PlayerIsLandlord)` + 血条颜色刷新，解决 Awake 执行顺序不确定导致的阵营错误 | GameBootstrapper.cs |
| BOSS 初始隐藏 | `BossController.Awake` 禁用 Renderer/Collider/HealthBar/BossSkillSystem，`ActivateBoss` 时恢复，未激活 BOSS 不参与战斗 | BossController.cs |
| BOSS BuildingAI 保护 | `GameBootstrapper` Awake/联机模式中跳过 `_isBoss` 单位的 BuildingAI 禁用，Step 5b 显式启用 BuildingAI 后再 Inject | GameBootstrapper.cs |
| BuildingAI 空手牌兼容 | `Update` 在 `Hand.Count == 0` 时仍执行摸牌和经济逻辑，不再直接 return，解决 BOSS 空手牌无法出兵 | BuildingAI.cs |
| 全局唯一 UnitId | `BattleManager` 统一分配 `_globalUnitId`，`RegisterUnit` 调用 `SetUnitId`，`OnUnitDied` 改用 `Dictionary<int, CardUnit>` O(1) 查找，消除场景预置单位与工厂单位 ID 冲突 | BattleManager.cs + CardUnit.cs |
| RoutePath 缓存开关 | `_cachePositions` Inspector 开关，取消勾选后路径点实时跟随移动物体（如 BOSS），解决 BOSS 召唤兵种在初始位置生成 | RoutePath.cs |
| UnitHeight 默认值兜底 | `CardUnit.Awake` 检查 `_unitHeight/_canAttackHeight/_canBlockHeight == 0` 时恢复默认值，修复 Unity 序列化 `[Flags]` 枚举组合默认值为 0 的问题 | CardUnit.cs |
| BuildingAI 启用状态保护 | `GameBootstrapper` Awake 记录 `_buildingAIOriginallyEnabled`，Step 5a 仅恢复原本启用的 BuildingAI，Inspector 未勾选的不会被误启用 | GameBootstrapper.cs |
| BOSS 技能系统 | `BossSkillSystem` 组件，支持 HP 阶段/定时/击杀触发，6 种效果（AOE 伤害/眩晕/治疗/击退/Buff/冲刺），施法期间不可选取+清除 CC | BossSkillSystem.cs |
| Invulnerable 状态 | `CardUnit.Invulnerable` 属性，`TakeDamage`/`ApplyDamage` 开头检查，免疫所有伤害 | CardUnit.cs + CardUnit.Combat.cs |
| Heal 方法 | `CardUnit.Heal(amount)` 治疗方法，不超过 MaxHP | CardUnit.cs |
| 嘲讽多目标修复 | 攻击中嘲讽打断仅在嘲讽目标变化时生效，防止多个嘲讽光环导致无法攻击 | CardUnit.cs |
| 攻击超时安全阀 | `_attackStateTimer` 计时，`AttackInterval×3` 秒未完成强制重置，防止攻击状态卡死 | CardUnit.cs |
| BOSS 技能动画 | SimpleAnimator 新增 dashClip/bossSkill1-3Clip，Animator Controller 新增 Dash/BossSkill1-3 Trigger 状态，更新菜单 `Tools → 生成兵种 Animator Controller` | SimpleAnimator.cs + CardUnit.Animation.cs + CreateUnitAnimatorController.cs |
| 动画优先级文档 | Any State Trigger 内部顺序：Death > Shockwave > Splash > StunHit > KingAura > DeathExplosion > Burn > Summon > Dash > BossSkill1-3 | ARCHITECTURE.md |
| 骤死期双倍金币 | `EconomyManager` 订阅 `OnPhaseChanged`，骤死期回金速度 × `suddenDeathMultiplier`，GameOver 恢复基础值 | EconomyManager.cs + GameBootstrapper.cs |
| VisualCenter 使用基准 | 全项目统一圆心地图（碰撞箱中心 vs transform.position），编辑器预览差异说明 | ARCHITECTURE.md §9.7 |
| 统一 Buff 系统 | 命名 Buff（同名覆盖，异名乘算）+ 从基础值 RecalculateStats | CardUnit.cs §9.8 |
| 受伤计算流水线 | 8 步串行：真实伤害→屏障→盾墙→减免→吸收→分担→撕裂→扣血 | CardUnit.Combat.cs §9.9 |
| FindNearestEnemyBuilding 热路径优化 | `FindObjectsByType` → `_enemyBuildings` 缓存数组（BattleManager 注入），O(m) 建筑遍历 | CardUnit.Combat.cs + BattleManager.cs + CardUnit.cs |
| transform.position 违规修复 | 10 处距离/范围计算改为 `VisualCenter`（英雄被动/三人组/轰炸/溅射/召唤） | BattleManager.Heroes.cs + Spawning.cs + UnitPassives.cs + UnitPassives.Summon.cs |
| 静态状态跨局清理 | `UnitAudio.ClearClipCounts()` + `DamageQueue.Clear()` 在新局开始时调用，`_shieldWallUnits` 对象池回收注销 | UnitAudio.cs + DamageQueue.cs + UnitPassives.cs + GameBootstrapper.cs |
| DomainUIController lambda 退订 | `counterCoolDown.OnCoolDownComplete` 匿名 lambda → 存储字段 `_onCounterCoolDownComplete`，OnDestroy 退订 | DomainUIController.cs |

| Master Authority Combat | `SimulatesCombat` 属性控制战斗模拟归属。Client 禁止 OnUpdate/TakeDamage/Die，只做视觉行军。死亡由 Master 广播 UNIT_DIED 驱动 | CardUnit.cs + CardUnit.Combat.cs |

| 记牌器负值兜底 | `Mathf.Max(0, total - discarded)` 防止联机牌堆不同步时显示负数 | CardCounterUI.cs |
| 本地联机模拟系统 | `LocalNetworkHub`（消息路由）+ `LocalNetworkService`（INetworkService 本地实现）+ `LocalTestLauncher`（Editor 窗口）。单进程多玩家，零网络延迟 | LocalNetworkHub.cs + LocalNetworkService.cs + LocalTestLauncher.cs |

| 叫分槽位转换 | `OnStartGame()` 将大厅位置索引正确转换为 actor-number 排序槽位，AI 槽位 = 全集 {0,1,2} - 真人玩家槽位，修复后加入玩家替代 AI 叫分的 bug | OnlineLobbyController.cs |
| 叫分槽位同步等待 | `InitializeSlotWhenReady()` 协程等待 Photon PlayerList 同步完成后再计算槽位（最多 3 秒轮询），修复同时进入时所有玩家拿到相同手牌 | NetworkBiddingManager.cs |
| AI 槽位房间持久化 | AI 槽位通过 Photon 房间属性（`aiSlots`）持久化，后加入玩家从房间属性恢复，修复后加入玩家看不到 AI 的 bug | OnlineLobbyController.cs |
| 农民路线 UI 隐藏 | `HandArea.SetRouteUIVisible(false)` 隐藏农民不需要的路线选择 UI（prev/nextButton, routeLabel, routeIndicator） | HandArea.cs + GameBootstrapper.cs |
| **Fusion 联机架构（Phase 5）** | FusionGameManager（GamePhase 状态机 + BeginTick/ReadInputOnce/ConsumeInput 管道 + TryAdvanceState 收敛器 + OnStateChanged 生命周期）+ FusionGameState（WorldState [Networked] + GamePhase 枚举 + PlayerState.IsAI）+ CombatSystem + PassiveSystem + IntentBuffer + ViewBinder + PlayerInputHandler + UnitSyncManager + DesyncDetector + Identity 系统（IIdentityProvider 策略模式）| Gameplay/Fusion/ + Gameplay/Fusion/Identity/ + Gameplay/Fusion/UI/ |
| **AI 槽位 WorldState 同步** | FusionGameManager.AddAISlot/RemoveAISlot/IsAISlotByState — 通过 WorldState.PlayerX.IsAI 同步，替代 PUN SendToAll/SetRoomProperty | FusionGameManager.cs + OnlineLobbyController.cs |
| **UnitPassives BOSS 守卫** | UnitPassives.Update() 开头检查 BossController.IsActive，BOSS 未激活时跳过所有被动逻辑 | UnitPassives.cs |
| **战斗演出系统** | BattlePresentationManager（并行调度所有动作类型）+ PresentationSequence（MonoBehaviour，场景级配置）+ CameraDirector（镜头，IsBusy 演出锁）+ BattleAnnouncementManager（广播）+ BossDialogueBubble（BOSS 对话，支持技能对话）| Gameplay/Presentation/ |
| **网络门面** | NetworkFacade（统一 Photon/Fusion/本地联机调用入口）| Gameplay/Network/ |
| **Fusion 网络基础设施** | FusionService + NetworkRunnerSetup + FusionTestSpawner + FusionTestObject + FusionMinimalDebug + FileLogger | Gameplay/Network/ |
| **运行时就绪接口** | IRuntimeReady（控制 Update 是否执行游戏逻辑）| Core/Lifecycle/ |
| **Fusion 叫分 RPC 管道** | Client→Host 叫分/摸牌/出牌通过 `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]` 发送，Host 统一执行 | FusionGameManager.cs + NetworkBiddingManager.cs |
| **IdentityReady 初始化锁** | GameState.IdentityReady 字段，Host 写入 1 后 Client 才允许读取 WorldState，防止时序竞态 | FusionGameState.cs + FusionGameManager.cs + NetworkBiddingManager.cs |
| **GameSession 模式锁定** | SetNetworkMode(bool) 单次写入不可逆，Reset() 不再覆盖 IsNetworkMode | GameSession.cs |
| **叫分状态机修正** | TryAdvanceState 叫分完成后不自动推进 Playing，等 UI 确认按钮触发 ConfirmBidding() | FusionGameManager.cs |
| **AI 叫分节流修复** | 删除 AISystem 内部 BID_INTERVAL_TICKS 双重节流，由 ProcessAI 统一控制频率 | AISystem.cs |
| **IsPlayingFinished 修正** | 返回 false，胜负由 BattleManager.OnGameEnded 驱动，不再误判手牌为空为游戏结束 | FusionGameManager.cs |
| **出牌执行连接** | ApplyPlayCards 连接 BattleManager.DeployCards 生成兵种 | FusionGameManager.cs |
| **Client 状态同步** | GameSceneSync 每 0.5s 广播牌堆/手牌/金币，RPC 同步到 Client 本地缓存 | GameSceneSync.cs + FusionGameManager.cs |
| **单机/联机拆分** | NetworkGameBootstrapper 接管联机游戏场景初始化（手牌/摸牌/领域/暂停/胜利面板），GameBootstrapper 移除所有 Fusion 引用 | NetworkGameBootstrapper.cs + GameBootstrapper.cs |
| **Fusion Runner 生命周期修复** | `ShutdownRunner()` 替代 `Destroy(_runner)`，保留 Runner 组件复用，消除 Fusion 回调循环中的已销毁引用警告 | FusionService.cs |
| **叫分轮次校验** | `SubmitBid`/`ApplyBid`/`OnBid` 三层校验 `CurrentBidTurn == slot`，防止非轮次叫分 | FusionGameManager.cs + NetworkBiddingManager.cs |
| **Client Slot 分配修复** | `GetLocalSlot()` Client 回退改为匹配 `SlotXPlayerRef`，替代盲目取第一个非 AI 槽位 | FusionGameManager.cs |

### P1（仍需实现）

| 系统 | 章节 | 说明 |
|:---|:---|:---|
| 商店系统 | §29 | 主菜单按钮已预留。商品类型：英雄解锁（200-500 金币）+ 皮肤外观（100-300 金币）。需新增 ShopItemConfig/ShopDatabase/ShopManager/ShopUIController |

### P2（增强/可视化）

| 功能 | 章节 | 状态 |
|:---|:---|:---|
| 索敌可视化标记 | §28（Combat Gate） | ❌ 未实现 |
| 兵种综合调试工具 | Editor | ✅ UnitDebugToolWindow（整合属性/被动/音效/特效/动画配置 + 批量应用） |

## 费用与牌值常量参考

```
点数映射: 3=3, 4=4, 5=5, 6=6, 7=7, 8=8, 9=9, 10=10, J=11, Q=12, K=13, A=14, 2=16, Joker=17
费用公式: C_n = 10 × 1.17^(n-3)
牌型系数 M_type: 单/对/三=1.0, 顺子/连对=0.7, 三带一/二=0.85, 四带二/飞机=0.8, 炸弹=1.2
```

## 1. 核心架构原则

### 1.1 单向依赖原则（Dependency Rule）

```
┌─────────────────────────────────────────────────────────────┐
│                    UI / View 层                              │
│  仅呈现数据、捕获输入，向 Gameplay 发送操作指令                 │
│  命名空间: DoudizhuTower.UI.*                                │
│  (依赖 Gameplay 层)                                          │
├─────────────────────────────────────────────────────────────┤
│                    Gameplay 层                                │
│  MonoBehaviour 管理器，监听 Core 事件，调控 Entities            │
│  命名空间: DoudizhuTower.Gameplay.*                          │
│  (依赖 Core 层，依赖 UnityEngine)                              │
├─────────────────────────────────────────────────────────────┤
│                    Core / 纯逻辑层                             │
│  零 Unity 依赖的 Pure C# Class                               │
│  牌堆 / 牌型检测 / 经济计算 / 战斗公式 / 索敌算法                │
│  命名空间: DoudizhuTower.Core.*                              │
│  (不依赖任何其他层)                                           │
├─────────────────────────────────────────────────────────────┤
│                    Config / 数据源层                           │
│  ScriptableObject 配置表，供所有层读取                         │
│  命名空间: DoudizhuTower.Config                              │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 三条红线

| # | 红线 | 后果 |
|:---|:---|:---|
| 1 | `Core/` 下任何文件出现 `using UnityEngine;` 或 `GetComponent<>` | 联机帧同步必然分叉 |
| 2 | `Core/` 中的战斗逻辑使用 `UnityEngine.Random` | 跨端牌序不一致 |
| 3 | `UI/` 直接修改 `Core/` 的手牌数据（必须经由 Gameplay 层校验） | 数据竞态 + 无法回滚 |

---

## 2. 目录结构与职责边界

```
Assets/Scripts/
├── BuildLogSuppressor.cs              # Build 日志抑制（Editor 中不受影响，Build 中禁用 Console 弹窗）
├── Core/                              # 纯 C# 逻辑层（零 Unity 依赖）
│   ├── Card/
│   │   ├── CardSuit.cs                # 花色枚举 ♠♥♣♦
│   │   ├── CardRank.cs                # 点数枚举 3-Joker，含 IsConsecutiveTo()
│   │   ├── Card.cs                    # 单张牌（值对象, struct, 含 IsJoker + _instanceId 唯一标识）
│   │   ├── CardDeck.cs                # 54 张牌堆（Fisher-Yates + 索引游标 + 弃牌堆）
│   │   ├── CardHand.cs                # 手牌容器（上限 20 张，排序/增删/事件 + 牌型封印系统）
│   │   ├── CardType.cs                # 牌型枚举（13 种）
│   │   ├── CardTypeResult.cs          # 牌型检测结果（类型 + 主体点数 + 长度 + 挂件）
│   │   └── CardTypeDetector.cs        # ★ 核心算法：合规牌型判定
│   ├── Battle/
│   │   └── SoldierStats.cs            # §3.1 兵种属性表（struct）+ 枚举（Lane/Identity/DamageType）
│   ├── Economy/
│   │   ├── EconomySystem.cs           # 金币增减 + 回金速度成长曲线
│   │   └── CardCostCalculator.cs      # §2.3 Cost = ΣC_n × M_type 公式
│   └── Lifecycle/
│       └── IRuntimeReady.cs           # 运行时就绪接口（IsRuntimeReady 属性，控制 Update 是否执行游戏逻辑）
│
├── Gameplay/                          # 运行时管理层（MonoBehaviour）
│   ├── Systems/
│   │   ├── GameBootstrapper.cs        # ★ 单机装配套管线（12 步初始化，联机模式 yield break）
│   │   ├── GameStateMachine.cs        # ★ FSM + 自动计时（Playing→SuddenDeath→GameOver，OnTimeUp 事件）
│   │   ├── TimerQueue.cs              # 全局异步计时器队列
│   │   ├── EconomyManager.cs          # 焊接 Core.EconomySystem → UI + 骤死期双倍回金
│   │   ├── AudioManager.cs            # ★ 单例音频管理器（4 通道优先级音效 + BGM，DontDestroyOnLoad）
│   │   ├── VFXManager.cs             # ★ 单例粒子特效对象池（DontDestroyOnLoad）
│   │   ├── UIManager.cs              # ★ 跨场景 UI 管理器（单例，管理 UI_Scene 加载 + PauseMenu/VictoryPanel 引用）
│   │   ├── SceneLoader.cs            # 场景加载工具（LoadBidding/LoadGame/LoadMainMenu/LoadCodex/QuitGame）
│   │   ├── GameSession.cs            # 叫分结果缓存（已废弃降级，核心逻辑由 WorldState 替代）
│   │   └── SaveSystem.cs             # ★ 存档系统（PlayerPrefs，金币/首次胜利/对局统计）
│   ├── Entities/
│   │   ├── CardUnit.cs                # ★ 兵种基类（4 个 partial 文件）
│   │   ├── CardUnit.Combat.cs         #   战斗逻辑（索敌/攻击/伤害/死亡）
│   │   ├── CardUnit.Movement.cs       #   移动逻辑（路径行军/追击/阻挡检测）
│   │   ├── CardUnit.Animation.cs      #   动画状态机 + 对象池生命周期 + Gizmo
│   │   ├── UnitFactory.cs             # 对象池工厂（按预制体引用分池）
│   │   ├── UnitHealthBar.cs           # §3.A.1 头顶血条（OnDisable 重置 _initialized）
│   │   ├── UnitPassives.cs            # ★ 17 种通用被动（字段/生命周期/光环/战斗被动）
│   │   ├── UnitPassives.Summon.cs     #   召唤师被动（定时召唤 + 击杀召唤）
│   │   ├── SimpleAnimator.cs          # 动画控制器（AnimatorOverrideController，18 种动画含 BOSS 技能）
│   │   ├── Projectile.cs              # 子弹/投射物（线性+抛物线，ClosestPoint 瞄准/命中 + _explosionRadius 范围爆炸）
│   │   ├── AttackEventRelay.cs        # 子物体 Animator Event → 父物体 CardUnit.OnAttackHitFrame
│   │   ├── UnitSelector.cs            # ★ 兵种点选器（Physics2D.OverlapPoint 左键选中/取消）
│   │   ├── UnitAudio.cs              # ★ 兵种音频组件（优先级通道 + 按 Clip 并发限制 + 屏幕可见性裁剪）
│   │   ├── UnitVFX.cs                # ★ 兵种特效组件（调用 VFXManager 对象池）
│   │   ├── UnitFlipper.cs            # 兵种朝向翻转（根据移动/攻击方向翻转 Sprite）
│   │   ├── HeroUnitConfig.cs         # ★ 英雄单位配置组件（挂载到英雄预制体）
│   │   ├── HeroPassiveBase.cs        # ★ 英雄被动技能基类
│   │   └── Passives/                 # 英雄被动技能组件
│   │       ├── BlademasterPassive.cs #   剑圣被动（攻击时概率造成额外伤害）
│   │       ├── GuardianPassive.cs    #   铁卫被动（嘲讽源 + 伤害减免）
│   │       ├── SharpshooterPassive.cs#   神射被动（优先攻击血量最低的敌人）
│   │       ├── WarlockPassive.cs     #   术士被动（攻击时溅射范围伤害）
│   │       └── SpiritRiderPassive.cs #   灵骑被动（友军光环：攻速 + 移速加成）
│   ├── Battle/
│   │   ├── BattleManager.cs           # ★ 战场管理器（核心字段/初始化/主循环/胜负判定）
│   │   ├── BattleManager.Spawning.cs  #   牌型生成（12种）+ 通用生成 + 召唤物 + 伤害分担
│   │   ├── BattleManager.Heroes.cs    #   英雄生成（使用 HeroUnitConfig 组件）
│   │   ├── DamageQueue.cs             # ★ 伤害批量结算队列（同帧入队，帧末统一结算 HP + 死亡）
│   │   ├── IBuildingTarget.cs         # 可攻击目标接口（CardUnit _isBuilding 唯一实现）
│   │   ├── RoutePath.cs               # 路径定义 + Scene 视图 Gizmo + `_cachePositions` 缓存开关 + `_locked` 路线锁定
│   │   ├── RouteGroup.cs              # 路线组（跳过锁定路线，`GetRoute()`/`SwitchToFirstUnlocked()`）
│   │   ├── SpawnPool.cs               # 出兵池（8 组 ×13 槽预制体映射）
│   │   ├── BuildingAI.cs              # ★ 建筑 AI（集成 DomainSystem 领域/反制决策 + 暂存槽自动取牌）
│   │   ├── BossController.cs          # ★ BOSS 控制器（定时/建筑摧毁触发，召唤师能力）
│   │   ├── BossSkillSystem.cs         # ★ BOSS 技能系统（HP 阶段/定时/击杀触发，6 种效果，冲刺+不可选取+清除 CC）
│   │   ├── DomainSystem.cs            # ★ 领域系统（字段/属性/事件/初始化/配置）
│   │   ├── DomainSystem.Gameplay.cs   #   出牌触发 + 计时器 + 激活/关闭 + 手牌封印
│   │   ├── SealRuleEngine.cs          # 封印规则引擎（判定手牌可反制牌型）
│   │   ├── CardTypeCompare.cs         # 牌型比较器（HasCounterInHand + CanCounter）
│   │   └── MapController.cs           # 地图坐标常量与工具方法（WinCondition 枚举定义在 BattleManager.cs 中）
│   ├── Network/                        # 联机网络层
│   │   ├── INetworkService.cs          # ★ 网络服务抽象接口（连接/房间/消息/场景同步 + OnMasterSwitched）
│   │   ├── NetworkManager.cs           # ★ 网络管理器单例（持有 INetworkService，DontDestroyOnLoad）
│   │   ├── NetworkFacade.cs           # 网络门面（统一 Photon/Fusion/本地联机的调用入口）
│   │   ├── NetworkProtocol.cs          # ★ 网络协议常量 + Card/CardTypeResult 序列化 + 玩家槽位工具
│   │   ├── NetworkLogger.cs            # ★ 网络日志写入文件（Logs/network_log_slotN.txt）
│   │   ├── NetworkDebugPanel.cs        # ★ 游戏内网络状态面板（左上角：槽位/手牌/金币/单位数/最近事件）
│   │   ├── GameSnapshot.cs             # ★ Snapshot 层（某 Tick 下的完整权威状态，可完全重建游戏）
│   │   ├── GameEvent.cs                # ★ Event 层（不可变操作记录，append-only，必须携带 Tick）
│   │   ├── FusionService.cs            # Fusion 网络服务实现（Photon Fusion SDK）
│   │   ├── FusionTestSpawner.cs        # Fusion 测试生成器（Editor 调试用）
│   │   ├── FusionTestObject.cs         # Fusion 测试对象（网络对象测试）
│   │   ├── FusionMinimalDebug.cs       # Fusion 最小调试组件
│   │   ├── NetworkRunnerSetup.cs       # NetworkRunner 配置（Fusion 运行时初始化）
│   │   ├── FileLogger.cs              # 文件日志（调试用）
│   │   ├── LocalNetworkHub.cs          # ★ 本地联机模拟消息路由中心（静态类，直接方法调用）
│   │   └── LocalNetworkService.cs      # ★ INetworkService 本地实现（零网络延迟，用于单进程多玩家测试）
│   ├── Fusion/                         # Fusion 联机架构（替代 PUN NetworkGameManager）
│   │   ├── FusionGameManager.cs        # ★ Fusion 游戏管理器（替代 NetworkGameManager，基于 Tick 状态机 + 双缓冲战斗）
│   │   ├── NetworkGameBootstrapper.cs  # ★ 联机游戏场景初始化器（手牌/摸牌/领域/暂停/胜利面板 RPC 桥接）
│   │   ├── FusionGameState.cs          # ★ Fusion 世界状态（WorldState struct，[Networked] 同步）
│   │   ├── FusionBattleManager.cs      # ★ Fusion 战场管理器（Host 权威战斗逻辑）
│   │   ├── CombatSystem.cs            # ★ Fusion 战斗系统（FindTarget → AttackTick → ApplyDamage）
│   │   ├── PassiveSystem.cs           # ★ Fusion 被动系统（17 种通用被动的 Fusion 实现）
│   │   ├── BossSkillSystem.cs         # Fusion 版 BOSS 技能系统（HP 阶段/定时/击杀触发）
│   │   ├── CardUnitView.cs            # ★ Fusion 单位视图（Client 侧视觉表现，行军/动画/血条）
│   │   ├── IntentBuffer.cs            # ★ 输入意图缓冲（Client 输入 → Host 处理的桥梁）
│   │   ├── EventBuffer.cs             # 事件缓冲（Fusion 事件队列）
│   │   ├── UnitSyncManager.cs         # ★ 单位同步管理器（Fusion 网络对象生命周期）
│   │   ├── UnitBuffer.cs              # 单位缓冲（Fusion 单位池）
│   │   ├── UnitConfig.cs              # 单位配置（Fusion 单位预制体映射）
│   │   ├── PlayerInputHandler.cs      # ★ 玩家输入处理（Fusion 输入采集 + 发送）
│   │   ├── AISystem.cs               # Fusion AI 系统（Host 侧 AI 决策）
│   │   ├── ViewBinder.cs             # ★ 视图绑定器（Fusion 状态 → UI 视图的映射）
│   │   ├── DesyncDetector.cs         # ★ 失步检测器（Client/Host 状态偏差检测）
│   │   ├── DesyncLogger.cs           # 失步日志（失步事件记录）
│   │   ├── TickDisplay.cs            # Tick 显示（调试用，显示当前 Tick 号）
│   │   ├── GameSceneSync.cs          # ★ Client 状态同步（每 0.5s 广播牌堆/手牌/金币，RPC 同步）
│   │   ├── Identity/                  # 身份系统（IIdentityProvider 策略模式）
│   │   │   ├── IIdentityProvider.cs    # 身份提供者接口（Offline/Online 统一抽象）
│   │   │   ├── OfflineIdentityProvider.cs  # 单机身份提供者
│   │   │   ├── OnlineIdentityProvider.cs   # 联机身份提供者
│   │   │   ├── LobbyIdentityService.cs    # 大厅身份服务（联机房间身份分配）
│   │   │   └── IdentityService.cs         # 身份服务 Facade（Singleton，场景显式绑定）
│   │   └── UI/                        # Fusion UI 层
│   │       ├── GameUIController.cs     # Fusion 游戏 UI 控制器
│   │       ├── GoldView.cs            # Fusion 金币视图
│   │       └── HandView.cs            # Fusion 手牌视图
│   └── Presentation/                  # 战斗演出系统
│       ├── BattlePresentationManager.cs  # ★ 战斗演出管理器（并行调度镜头/对话/广播/特效）
│       ├── PresentationSequence.cs    # ★ 演出序列配置（MonoBehaviour，场景级，支持镜头target运行时绑定）
│       ├── CameraDirector.cs          # ★ 镜头导演（IsBusy 演出锁 + SyncOriginalPosition）
│       ├── BattleAnnouncementManager.cs  # 战场广播管理器（屏幕广播文字显示）
│       └── BossDialogueBubble.cs      # ★ BOSS 对话气泡（演出对话 + 技能对话，订阅 OnSkillActivated）
│
├── UI/                                # 界面层（仅作为 View）
│   ├── Audio/
│   │   └── ButtonAudio.cs             # ★ 按钮音效（自动点击/悬停音效，支持自定义 Clip）
│   ├── Hand/
│   │   ├── HandArea.cs                # 手牌区排列 + 选中交互 + 出牌音效 + 回收系统 + 牌型封印状态
│   │   ├── CardWidget.cs              # 单张卡牌 UI 控件（拖拽 + 选中浮动 + 封印锁链 + 拒绝脉冲）
│   │   └── SelectionValidator.cs      # 选牌校验 + 实时牌型检测（封印牌拒绝）
│   ├── HUD/
│   │   ├── CardCounterUI.cs           # §1.5 记牌器
│   │   └── GameTimerUI.cs             # ★ 对局计时 UI（正计时显示，骤死期变色）
│   ├── Bidding/
│   │   ├── BiddingManager.cs          # ★ 叫分期控制器（倒计时 + AI 叫分 + 玩家叫分 + 跳转）
│   │   ├── NetworkBiddingManager.cs   # ★ 联机叫分控制器（3 人网络轮流叫分 + AI 槽位 + 断线处理）
│   │   └── BiddingSceneBootstrap.cs   # 叫分场景引导（检测联机状态 → 切换单机/联机管理器）
│   ├── Battlefield/
│   │   ├── LaunchTubeUI.cs            # ★ 传送飞筒（农民专属，拖拽传牌，6s CD）
│   │   ├── TempSlotUI.cs             # ★ 暂存槽（接收飞筒传牌，交互模式/只读模式）
│   │   └── DomainUIController.cs      # ★ 领域 UI 统一控制器（覆盖层 + 反击按钮 + 按钮状态 + 冷却效果）
│   ├── LevelSelect/                    # 关卡选择
│   │   ├── LevelSelectController.cs    # ★ 轮播式关卡选择（中心最大，两侧缩小，拖拽 + 吸附）
│   │   └── LevelCard.cs               # ★ 关卡卡片组件（缩略图 + 信息 + 动态缩放）
│   ├── Online/                         # 联机 UI
│   │   └── OnlineLobbyController.cs    # ★ 联机大厅（单排/创建房间/加入房间 + 匹配 + 房间管理）
│   ├── Codex/                          # 图鉴系统
│   │   └── CodexUIController.cs        # 图鉴 UI 控制器（分类浏览/搜索/详情展示）
│   ├── Dialogue/                       # 对话系统
│   │   ├── DialogueData.cs             # ★ 对话数据 ScriptableObject（定义对话序列）
│   │   ├── DialogueBox.cs              # ★ 对话框控制器（打字机+立绘+全区域点击继续）
│   │   └── DialogueTrigger.cs          # ★ 场景进入对话触发器（自动/手动触发）
│   ├── Components/
│   │   ├── CoolDownEffect.cs          # ★ 可复用钟表式冷却视觉（Image.fillAmount Radial 360）
│   │   └── ButtonEffect.cs            # ★ 按钮悬停放大 + 按压缩小动画
│   ├── Floating/
│   │   ├── DamageFloatText.cs         # 伤害飘字组件（向上浮动 + 渐隐，颜色编码）
│   │   └── FloatingTextPool.cs        # 飘字对象池（订阅 BattleManager.OnUnitSpawned）
│   ├── Panels/
│   │   ├── UnitInfoPanel.cs           # ★ 兵种信息面板（世界空间，跟随目标，实时属性）
│   │   ├── PauseMenu.cs              # ★ 暂停菜单（ESC 切换，音量滑块，联机模式不暂停游戏逻辑）
│   │   ├── VictoryPanel.cs           # ★ 胜利/结算面板（单人/联机模式，对局时长+奖励）
│   │   └── VictoryStats.cs           # 结算数据 struct（gameDuration + 联机结算公式字段）
│   ├── MainMenuController.cs          # 主菜单控制器（开始游戏/退出）
│   ├── SceneFader.cs                  # 场景切换淡入淡出效果
│   ├── ScreenEffect.cs               # 全屏特效（屏幕震动等）
│   └── CameraController.cs            # 摄像机 WASD/边缘滚动 + 滚轮缩放
│
├── Config/                             # ScriptableObject 数据源
│   ├── EconomyConfig.cs               # 经济曲线配置
│   ├── BiddingConfig.cs               # ★ 叫分配置（叫分时长/AI 策略/超时处理）
│   ├── LevelConfig.cs                 # ★ 关卡配置（关卡名称/描述/缩略图/场景名/难度）
│   ├── UnitStatsConfig.cs             # ★ 兵种数值汇总（CSV 管线中间层，预制体引用+属性）
│   ├── CardSpriteDB.cs                # 卡牌精灵图数据库
│   ├── CodexEntry.cs                  # 图鉴条目 ScriptableObject（Id/DisplayName/Category/Icon/Description）
│   └── CodexDatabase.cs              # 图鉴数据库 ScriptableObject（按分类组织条目，支持运行时查询）

Assets/Editor/                          # 编辑器工具（不在 Scripts/ 下）
├── CreateUnitAnimatorController.cs     # 生成兵种 Animator Controller（12 状态）
├── CreateRunnerPrefab.cs              # 创建 Fusion NetworkRunner 预制体工具
├── ReplaceAllTMPFonts.cs              # 批量替换 TextMeshPro 字体工具
├── AudioClipTrimmer.cs                # ★ 音频剪辑工具（波形可视化 + 裁剪 + 试听 + 导出 WAV）
├── CsvIO.cs                          # ★ CSV 读写工具（支持引号字段、UTF-8 BOM）
├── ConfigImportExport.cs             # ★ CSV 配置数据导入导出窗口（Tools → 配置数据管理）
├── UnitDebugToolWindow.cs            # ★ 兵种综合调试工具（属性编辑/被动配置/音效预览/批量应用）
└── LocalTestLauncher.cs              # ★ 本地联机测试启动器（Tools → 本地联机测试，单进程多玩家）

_DisabledTests/                         # 已禁用的测试（项目根目录）
└── CardTypeDetectorTests.cs
```

---

## 3. 跨层焊接协议：观察者模式

Core/ 层是 Pure C# Class，**无法**使用 `GameObject.Find` 或 Inspector 拖拽。
唯一的跨层通信方式：**C# `event Action<>` 委托事件**。

### 3.1 核心事件总表

| 触发时机 | 事件签名 | 发布者（Core） | 订阅者（Gameplay/UI） |
|:---|:---|:---|:---|
| 金币变动 | `OnGoldChanged(float)` | EconomySystem | EconomyManager → GoldDisplay |
| 回金速度变动 | `OnIncomeChanged(float)` | EconomySystem | EconomyManager → incomeText |
| 手牌变动 | `CardHand.OnHandChanged(List<Card>)` | CardHand | HandArea.RefreshHand |
| 手牌 UI 变动 | `HandArea.OnHandChanged()` (无参) | HandArea | DomainSystem |
| 补牌 | `OnCardDrawn(Card)` | CardDeck | — |
| 弃牌 | `OnDiscarded()` | CardDeck | — |
| 牌堆刷新 | `OnDeckReshuffled()` | CardDeck | — |
| 兵种扣血 | `OnHPChanged(int, float)` | CardUnit | UnitHealthBar |
| 兵种阵亡 | `OnDied(int)` | CardUnit | BattleManager → UnitFactory |
| 兵种攻击 | `OnAttackEvent(CardUnit)` | CardUnit | UnitPassives（人海/冲锋，每次攻击触发一次）|
| 兵种多目标攻击 | `OnPerTargetAttackEvent(CardUnit)` | CardUnit | UnitPassives（溅射/眩晕/连击，每个目标独立触发）|
| 兵种受伤 | `OnTakeDamageEvent(float, DamageType)` | CardUnit | UnitAudio（音效）, BattleManager（分担）|
| 伤害结算完成 | `OnDamageCalculated(float, DamageType)` | CardUnit | FloatingTextPool（飘字，含撕裂加成）|
| 兵种死亡 | `OnDeathEvent()` | CardUnit | UnitPassives（死爆/燃烧）|
| 兵种击杀 | `OnKillEvent(CardUnit)` | CardUnit | UnitPassives（召唤师击杀召唤）|
| 召唤帧触发 | `OnSummonFrame()` | CardUnit | UnitPassives（召唤师 Animation Event）|
| 基地扣血 | `OnHPChanged(int, float)` | CardUnit | UnitHealthBar（复用兵种血条组件，与兵种共用同一事件签名） |
| 基地摧毁 | `OnDestroyed(IBuildingTarget)` | CardUnit | BattleManager |
| 阶段切换 | `OnPhaseChanged(GamePhase)` | GameStateMachine | GameTimerUI |
| 时间耗尽 | `OnTimeUp()` | GameStateMachine | GameBootstrapper → BattleManager.TriggerDefeat |
| 单位生成 | `OnUnitSpawned(CardUnit)` | BattleManager | FloatingTextPool |
| 领域激活 | `OnDomainActivated(CardTypeResult, float)` | DomainSystem | DomainUIController |
| 领域解除 | `OnDomainDeactivated()` | DomainSystem | DomainUIController |
| 反制护盾激活 | `OnCounterShieldActivated(CardTypeResult, float)` | DomainSystem | DomainUIController |
| 反制护盾解除 | `OnCounterShieldDeactivated()` | DomainSystem | DomainUIController |
| 飞筒传送 | `OnCardTransmitted(Card)` | LaunchTubeUI | GameBootstrapper（移除手牌 → 队友暂存槽）|
| 暂存槽清空 | `OnSlotEmptied()` | TempSlotUI | — |
| 网络自定义事件 | `OnCustomEvent(string, object, int)` | INetworkService | NetworkBiddingManager, OnlineLobbyController |
| 网络连接断开 | `OnConnectionLost()` | INetworkService | OnlineLobbyController |
| 网络玩家加入 | `OnPlayerJoined(string)` | INetworkService | OnlineLobbyController, NetworkBiddingManager |
| 网络玩家离开 | `OnPlayerLeft(string)` | INetworkService | OnlineLobbyController, NetworkBiddingManager |
| 联机金币同步 | WorldState.PlayerX.Gold/IncomeRate | Fusion [Networked] | Client 直接读取 |
| 联机领域同步 | WorldState.Game.DomainActive/DomainType | Fusion [Networked] | Client 直接读取 |
| 联机摸牌请求 | IntentBuffer.SubmitDrawCard() | Client → Host | FusionGameManager.ProcessNetworkInput() |
| HP 同步 | UnitSyncManager（Fusion RPC） | Host → Client | UnitSyncManager.cs |


### 3.2 订阅者生命周期规则

```
MonoBehaviour.OnEnable()   → 订阅 Core 事件
MonoBehaviour.OnDisable()  → 取消订阅 Core 事件
MonoBehaviour.OnDestroy()  → 确保取消订阅（双重保险）
```

**例外**：`UnitPassives` 使用 `Awake()`/`OnDestroy()` 配对注册。
**UnitHealthBar** 使用 `OnDisable()` 解绑 + 重置 `_initialized`，以支持对象池复用。

---

## 4. 工业级初始化装配套管线

所有模块在 `GameBootstrapper` 中**自底向上**完成装配，使用动态基地列表：

**Awake() 阶段（必须在 Start 之前）：**
```
读取 GameSession.HasResult → 设置 _playerIsLandlord 和 playerBaseIndex
设置 CardUnit.PlayerIsLandlord（必须在 Awake 中，保证在所有 UnitHealthBar.Start() 之前生效）
联机模式：跳过 BuildingAI 禁用，由 NetworkGameBootstrapper 接管（直接 return）
单机模式：记录 _buildingAIOriginallyEnabled → 禁用所有预置 BuildingAI（跳过 _isBoss 单位）
```

**Start() 协程阶段：**
```
Step 0:  确保 UI_Scene 已加载（UIManager.WaitForReady）
Step 0b: 加载存档（SaveSystem.Load）
Step 1:  加载 Config（EconomyConfig）
Step 2:  实例化 Core 层（CardDeck, EconomySystem）+ 启用批量伤害结算
Step 3:  发初始手牌（玩家手牌 + AI 手牌，单机按基地遍历 / 联机按 AISlots）
Step 4:  初始化建筑 CardUnit（InitBuildingHP + 注册战场）
Step 5:  依赖注入焊接（EconomyManager.Initialize(gameStateMachine), BattleManager.Initialize/SetEconomyManager）
Step 5b: BOSS 控制器注入（FindObjectsByType<BossController> → 纠正阵营 SetLandlord → 刷新血条颜色 → 启用 BuildingAI → boss.Inject(battleManager, deck)）
Step 5a: AI 对手初始化（遍历 aiHands，非玩家基地注入 BuildingAI + DomainSystem 引用）
Step 6:  焊接 UI（HandArea 空值保护 → return, 出牌事件, PlayValidator）
Step 6b: 焊接传送飞筒 + 暂存槽（单机：查找队友 AI → 初始化队友暂存槽 → 注入 BuildingAI → 飞筒接线 / 联机：地主隐藏飞筒，农民启用飞筒 + FusionGameManager IntentBuffer 传牌/取牌同步）+ 根据身份隐藏 UI（地主隐藏飞筒/暂存槽/队友暂存槽，农民隐藏分路）
Step 7:  基地血条使用 UnitHealthBar（与兵种共用）
Step 8:  焊接摸牌按钮（自动摸牌定时器 + 手动摸牌按钮，地主 5s/10g，农民 6s/12g）
Step 9:  暂停菜单事件焊接
Step 9b: 胜利面板焊接（OnGameEnded → StopTimer → CollectVictoryStats → Show）
Step 9c: 焊接金币追踪（economyManager.OnGoldEarned → battleManager.TrackGoldEarned）
Step 9d: 计时器焊接（GameTimerUI ← GameStateMachine, OnTimeUp → TriggerDefeat）
Step 10: DomainSystem 初始化（DomainUIController 统一管理覆盖层 + 按钮 + 冷却）
```

预置兵种终态初始化由 `CardUnit.Start()` 生命周期方法自动处理（血条激活 + 战场注册），不属于 GameBootstrapper 步骤。

基地选择使用 `Component[] baseBuildings` 数组，玩家通过 `playerFaction`（Farmer/LandLord）和 `playerBaseIndex` 选择操控的基地。
**B14 安全阀**：`handArea` 未在 Inspector 赋值时直接 `Debug.LogError` + `return`，杜绝 NullReferenceException 崩溃。

### AI 注入管线

```csharp
// 遍历 baseBuildings，非玩家操控的基地自动注入 BuildingAI
foreach (var baseBldg in baseBuildings)
{
    var cu = baseBldg.GetComponent<CardUnit>();
    if (cu == null) continue;
    bool isPlayerBase = (baseBldg == playerBaseRef);
    if (!isPlayerBase && baseBldg.GetComponent<BuildingAI>() != null)
    {
        var aiHand = new CardHand(17);
        _mainDeck.Deal(7, aiHand);
        var identity = cu.IsLandlord ? Identity.Landlord : Identity.FarmerA;
        InjectBuildingAI(baseBldg, aiHand, econConfig, battleManager, identity);
    }
}
```

---

## 5. 坐标系约定（可编辑地图）

所有兵种位置、射程、索敌计算，统一使用 Unity 世界坐标。

### 关键原则

- **基地位置**由关卡设计师在场景中自由放置
- **路线**由 `RouteGroup` 组件定义，内含 `RoutePath[]` 数组
- **兵种生成点**为每个基地下 `SpawnPool.SpawnPoint` 子物体位置
- **前排/后排**：按距目标基地的距离排序

### 索敌判定

- **检测方式**：`Physics2D.OverlapCircle(point, radius, filter, buffer)`，非分配，预缓存 64 槽
- **距离计算**：两套边缘距离方法：
  - `GetEdgeDistance(IBuildingTarget)`：用于建筑目标（bounds.Intersects + 双向 ClosestPoint）
  - `GetUnitEdgeDistance(CardUnit)`：用于单位间距离（bounds.Intersects + 双向 ClosestPoint + ClosestPoint 退化兜底）
  - 共同逻辑：
    1. `bounds.Intersects` 安全阀：碰撞箱重叠时直接返回 0
    2. 双向 `ClosestPoint` 计算真正边缘距
    3. 无碰撞箱时回退 `VisualCenter` / `LogicCenter`
  - `GetUnitEdgeDistance` 额外逻辑：ClosestPoint 退化兜底（查询点在碰撞箱内部时改用中心距减双半径）
- **GetWorldRadius() 已删除**：不再使用圆形近似补偿建筑碰撞半径
- **嘲讽优先级**：`IsTauntSource=true` 且在 `TauntRange` 内的敌方单位最高优先级（嘲讽索敌同样使用 `bounds.Intersects` + ClosestPoint 边缘检测）
- **本路优先**：同 `Lane` 的敌人最高优先级（`Lane.None` 匹配任何路线）
- **目标切换**：锁定目标后打到死，超出 `Range × 3` 后重新索敌。建筑锁定不覆盖已有敌方单位目标——兵种在追击小兵时不会因路过建筑而强制转火
- **碰撞箱阻挡**：移动前 `IsBlockedAt(nextPos)` 用 `Physics2D.OverlapBox` 检测敌方碰撞箱重叠，重叠即停止（边缘接触卡位）
- **CollisionRadius**：取碰撞箱**较小**维度半径（`Mathf.Min(size.x, size.y) / 2f`），仅用于 ClosestPoint 退化兜底路径
- **攻击范围 Gizmo**：BoxCollider2D 画碰撞箱膨胀 `_range` 的圆角矩形（4 边 + 4 角 DrawArc），精确匹配边缘判定边界

### IBuildingTarget 目标队列

兵种生成时通过 `SetTargets()` 注入 `IBuildingTarget[]` 队列（主堡、箭塔、BOSS 等）。
`CardUnit._isBuilding=true` 的单位自动加入目标队列。

---

## 5a. 2.5D 沙盘视觉规范 v2.2

### 5a.1 纯数学步进架构

所有兵种移动完全基于 `transform.position` 数学计算，不依赖 Unity 物理引擎：

```csharp
// 移动方式
transform.Translate(dir * Stats.MoveSpeed * Time.deltaTime, Space.World);  // 追击/行军
transform.position = Vector3.MoveTowards(transform.position, targetPos, step);  // 路径行军

// 碰撞箱仅用于范围检测 + 阻挡判定，不参与物理模拟
_collider.isTrigger = true;
```

**阻挡机制**：`IsBlockedAt(Vector3)` 在每帧移动前用 `Physics2D.OverlapBox` 检测目标位置是否有敌方碰撞箱重叠，若重叠则拒绝更新坐标，实现碰撞箱边缘接触即停止。

### 5a.2 父子结构约定

```
父物体（CardUnit, Collider2D）— 逻辑/碰撞
  └── 子物体（SpriteRenderer, Animator, UnitHealthBar）— 视觉/动画/血条
```

### 5a.3 血条缩减方向

```csharp
fillTransform.localScale = new Vector3(_initialScale.x * ratio, ...);
fillTransform.localPosition = new Vector3(_initFillLocalX + _halfWorldWidth * (1f - ratio), ...);
```

### 5a.4 碰撞箱模型

| 参数 | 值 | 说明 |
|:---|:---|:---|
| Is Trigger | true | 碰撞箱仅用于 OverlapBox/OverlapCircle 检测 |
| 用途 | 范围检测 + 阻挡判定 | ClosestPoint 边缘距离、bounds.Intersects 重叠判定、IsBlockedAt 阻挡 |

---

## 6. 异步计时器队列规范

所有并发定时器统一由 `TimerQueue` 管理，禁止 `Invoke()` 或 `StartCoroutine()`。

**例外**：
- `DomainSystem` 使用自身 `Update()` 驱动的 float 计时器（`UpdateDomainTimer`/`UpdateCounterShieldTimer`/`UpdateCooldowns`），因其状态机复杂度不适合 TimerQueue 的回调模式。
- `BossController` 在 TimerQueue 不可用时回退到 `Invoke()`（TimerQueue 可用时优先使用 TimerQueue）。
- 演出系统（`BattlePresentationManager`/`CameraDirector`/`BossDialogueBubble`）和视觉效果（`UnitPassives` 击退/`SpiritRiderPassive` 光环）使用 `StartCoroutine()`，因其需要协程的流程控制能力（等待、并行、取消）。

### 全局计时器汇总

| 计时器 | 类型 | 持续时间 | 位置 | 状态 |
|:---|:---|:---|:---|:---|
| 农民补牌 | 循环 | 6s | GameBootstrapper | ✅ 已实现 |
| 地主补牌 | 循环 | 5s | GameBootstrapper | ✅ 已实现 |
| AI 自动摸牌 | 循环 | 5/6s | BuildingAI.Update() | ✅ 已实现 |
| AI 出牌判定 | 循环 | 4s | BuildingAI.Update() | ✅ 已实现 |
| AI 经济增长 | 循环 | 60s | BuildingAI.Update() | ✅ 已实现 |
| 经济成长 | 循环 | 60s | EconomyManager | ✅ 已实现 |
| 记牌器刷新 | 事件驱动 | — | CardCounterUI.Refresh() | ✅ 已实现 |
| 叫分期（单机） | 一次性 | 30s | BiddingManager | ✅ 已实现 |
| 叫分期（联机） | 一次性 | 30s（可配） | NetworkBiddingManager | ✅ 已实现 |
| 要不起领域 | 一次性 | 5s | DomainSystem | ✅ 已实现 |
| 反制护盾 | 一次性 | 2s | DomainSystem | ✅ 已实现 |
| 传送飞筒 CD | 一次性 | 6s | LaunchTubeUI | ✅ 已实现 |
| 对局阶段 | 一次性 | 300s（可配） | GameStateMachine | ✅ 已实现 |
| 骤死期 | 一次性 | 60s（可配） | GameStateMachine | ✅ 已实现 |

### GameStateMachine 自动计时

`Update()` 驱动阶段自动转换：`Playing`（playingDuration）→ `SuddenDeath`（suddenDeathDuration）→ `GameOver` + `OnTimeUp`。

```csharp
// 关键属性
public float ElapsedTime;        // 对局已用时间（StopTimer 后冻结）
public float RemainingTime;      // 当前阶段剩余时间
public float PlayingDuration;    // 对局阶段总时长（Inspector 可配）
public float SuddenDeathDuration;// 骤死期总时长（Inspector 可配）

// 关键方法
public void TransitionTo(GamePhase newPhase);  // 手动切换阶段
public void StopTimer();                        // 冻结 ElapsedTime（结算前调用）

// 关键事件
public event Action<GamePhase> OnPhaseChanged;  // 阶段切换
public event Action OnTimeUp;                    // 骤死期结束
```

### 骤死期效果

`EconomyManager` 订阅 `OnPhaseChanged`，进入骤死期时回金速度乘以 `EconomyConfig.suddenDeathMultiplier`（默认 2x），游戏结束时恢复基础值。

```csharp
// EconomyManager.OnPhaseChanged()
if (phase == GamePhase.SuddenDeath)
    _coreEconomy.SetIncomeRate(_baseIncomeRate * config.suddenDeathMultiplier);
else if (phase == GamePhase.GameOver)
    _coreEconomy.SetIncomeRate(_baseIncomeRate);
```

### TimerQueue 接口

```csharp
public int Schedule(float delaySeconds, Action callback);
public int ScheduleLoop(float interval, Action callback);
public bool Cancel(int timerId);
public void CancelAll();
```

---

## 7. 致命地雷检查表

### 🛑 RED-01: Core 层必须完全隔离 UnityEngine
### 🛑 RED-02: 战斗计算必须纯数学
### 🛑 RED-03: UI 层不准直接修改 Core 数据
### 🛑 RED-04: 手牌动态更新保护
### 🛑 RED-05: 事件注册必须配对（含 OnDisable 重置对象池状态）
### 🛑 RED-06: 对象池回收必须重置全部状态（含 UnitHealthBar._initialized、CardUnit._bonusDamage/OriginalMoveSpeed）
### 🛑 RED-07: 禁用 `FollowPath = null` 粗暴清路径
### 🛑 RED-08: `TryAttack` 通过 `_isAttacking` 标志防止重复攻击；冷却期内 `_isAttacking=true` 时 OnUpdate 顶部直接 `return`（站桩等冷却），嘲讽可打断当前攻击
### 🛑 RED-09: 减速光环保留原始移速：仅当 `OriginalMoveSpeed ≈ 0f` 时保存，多光环不互相覆盖；恢复后清零以便下次捕获
### 🛑 RED-10: SpiritRider 光环每秒不可复合叠加：保存盟友原始属性字典，进入范围一次性应用，离开恢复
### 🛑 RED-11: 撕裂（Tear）易伤必须在 `CardUnit.TakeDamage()` 扣除血量前调用 `GetTearMultiplier(this)`
### 🛑 RED-12: 君王光环/震波推离前必须检查 `sqrMagnitude < 0.001f`，零向量时改用 `Random.insideUnitCircle.normalized`
### 🛑 RED-13: `SelectionValidator.ToggleCard` 在超出上限时必须返回 `false` 拒绝新选，不可静默丢弃最早选中；UI 层调用 `PulseRejection()` 提供视觉反馈

---

## 8. 牌型检测器：Core 层最高优先级模块

**输入 → 输出示例**

| 输入牌组 | 输出 (CardTypeResult) |
|:---|:---|
| [3♠, 3♥] | Type=Pair, MainRank=Three |
| [7♠, 7♥, 7♦, 8♣, 9♠] | Type=TripleWithOne, MainRank=Seven, Kickers=[Eight] |
| [3♠, 4♥, 5♦, 6♣, 7♠] | Type=Straight, MainRank=Seven, Length=5 |
| [9♠, 9♥, 9♦, 9♣] | Type=Bomb, MainRank=Nine |
| [5♠, 5♥, 6♦, 6♣, 7♠, 7♥] | Type=ConsecutivePair, MainRank=Seven, Length=3 |
| [Joker, Joker] | Type=DoubleKingBomb |
| [3♠, 3♥, 3♦] | Type=Triple, MainRank=Three |
| [3♠, 4♥, 5♦, 6♣, 7♠, 8♥] | Type=Straight6Plus, MainRank=Eight, Length=6 |

**边界规则**
- 农民出牌上限 5 张，地主 6 张（`_maxSelection` 参数）
- 单张牌型无法开启要不起领域（§4.2 特例铁律）
- 点数 2 映射为 n=16（非 15），确保费用公式自洽
- 大小王（Joker）视为同一点数，两张 Joker 总是构成王炸
- 核心逻辑见 `CardTypeDetector.cs`，无 Unity 依赖

---

## 9. 兵种实体规范

### 9.1 基础兵种接口

CardUnit、BattleManager、DomainSystem、UnitPassives 均采用 partial class 拆分，Inspector 引用不变。

CardUnit 拆分为 4 个文件：
- `CardUnit.cs`：主文件，属性/状态/IBuildingTarget/统一 Buff 系统（StatBuff struct，命名 Buff 乘法叠加）
- `CardUnit.Combat.cs`：战斗逻辑（索敌/攻击/伤害计算/死亡处理）
- `CardUnit.Movement.cs`：移动逻辑（路径行军/追击/阻挡检测/路径重投影）
- `CardUnit.Animation.cs`：动画状态机 + 对象池生命周期（OnPoolSpawn/OnPoolDespawn 全状态重置）+ Gizmo

**IBuildingTarget 实现：**
- `BuildingCollider` → 直接返回 `_collider`（O(1)）
- `LogicCenter` → 返回 `VisualCenter`（`Collider2D.bounds.center`）
- `GetWorldRadius()` 已删除

**多目标攻击：**
- `_maxTargets`：同时攻击的最大目标数（1=单目标，>1=多目标），`MaxTargets` 属性公开
- `_multiTargetRadius`：多目标搜索半径（0=使用攻击范围）
- `FindAllTargets()`：返回范围内最多 `_maxTargets` 个敌方单位，嘲讽目标优先，按距离排序
- `OnAttackHitFrame()` 多目标模式：
  1. `OnAttackEvent` 一次性触发人海/冲锋等基础被动 + 计算伤害
  2. 循环每个目标：`OnPerTargetAttackEvent` 独立触发溅射/眩晕/连击 + 造成伤害
- `OnAttackHitFrame()` 单目标模式：`OnAttackEvent` 触发所有被动（含溅射/眩晕），与旧行为一致

**网络同步方法：**
```csharp
public void SetHP(float hp);              // 设置 HP（网络校正用，不触发伤害流程）
public void ForceDie();                   // 强制死亡（网络校正用，跳过伤害流程）
```

**伤害分担字段：**
```csharp
public bool ShareRedirected { get; set; }           // 分担标记（RedistributeDamage 设置）
public float SharedDamageOverride { get; set; }     // 分担后的伤害值（>0 时替代原始伤害）
```

**新增方法：**
```csharp
// 统一边缘距离计算（建筑目标）
private float GetEdgeDistance(IBuildingTarget target)
{
    // 1. 无碰撞箱 → 回退 VisualCenter / LogicCenter
    // 2. bounds.Intersects → 返回 0（防御 ClosestPoint 重叠死锁）
    // 3. 双向 ClosestPoint 计算真实边缘距
}

// 统一边缘距离计算（单位间，避免 IBuildingTarget 装箱）
public float GetUnitEdgeDistance(CardUnit other)
{
    // 1. bounds.Intersects → 返回 0
    // 2. ClosestPoint 退化兜底：查询点在碰撞箱内部时用中心距减双半径
    // 3. 双向 ClosestPoint 计算真实边缘距
}

// 碰撞箱阻挡检测（移动前预判）
private bool IsBlockedAt(Vector3 pos)
{
    // Physics2D.OverlapBox 在目标位置检测敌方碰撞箱重叠
}

// 动画接口
public void TriggerAnim(string name);     // 一次性效果
public void SetAnimBool(string name, bool value);  // 持续效果
```

**VisualCenter 属性：**
```csharp
public Vector2 VisualCenter => _collider != null ? _collider.bounds.center : (Vector2)transform.position;
```
全场所有距离/瞄准/击退计算统一使用此值，禁止直接读 `transform.position`。

### 9.2 行为决策树（OnUpdate）

```
1. 已有目标（小兵/建筑）且存活且在射程内 → 攻击
2. 已有目标但超出追击范围 → 放弃，重新索敌
3. 无目标 → UpdateTarget()
   a. 嘲讽源在范围内 → 锁定嘲讽源
   b. 特化索敌（点杀/骑兵）→ 返回特化目标
   c. 默认索敌 → 本路最近敌人
4. _isAttacking=true（攻击冷却中）→ 嘲讽可打断，否则站桩等待动画+伤害完成 → return
5. 有目标且在射程内 → TryAttack()，设置 _isAttacking=true，return（站桩等冷却）
5. 有目标但不在射程内 → 追击（同路可追，跨路无视）
6. 无目标 → 沿路径行军
7. 路径到达终点 → 直接攻击终点建筑
```

**关键规则**：建筑锁定（行军中经过建筑旁）不会覆盖已有的敌方小兵目标。嘲讽优先级高于一切，包括建筑。

### 9.3 寻路与移动（纯数学步进）

- **路径行军**：`Vector3.MoveTowards(transform.position, targetPos, speed * dt)`，纯数学位移
- **战斗追击**：`transform.Translate(dir * speed * dt, Space.World)`，纯数学位移
- **碰撞箱阻挡**：所有移动前调用 `IsBlockedAt(nextPos)`，若目标位置有敌方碰撞箱重叠则拒绝移动
- **路径重投影**：`ResnapToClosestPathDistance()` 战斗后用点积投影重校准 `_pathDistance`，只允许前进
- **路径终点**：到达 `FollowPath.TotalLength` 后清空路径，转为直接朝向基地移动
- **所有距离判定**：默认使用 `VisualCenter`（`Collider2D.bounds.center`），禁用 `transform.position` 裸读
- **Rigidbody2D 已移除**：全面转型纯几何数学步进，碰撞箱仅用于范围检测和阻挡判定

### 9.4 兵种被动系统（UnitPassives）

所有被动在预制体 Inspector 中勾选启用，`Awake()` 中完成事件订阅。共 17 种：

| 被动 | 开关字段 | 触发方式 | 说明 |
|:---|:---|:---|:---|
| 点杀 | `enableSniper` | `OverrideFindTarget` | 锁定全场血量最低的敌方单位 |
| 人海连击 | `enableSwarm` | `OnAttackEvent`（一次） | 每名周围友军追加 50% ATK |
| 冲锋一击 | `enableCharge` | `OnAttackEvent`（一次） | 蓄力后首击 ATK×2.5，蓄力期间移速×1.3 |
| 君王光环 | `enableKingAura` | Update 每 interval | 周期震退周围敌人 |
| 盾墙线 | `enableShieldWall` | `ApplyShieldWallGlobal` | 周围友军伤害减免（乘法叠加）|
| 嘲讽光环 | `enableTaunt` | Awake | 设置 `IsTauntSource = true` |
| 死亡爆炸 | `enableDeathExplosion` | `OnDeathEvent` | 死亡时范围爆炸 |
| 护盾吸收 | `enableShieldAbsorb` | Awake | 设置 `DamageAbsorbRemaining` |
| 减速光环 | `enableSlowAura` | Update 每帧 | 周围敌人移速降低，仅首次 `≈0` 时保存 `OriginalMoveSpeed`，多光环不互相覆盖；`SlowRestoreTimer` 到期恢复后清零 |
| 攻击眩晕 | `enableStunOnHit` | `OnAttackEvent` + `OnPerTargetAttackEvent` | 攻击命中设置目标 `StunTimer`（多目标模式下每个目标独立眩晕） |
| 撕裂 | `enableTear` | `OnAttackEvent` | 为目标叠加 `TearStacks`；`CardUnit.TakeDamage()` 扣除血量前调用 `GetTearMultiplier()` 结算受伤 +5%/层 |
| 出场震波 | `enableShockwave` | Awake | 出场震退周围敌人 |
| 死亡燃烧 | `enableBurnOnDeath` | `OnDeathEvent` | 死亡留下火海（BurnZone）持续伤害 |
| 溅射攻击 | `enableSplash` | `OnAttackEvent` + `OnPerTargetAttackEvent` | 攻击时以 `Collider2D.ClosestPoint`（攻击者→目标碰撞箱最近点）为圆心扩散范围伤害，支持大型建筑边缘溅射；多目标模式下每个目标独立触发溅射 |
| 骑兵追击 | `enableCavalryChase` | `OverrideFindTarget` | 优先锁定远程（IsRanged）敌方单位 |
| 召唤师 | `enableSummoner` | Update 定时 + `OnKillEvent` | 定时召唤（Animation Event 驱动）+ 击杀召唤（从尸体位置立刻生成），召唤物直接继承召唤师的 `FollowPath`（而非基地 `RouteGroup.CurrentRoute`），击杀归属到召唤师。击杀召唤物不会触发新召唤（防止无限循环） |
| 快速连击 | `enableBurstAttack` | `OnAttackEvent` + `OnPerTargetAttackEvent` | 连续攻击 N 次后自我眩晕进入冷却（`burstHitCount`/`burstCooldown`），多目标模式下每个命中计数 |

**召唤师可配参数**：`summonPrefab`（召唤物预制体）、`summonInterval`（定时召唤间隔）、`maxSummons`（最大召唤物数量）、`summonOnKill`（击杀时是否额外召唤）

**OverrideFindTarget / OverrideAttackRange 委托**：
```csharp
public System.Func<CardUnit, CardUnit> OverrideFindTarget;   // 自定义索敌逻辑
public System.Func<CardUnit, float> OverrideAttackRange;      // 自定义攻击范围
```

**性能约束**：所有 Update 循环扫描使用 `Physics2D.OverlapCircleNonAlloc` + 预分配 64 槽缓存。盾墙检测使用静态缓存列表 `_shieldWallUnits`（Awake 注册，OnDestroy 注销），只遍历盾墙单位。所有状态（撕裂层数、减速计时器）存储在 `CardUnit` 实例字段，对象池回收时自动清零。

### 9.5 牌型生成规则（BattleManager）

`BattleManager.cs` 的 `Spawn*` 方法只负责从 `SpawnPool` 读取预制体并实例化，不做任何硬编码数值 Buff。所有特殊效果通过预制体上挂载的 `UnitPassives` 组件实现。

| 牌型 | BattleManager 做的事 | 效果来源 |
|:---|:---|:---|
| Single/Pair/Triple | 按点数实例化预制体 | 预制体自身 |
| 三带一 | 主体 3 个 + 诱饵 1 个（SpawnPool._baitPrefabs）| 诱饵的 UnitPassives |
| 三带二 | 主体 3 个 + 骑兵 2 个（SpawnPool._cavalryPrefabs）| 骑兵的 UnitPassives |
| 顺子 5+ | 每张牌一个兵 + 链式加速参数 | BattleManager 攻速/移速倍率 |
| 连对 | 连对预制体（SpawnPool._consecutivePairPrefabs）| 预制体自身 |
| 炸弹 | 炸弹预制体（SpawnPool._bombPrefabs）| 预制体自身 |
| 四带二 | 坦克预制体 + 无人机预制体（SpawnPool._tankPrefabs/_dronePrefabs）| 预制体自身 |
| 飞机 | 轰炸机预制体（SpawnPool._bomberPrefabs）+ 同路线地毯轰炸 | BattleManager 参数 |
| 王炸 | 英雄 5 选 1，属性覆盖（预制体自包含） | BattleManager + HeroUnitConfig |

`CardTypePassives.cs` 已删除，其所有功能已迁移至 `UnitPassives.cs`（16 种通用被动）和 `SpawnPool.cs`（预制体映射）。

### 9.6 兵种对象池

```csharp
// UnitFactory 按预制体引用分池
// Spawn() → unit.Initialize() → healthBar.Initialize(unit)
// Despawn() → unit.OnPoolDespawn()
//   → gameObject.SetActive(false) → UnitHealthBar.OnDisable() 解绑 + 重置
// OnPoolSpawn() → gameObject.SetActive(true) → 血条重新绑定
```

**OnPoolDespawn 动画重置**：对象池回收时重置所有 Trigger 和 Bool 参数，
强制回到 Idle 状态，防止状态残留导致动画异常。

### 9.7 VisualCenter 使用基准地图

`VisualCenter` = `_collider.bounds.center`（碰撞箱 bounds 中心），`_collider` 为 null 时回退 `transform.position`。
`_collider` 仅在 `Initialize()` 中赋值，编辑器预览时可能为 null。

| 系统 | 圆心/基准 | 说明 |
|:---|:---|:---|
| 攻击范围判定 | `VisualCenter`（仅作 ClosestPoint 查询起点） | 实际距离为边缘到边缘（`GetUnitEdgeDistance`） |
| 攻击范围 Gizmo | `bounds.center`（实时取碰撞箱，有兜底） | `CardUnit.Animation.cs`：`_collider != null ? _collider : GetComponentInChildren` |
| 点杀搜索 | `_owner.VisualCenter` | `Physics2D.OverlapCircle` 圆心 + `GetUnitEdgeDistance` 边缘判定 |
| 点杀 Gizmo | `owner.VisualCenter` | `UnitPassives.cs`：`_collider` 未初始化时回退 `transform.position` |
| 被动技能 AoE | `_owner.VisualCenter` | 人海/君王光环/减速光环/震波/死爆/死燃/嘲讽/骑兵追击 |
| 被动 Gizmo | `owner.VisualCenter` | 编辑器预览时 `_collider` 未初始化 → 回退 `transform.position`（停在预制体中心不动） |
| 溅射攻击 | `col.ClosestPoint(_owner.VisualCenter)` | **圆心在碰撞箱边缘**：目标碰撞箱上离攻击者最近的点（查询起点从 transform.position 修正为 VisualCenter） |
| 英雄被动（神射/术士/灵骑） | `unit.VisualCenter` / `owner.VisualCenter` | OverlapCircle 扫描圆心 |
| 三人组伤害分担 | `damaged.VisualCenter` / `unit.VisualCenter` | 距离判定双端 |
| 轰炸机伤害 | `bomber.VisualCenter` | OverlapCircle 扫描圆心 |
| 召唤物生成 | `_owner.VisualCenter` / `victim.VisualCenter` | 定期召唤 + 击杀召唤位置 |
| BOSS 技能 | `_owner.VisualCenter` | AOE 伤害/眩晕/击退/冲刺方向/VFX 生成 |
| BOSS 技能 Gizmo | 无 | BossSkillSystem 没有 OnDrawGizmos |
| 击退方向 | `enemy.VisualCenter - _owner.VisualCenter` | 所有击退/推离向量 |
| 伤害飘字 | `unit.transform.position + offset` | **基于 transform.position**，非 VisualCenter |
| 弹道生成 | `_firePoint ?? transform.position` | 基于 firePoint 或 transform.position |
| 兵种生成 | `SpawnPoint.position` / `MapController.GetSpawnPosition` | 基于 transform.position |
| IBuildingTarget.LogicCenter | `VisualCenter` | 建筑逻辑中心 = 碰撞箱中心 |

**编辑器预览差异**：`CardUnit.Animation` 的 Gizmo 有 `GetComponentInChildren<Collider2D>()` 兜底，即使 `_collider` 为 null 也能正确跟随碰撞箱。`UnitPassives` 的 Gizmo 依赖 `owner.VisualCenter`，`_collider` 为 null 时回退 `transform.position`，导致编辑器中被动 Gizmo 可能停在预制体原点。**运行时无差异**（`Initialize()` 已赋值 `_collider`）。

### 9.8 统一 Buff 系统（属性修改）

`CardUnit.cs:196-247` — 命名 Buff + 从基础值乘法叠加。

```csharp
public struct StatBuff
{
    public float AtkIntervalMult;  // 攻击间隔乘数（1.0 = 无影响）
    public float MoveSpeedMult;    // 移速乘数
    public float HpMult;           // HP 乘数
    public float AtkMult;          // ATK 乘数
    public float RangeMult;        // 射程乘数
}

// 应用 Buff（同名覆盖，异名乘算）
public void ApplyBuff(string buffId, StatBuff buff);
public void RemoveBuff(string buffId);
```

**叠加规则**：
- 每个 Buff 有唯一 `buffId`，**同名 BuffId 覆盖**（后到的替换先到的）
- **异名 Buff 乘法叠加**：从 `_baseStats`（首次 ApplyBuff 时快照）重新计算
- 重算公式：`最终值 = 基础值 × buff1.X × buff2.X × buff3.X × ...`

**举例**：基础移速 10，冲锋 Buff(`"charge"`, ×1.3) + 减速 Buff(`"slow_aura"`, ×0.7)：
```
10 × 1.3 × 0.7 = 9.1
```

如果再叠加第二个减速源用不同 buffId（如 `"boss_slow"`, ×0.8）：
```
10 × 1.3 × 0.7 × 0.8 = 7.28
```

但当前减速光环统一使用 `"slow_aura"`，后到的覆盖先到的，不会叠加。

**当前使用的 BuffId**：

| buffId | 来源 | 效果 |
|:---|:---|:---|
| `"charge"` | UnitPassives 冲锋 | MoveSpeed × chargeSpeedMultiplier |
| `"slow_aura"` | UnitPassives 减速光环 / BossSkillSystem | MoveSpeed × (1 - slowPercent) |
| `"spirit_rider"` | BattleManager 灵骑光环 | MoveSpeed × spiritRiderMoveSpeedBonus |

### 9.9 受伤计算流水线（TakeDamage）

`CardUnit.Combat.cs:425-499` — **严格串行，每一步的输出是下一步的输入**：

```
原始伤害 rawDamage
    │
    ├─ 1. 真实伤害（DamageType.True）？ → 直接扣血，跳过一切
    │
    ├─ 2. 屏障层（ShieldBlocks > 0）？ → 消耗一层，吸收全部伤害，结束
    │
    ├─ 3. 盾墙减免 → rawDamage × ∏(1 - 盾墙减伤)  ← 多个盾墙单位各自乘一次
    │
    ├─ 4. 伤害减免（DamageReduction）→ rawDamage × (1 - DamageReduction)  ← 如守护者 30%
    │
    ├─ 5. 伤害吸收（DamageAbsorbRemaining）→ 扣除护盾池（诱饵护盾/帝王盾）
    │
    ├─ 6. 分担伤害（ShareRedirected）→ 用 SharedDamageOverride 替代（主目标 60%，其他各 20%）
    │
    ├─ 7. 撕裂易伤 → rawDamage × (1 + 层数 × 每层比例)  ← 默认 +5%/层，上限 5 层
    │
    └─ 8. finalDamage → 扣血 → 死亡判定
```

**举例**：100 伤害打一个有 2 层撕裂(5%/层)、30% 守护者减伤、附近有 1 个 20% 盾墙的单位：
```
100 × (1 - 0.2) × (1 - 0.3) × (1 + 2 × 0.05)
= 100 × 0.8 × 0.7 × 1.1
= 61.6
```

如果附近有 2 个盾墙单位（各 20%）：
```
100 × (1 - 0.2) × (1 - 0.2) × (1 - 0.3) × (1 + 2 × 0.05)
= 100 × 0.8 × 0.8 × 0.7 × 1.1
= 49.28
```

**关键区别**：
- **速度 Buff**：同名覆盖，异名乘算，从基础值重算（`RecalculateStats`）
- **减伤**：串行相乘（盾墙 → 减免 → 吸收），不存在覆盖问题
- **撕裂增伤**：最后一步乘算，在所有减伤之后应用
- **真实伤害**：跳过全部减伤/吸收/撕裂
- **屏障**：吸收整击（不论伤害量），优先级最高（仅次于真实伤害）

---

## 10. AI 对手系统

### 10.1 BuildingAI

```csharp
public class BuildingAI : MonoBehaviour {
    public CardHand Hand { get; set; }
    public EconomySystem Economy { get; set; }
    public void Initialize(CardHand, EconomySystem, BattleManager, CardDeck, int maxSelection, float drawInterval);
}
```

### 10.2 AI 行为规则

| 规则 | 行为 |
|:---|:---|
| 出牌频率 | 每 4 秒判定一次 |
| 选牌策略 | 枚举合规牌型，选最贵且付得起的；**性能优化**：k 从大到小遍历（高价优先），上限 1000 次评估防帧率尖刺 |
| 选路（农民） | 固定路线（RouteGroup 配置） |
| 选路（地主） | 路线压力评估（`ChooseLane()`），根据敌方存活兵种金币权重 + 玩家路线权重 + 防守需求选择最优路线 |
| 自动摸牌 | 地主 5s / 农民 6s |
| 经济增长 | 每分钟 +1g/s |
| 领域决策 | 集成 DomainSystem，地主 AI 在手牌充足时激活要不起领域，农民 AI 在可反制时激活反制护盾 |

---

## 11. UI 层规范

### 11.0 阵营 UI 可见性管理

GameBootstrapper 在初始化完成后根据玩家身份自动隐藏不需要的 UI 组件：

| 身份 | 隐藏的 UI | 保留的 UI |
|:---|:---|:---|
| 地主 | `launchTubeUI`, `tempSlotUI`, `teammateTempSlotUI` | `laneArea`（分路系统）, 路线选择 UI |
| 农民 | `laneArea`, 路线选择 UI（prev/nextRouteButton, routeLabel, routeIndicator） | `launchTubeUI`, `tempSlotUI`, `teammateTempSlotUI` |

**实现位置**：`GameBootstrapper.Start()` Step 6b 末尾

```csharp
if (playerIsLandlord) {
    launchTubeUI?.gameObject.SetActive(false);
    tempSlotUI?.gameObject.SetActive(false);
    teammateTempSlotUI?.gameObject.SetActive(false);
} else {
    laneArea?.SetActive(false);
    handArea?.SetRouteUIVisible(false);  // 农民不需要路线选择
}
```

**Inspector 配置**：`laneArea` 字段需手动拖入分路 UI 对象。`teammateTempSlotUI` 需在场景中创建第二个 TempSlotUI 实例并拖入。

### 11.1 手牌交互流程

```
用户操作 → CardWidget.OnClick()
           → SelectionValidator.Toggle(card)
                → 已选中 → 取消选中
                → 未选中 + 未达上限 → 加入缓冲区
                → 未选中 + 已达上限 → 返回 false 拒绝（不可静默丢弃）
                → 实时牌型检测 + OnSelectionChanged
           → Toggle 返回 false 时为拒绝态：
                → CardWidget.PulseRejection()（红色闪烁 + 水平抖动 0.3s）
                → validationLabel 显示 "已达上限 (N 张)"
选牌完成 → OnDeployClicked()
           → OnPlayRequest(cards, result, routeGroup)
           → GameBootstrapper 扣费 + 移除手牌 + 弃牌堆记录 + 刷新记牌器 + 部署兵种
选牌刷新 → RefreshHand() 保持选中状态（RestoreSelection）
```

### 11.2 路线标签

路线切换通过 `RouteGroup.PrevRoute()` / `NextRoute()` 实现。UI 显示当前路线索引和名称，单路线时禁用切换按钮：

```csharp
routeLabel.text = _routeGroup?.CurrentRouteName ?? "无路线";
```

### 11.3 自动摸牌按钮

| 身份 | 自动摸牌间隔 | 金币抽牌费用 |
|:---|:---|:---|
| 地主 | 5s | 10g |
| 农民 | 6s | 12g |

### 11.4 3 换 1 回收系统

玩家将手牌拖入弃牌桶 → 累计 3 张自动触发抽牌。

```csharp
// HandArea.OnCardDiscardRequested()
_boundHand.RemoveRange(new[] { widget.BoundCard });
_deck?.Discard(widget.BoundCard);
_discardCount++;
if (_discardCount >= 3 && _deck != null && !_boundHand.IsFull)
{
    _discardCount = 0;
    var newCard = _deck.Draw();
    _boundHand.Add(newCard);
}
```

### 11.5 记牌器（CardCounterUI）

实时显示弃牌堆中各点数的已出张数（如 "0/4"、"4/4"）：
- 同点数 4 张全出 → 变灰 + "已断"标记
- 底部显示牌堆剩余张数

### 11.6 摄像机控制（CameraController）

WASD/方向键 + 鼠标边缘滚动移动，滚轮缩放 orthographicSize，支持边界限制。

### 11.7 伤害飘字系统

**组件**：`DamageFloatText` + `FloatingTextPool`

- World Space TextMeshPro，3D 渲染
- `FloatingTextPool` 订阅 `BattleManager.OnUnitSpawned`，自动挂钩所有单位
- 颜色规则：物理=白色，特殊=紫色，真实=橙色，大伤害（≥50）红色加粗
- 动画：向上浮动 1.5m + 渐隐，1 秒后自动回池
- Pool 大小：20

### 11.8 基地血条

- 基地本体是挂载了 `CardUnit(_isBuilding=true)` 的实体（实现 IBuildingTarget）
- 复用 `UnitHealthBar` 组件（与兵种共用同一血条系统）
- 血量读取：`CardUnit.Stats.HP` / `CardUnit.MaxHP`
- 阵营颜色：`Identity` 枚举判断敌友

### 11.10 对局计时器（GameTimerUI）

**组件**：`GameTimerUI`（UI/HUD/）

- 显示游戏已运行时间（正计时 `00:00` → `01:00` → ...）
- 正常阶段白色文字，骤死期红色文字
- 由 `GameBootstrapper` 注入 `GameStateMachine` 引用
- 自动查找兜底：`Initialize()` 未调用时通过 `FindFirstObjectByType` 自动获取

### 11.11 领域 UI（DomainUIController）

合并原 `DomainOverlay` 和 `DomainCoolDownUI`，单一组件管理所有领域 UI。

**反击按钮**（农民视角）：始终可见（与 overlayGroup alpha 独立），通过 `interactable` + `ButtonEffect` 状态 + 文字三者统一管理。

| 状态 | ButtonEffect | 按钮文本 | 可点击 |
|:---|:---|:---|:---|
| 领域未开启 | cooldown | "反击（等待领域）" | 否 |
| 领域已激活 | default | "反击" | 是 |
| 已点击待激活 | pending | "反制待激活" | 是（可取消） |
| 反制护盾生效中 | cooldown | "反制生效中" | 否 |
| 冷却中 | cooldown | "冷却 Xs" | 否 |

**关键约束**：`counterButton` 必须是 `overlayGroup` 的**同级**而非子物体，否则 overlay 的 `alpha=0` 会使其不可见。

### 11.9 点选信息面板（已实现 ✅）

**组件**：`UnitSelector`（Gameplay/Entities） + `UnitInfoPanel`（UI/Panels）

**UnitSelector**：挂在兵种/建筑上，`Physics2D.OverlapPoint` 检测左键点击，选中/取消切换。

**UnitInfoPanel**：世界空间 Canvas，跟随目标，面向摄像机。显示字段：
- 名称、HP/MaxHP、ATK、攻击间隔、DPS、移速、Range
- 阵营、兵种行为（已启用的被动列表）
- 实时刷新：订阅 `CardUnit.OnHPChanged` 更新血量
- 自动关闭：目标死亡时自动销毁

---

## 12. Config 配置表规范

### 12.1 经济配置（EconomyConfig）

```csharp
public float initialGold = 50f;
public float farmerBaseIncome = 5f;
public float landlordBonusIncome = 2f;
public float incomeStepPerMinute = 1f;
public float suddenDeathMultiplier = 2f;
public float baseCostC3 = 10f;           // 点数3的基础市值
public float costGrowthRate = 1.17f;     // Cost 增长系数
```

### 12.2 基地配置（CardUnit 建筑模式）

基地现在是挂载了 `CardUnit(_isBuilding=true)` 的实体，不再有独立的 BaseConfig。

| 字段 | 说明 |
|:---|:---|
| `_isBuilding` | 标识是否为建筑 |
| `_hp` | 生命值（预制体 Inspector 配置） |
| `_regenPerSecond` | 建筑每秒回血量 |
| `_baseScale` | 基础缩放 |

**初始化**：`InitBuildingHP(float hp)` 方法，从 `MaxHP`（即 `_hp`）读取血量。
**阵营身份**：通过 `CardUnit.IsLandlord` 属性判断。
**默认血量**：如果 `_hp` 未设置（≤0），默认 1000f。

### 12.3 英雄配置（HeroUnitConfig）

**v2.0 架构（预制体自包含）**：

英雄的生成完全取决于其预制体，不再依赖外部 HeroConfig ScriptableObject。

```csharp
// 挂载到英雄预制体上
public class HeroUnitConfig : MonoBehaviour {
    public string heroName;
    
    // 觉醒属性倍率
    public float awakenHpMultiplier = 2.0f;
    public float awakenAtkMultiplier = 2.0f;
    public float awakenMoveSpeedMultiplier = 1.2f;
    public float awakenRangeMultiplier = 1.3f;
    
    // 被动技能组件
    public HeroPassiveBase[] passives;
}

// 被动技能基类
public abstract class HeroPassiveBase : MonoBehaviour {
    public abstract void Apply(CardUnit unit, bool awakened);
    public abstract void Remove(CardUnit unit);
}
```

**优势**：
- 每个英雄预制体自包含所有配置
- 被动技能模块化，易于扩展
- 联机模式下支持多玩家选择不同英雄
- 移除对全局 HeroConfig 的依赖

**已实现的被动技能组件**：
- `BlademasterPassive`：剑圣被动（攻击时概率造成额外伤害）
- `GuardianPassive`：铁卫被动（嘲讽源 + 伤害减免）
- `SharpshooterPassive`：神射被动（优先攻击血量最低的敌人）
- `WarlockPassive`：术士被动（攻击时溅射范围伤害）
- `SpiritRiderPassive`：灵骑被动（友军光环：攻速 + 移速加成）

**使用方法**：
1. 在英雄预制体上添加 `HeroUnitConfig` 组件
2. 在 `passives` 数组中添加需要的被动技能组件
3. 在 Inspector 中配置被动技能参数

### 12.4 兵种配置

删除 `SoldierStatsConfig`（已废弃）。兵种属性由预制体 Inspector 字段驱动（`CardUnit._hp`, `_atk` 等），在 `Initialize()` 中组装为 `SoldierStats` struct。

### 12.5 SpawnPool 预制体映射

```csharp
// 按 Rank 3~2 顺序（13 槽），空槽回退 _rankPrefabs
_rankPrefabs[13]:     基础兵种
_baitPrefabs[13]:     三带一诱饵
_cavalryPrefabs[13]:  三带二骑兵
_consecutivePairPrefabs[13]: 连对
_bombPrefabs[13]:     炸弹
_tankPrefabs[13]:     四带二坦克主体
_dronePrefabs[13]:    四带二无人机挂件
_bomberPrefabs[13]:   飞机轰炸机
```

所有特殊牌型预制体按点数（3~2）独立配置，不再按分段（5 型）映射。未填槽位自动回退到 `_rankPrefabs[rank]`。

### 12.6 CSV 数据管线

集中管理所有游戏数值，支持 CSV 双向同步。菜单入口：`Tools → 配置数据管理`。

**CSV 文件位置**：`Assets/StreamingData/Config/`

| 文件 | 内容 | 数据来源 |
|:---|:---|:---|
| `Units.csv` | 兵种数值（HP/ATK/移速/攻速/射程等） | 各预制体 CardUnit 的 [SerializeField] 字段 |
| `Heroes.csv` | 英雄配置（觉醒倍率） | HeroUnitConfig 组件（挂载到英雄预制体） |
| `Economy.csv` | 经济参数（初始金币/回金速度/费用公式） | EconomyConfig ScriptableObject |
| `Bidding.csv` | 叫分参数（时长/AI策略/超时处理） | BiddingConfig ScriptableObject |
| `Levels.csv` | 关卡配置（名称/场景/难度/解锁状态） | LevelConfig ScriptableObject |

**导入流程**：`CSV → ScriptableObject → Prefab 字段`
**导出流程**：`Prefab 字段 → ScriptableObject → CSV`

**关键组件**：
- `CsvIO`（Editor）：CSV 解析/生成，支持引号字段和 UTF-8 BOM
- `ConfigImportExport`（Editor）：导入导出 Editor 窗口
- `UnitStatsConfig`（Config）：兵种数值汇总 ScriptableObject，作为 CSV 和预制体的中间层

---

## 13. 网络联机系统

### 13.1 架构

**网络模型：Fusion Host Authority（Host 权威）**

当前代码以 `FusionGameManager.World` / `State` 作为 `[Networked]` 状态入口。Client 通过 Fusion RPC 提交输入请求；State Authority 校验并推进游戏逻辑，Fusion 同步网络化状态。具体状态是否完整覆盖所有 UI、战斗和断线流程，仍需多客户端运行验证。

**三条不可破坏不变量（顶层约束，高于一切实现细节）：**

1. **权威状态唯一性（Single Authority State）** — 游戏逻辑状态（牌堆/手牌/出牌/叫分/胜负）只能由 Master 计算并更新。Client 不得推导、修正或模拟状态。Client 只做：显示（Render）、输入（Request）
2. **事件是传输层，不是真相层（Event ≠ State）** — 网络事件只代表状态变化请求或结果通知。不能依赖"是否收到某个事件"判断状态，不能依赖事件顺序恢复状态。State = Master Snapshot，Event = State transition notification
3. **任何客户端必须可以无历史恢复（Join-time Snapshot）** — 客户端状态不能依赖事件历史。进入房间后只需获取当前快照即可直接进入 Running，不需要 replay event log、等 START_MATCH、等 PLAYER_READY 链

**判断标准**：如果某个客户端丢掉所有网络事件，它还能回到正确状态吗？不能 → 一定会出 bug。

**Network Rules（7 条铁律）：**

1. **Master is authoritative state writer** — 所有游戏状态变更由 Master 决定
2. **Master applies state immediately** — Master 在广播 RESULT 前先本地执行状态变更，保证后续逻辑可访问最新状态。未来 AI 不得改为"必须等 RESULT 回来再执行"
3. **Clients only mirror RESULT events** — Client 收到 RESULT 后更新本地表现。Client 禁止从 REQUEST 事件推导游戏状态
4. **No client-side state mutation** — Client 不修改手牌/金币/HP 等游戏状态
5. **No dual simulation（ARCH-001）** — Client 禁止独立于 Master 运行游戏逻辑模拟。所有状态转换仅由 Master 产生。Client 与 Master 之间的状态偏差视为同步 bug，不视为合法的游戏分歧
6. **All protocol keys must be request/result paired** — 请求和结果使用独立 Key（如 `DRAW_CARD` / `DRAW_CARD_RESULT`、`PLAY_CARDS` / `PLAY_APPROVED`）。例外：内部同步事件（`MASTER_STATE_SYNC`、`HP_CORRECTION`）、心跳/状态快照无需配对
7. **UI state from Master snapshot, not local derivation** — `NetworkRemaining` 等 UI 显示状态由 Master 计算后通过 WorldState 同步，Client 直接使用，禁止本地推导。实现：FusionGameManager 维护 `_slotHandCards` + `WorldState.Game.DeckCount` → Client 读取 WorldState → CardCounterUI 显示

> 本节描述目标约束。它不代表所有客户端表现、恢复流程或跨场景桥接已经通过端到端验证。

### NETWORK EXECUTION GUARANTEE

本系统保证：

1. 存在且仅存在一个权威模拟（Master）
2. Client 是被动投影层（Passive Projection）
3. 任何 Client 逻辑不得影响权威状态
4. 所有游戏状态变更源自 Master
5. Client 与 Master 之间的状态偏差视为同步 bug，不视为合法的游戏分歧

```
OnlineLobbyController / NetworkBiddingManager
    ├── NetworkManager → INetworkService → FusionService → NetworkRunner
    └── FusionGameManager → [Networked] WorldState/State + RPC（Host 校验请求）

本地模拟路径（编辑器/测试代码自行创建服务，不等同于线上房间）：
LocalNetworkService → LocalNetworkHub（同一进程内直接路由）
```

### 13.2 接口设计

```csharp
public interface INetworkService
{
    // 连接
    void Connect();
    void Disconnect();
    bool IsConnected { get; }

    // 房间
    void CreateRoom(string roomCode, int maxPlayers);
    void JoinRoom(string roomCode);
    void JoinRandomRoom();
    void LeaveRoom();
    bool IsInRoom { get; }
    bool IsMasterClient { get; }
    string CurrentRoomName { get; }
    int CurrentPlayerCount { get; }
    int MaxPlayers { get; }

    // 玩家
    string LocalPlayerName { get; set; }
    string[] GetPlayerNames();
    void SetPlayerReady(bool ready);
    bool AreAllPlayersReady { get; }

    // 消息同步
    [Obsolete] // FusionService 中为空操作；由 Fusion RPC / Networked 状态替代
    void SendToAll(string key, object value);
    [Obsolete]
    void SendToMaster(string key, object value);
    [Obsolete]
    void SendToPlayer(int actorNumber, string key, object value);
    void SetRoomProperty(string key, object value);
    object GetRoomProperty(string key);

    // 场景同步
    void LoadScene(string sceneName);

    // 玩家标识
    int LocalActorNumber { get; }
    int[] GetPlayerActorNumbers();
    int GetActorNumberAtPosition(int position);

    // 事件
    event Action OnServerConnected;
    event Action OnConnectionLost;
    event Action<string> OnRoomCreateSuccess;
    event Action<string> OnRoomJoinSuccess;
    event Action<string> OnRoomJoinError;
    event Action<string> OnPlayerJoined;
    event Action<string> OnPlayerLeft;
    event Action OnAllPlayersReady;
    event Action OnPlayerReadyChanged;
    event Action<string, object, int> OnCustomEvent;
    event Action OnMasterSwitched;
}
```

`OnCustomEvent` 和定向消息目前只在 `LocalNetworkService` 的进程内模拟中有消息路由。Fusion gameplay 使用 `FusionGameManager` 的类型化 RPC 和 `[Networked]` 状态，不能依赖旧的 `SendTo*` 事件通道。

### 13.3 Core 层兼容

Core/ 是纯 C# 且逻辑确定性（Deterministic），联机时只需要服务端生成 Random Seed → 同步给所有客户端。

### 13.4 断线重连与 Host 迁移

当前 `FusionService` 处理 `OnDisconnectedFromServer` / `OnConnectFailed` 回调并更新连接状态，但没有应用恢复时自动重连、自动重入房间或恢复会话的实现。上线前需要设计并测试重连策略。

`FusionService.OnHostMigration` 当前为空实现。Host 迁移、WorldState 恢复和客户端重新加入均未由本项目代码实现；不能假定 Fusion 会自动迁移并恢复游戏状态。

### 13.5 网络协议层（NetworkProtocol）

`NetworkProtocol` 是纯静态工具类，集中管理所有联机事件的 Key 定义和数据序列化：

```csharp
public static class NetworkProtocol
{
    // 事件 Key（叫分/出牌/抽牌/领域/状态校验/房间管理）
    public const string BID_TURN, BID_ACTION, BID_RESULT;
    public const string GAME_INIT, PLAY_CARDS, PLAY_APPROVED, PLAY_REJECTED;
    public const string DRAW_CARD, DRAW_CARD_RESULT, DOMAIN_ACTIVATE, COUNTER_ACTIVATE;
    public const string STATE_CHECKSUM, GAME_END, PLAYER_LEFT;
    public const string HP_CHECKSUM, HP_CORRECTION, UNIT_DIED;
    public const string GOLD_UPDATE;
    public const string PLAYER_READY;
    public const string DOMAIN_PENDING, COUNTER_PENDING;
    public const string ADD_AI, REMOVE_AI, KICK_PLAYER;
    public const string CARD_TRANSFER, CARD_ARRIVE, CARD_TAKE;  // 飞筒传牌
    public const string MASTER_STATE_SYNC;  // 遗留协议常量；不是当前 Fusion 的定时广播通道

    // 序列化工具
    public static int[] SerializeCards(Card[] cards);
    public static Card[] DeserializeCards(int[] indices, CardDeck deck);
    public static object[] SerializeCardTypeResult(CardTypeResult r);
    public static CardTypeResult DeserializeCardTypeResult(object[] data);

    // 玩家槽位
    public static int GetPlayerSlot(int actorNumber, int[] sortedActorNumbers);
}
```

**传输边界**：`NetworkProtocol` 中保留的 Key/序列化代码属于旧的通用消息协议，仍可用于本地模拟或兼容逻辑。Fusion 玩法同步使用 `FusionGameManager` 上的强类型 RPC 和 `[Networked]` 属性；`FusionService.SendTo*` 是空操作，不得作为 Fusion 消息通道使用。

### 13.6 已实现功能

| 功能 | 状态 |
|---|---|
| 模块 | 代码现状 |
|---|---|
| 网络服务入口 | `NetworkManager` 持有 `INetworkService`；默认查找或创建 `FusionService` |
| Fusion 会话 | `FusionService` 使用 Host 创建会话、Client 加入会话，并转发 Runner 回调 |
| Fusion 游戏状态 | `FusionGameManager` 声明 `[Networked] World` / `State`，在 `FixedUpdateNetwork` 中推进 Host 逻辑 |
| 玩家请求 | `RpcSubmitBid`、`RpcDrawCard`、`RpcPlayCard` 等 RPC 将输入交给 State Authority 校验 |
| 本地模拟 | `LocalNetworkService` 与 `LocalNetworkHub` 提供同进程的玩家/准备状态/消息路由模拟 |
| 兼容消息 API | Fusion 下 `SendToAll`、`SendToMaster`、`SendToPlayer` 不传输消息；已有调用应迁移到 Fusion RPC/网络状态 |

此表只记录可从代码确认的实现，不代表多人对局通过端到端验收。房间切换、所有 UI 同步和断线场景需实际多客户端测试。


### 13.7 Fusion 游戏管理器

`FusionGameManager` 是 `NetworkBehaviour`，在网络对象 Spawn 后初始化世界状态和战斗系统。`World` 与 `State` 标记为 `[Networked]`；State Authority 在 `FixedUpdateNetwork` 驱动叫分、出牌、摸牌、领域/传牌和 AI 处理。

客户端提交输入的代表性 RPC：`RpcSubmitBid`、`RpcDrawCard`、`RpcPlayCard`、`RpcSubmitDomain`、`RpcSubmitTransfer`。Host 通过 `HasStateAuthority` 和 `TryResolveRpcSlot` 验证请求来源，再修改权威状态。私有手牌使用 `RpcRequestPrivateHand` / `RpcReceivePrivateHand` 定向同步；这与广播任意 Key/Value 事件不同。

```
UI 输入 → FusionGameManager RPC → Host 校验 → WorldState 更新
                                    ├→ Fusion 同步 [Networked] 状态
                                    └→ RPC/本地事件通知视图更新
```

`UnitSyncManager`、`ViewBinder`、`HandView`、`GoldView` 和 `CardUnitView` 负责不同的视图/单位呈现职责。代码中存在这些同步路径，但仍需在真实 Host + 多 Client 场景验证手牌、经济、单位生命周期、结算和重连的一致性；不要依据旧版 `MASTER_STATE_SYNC` / `NetworkGameManager` 说明推断它们仍是当前 Fusion 数据流。

### 13.8 历史网络同步方案记录（非当前实现）

> 以下内容保留自 v9.3，用于追溯旧设计和排查思路。它描述的是旧版通用消息/同步方案，不是当前 Fusion 实现清单；其中列出的类、字段、Key 和日志标签须先在当前代码中核实后才能用于修改或验收。

旧设计曾记录如下数据流：

```
玩家出牌 → HandArea → SetPlayCardInput() → IntentBuffer
  → Host 校验手牌/金币/领域封印 → 执行出牌 → 同步状态并通知视图

飞筒传牌 → LaunchTubeUI → IntentBuffer → 校验手牌 → 执行传牌 → 更新双方手牌状态
摸牌 → Host 扣减牌堆并增加对应玩家手牌
```

旧版方案还曾规划由 Master 跟踪远端玩家的手牌、牌堆和经济，在断线转 AI 时沿用该槽位已跟踪的数据；出牌验证由 Master 执行，避免信任客户端提交的金币或领域状态。旧记录中的 `_slotHands`、`_slotDecks`、`_slotEconomies` 等名称仅代表当时的设计/实现记录，不应据此认定它们当前仍承担权威状态存储。

以下是历史排查关注点，保留为复核清单而非已验证的现行保障：

| 历史关注点 | 曾记录的处理思路 |
|:---|:---|
| 玩家就绪与自动摸牌竞态 | 玩家确认就绪后再接受该槽位的摸牌请求 |
| 金币权威 | Host 使用权威经济状态，不采纳客户端报告值 |
| 领域封印校验 | Host 独立校验出牌是否符合领域规则 |
| 状态版本乱序 | 通过版本号拒收过期状态广播 |
| 请求/结果重入 | 请求 Key 与结果 Key 分离，避免结果再次被当成请求处理 |
| 网络追踪 | 记录玩家就绪、出牌、摸牌、状态同步及拒绝原因 |

历史日志搜索词包括 `PLAYER_READY_SEND`、`PLAYER_READY_RECV`、`PLAY_CARDS_RECV`、`PLAY_APPROVED`、`PLAY_REJECTED`、`DRAW_CARD_RESULT`、`DRAW_REJECTED_NOT_READY`、`STATE_SYNC` 和 `STATE_SYNC_STALE`。这些标签不保证仍由当前代码输出。

v9.3 中关于 `OnApplicationFocus` / `OnApplicationPause` 自动重连、`ReconnectAndRejoin()` 以及“Fusion 自动完成 Host 迁移并继承 WorldState”的说明已核对为不符合当前 `FusionService` 实现，因此没有作为可用流程恢复；当前状态见 §13.4。

---

## 14. 动画状态系统

### 14.1 三层参数解耦

动画参数分三层表达，互不冲突：

| 层 | 参数名 | 类型 | 用途 | 互斥？ |
|:---|:---|:---|:---|:---|
| 基础状态 | `State` | int | 0=Idle, 1=Walk, 2=Attack | 是 |
| 一次性触发 | 各 Trigger 名 | Trigger | 12 种特效（见下表） | 否，叠加触发 |
| 持续开关 | 各 Bool 名 | bool | 3 种持续状态（见下表） | 否，开关切换 |

### 14.2 动画播放优先级

```
Any State → 特效状态（最高优先级，立即打断当前状态）
特效状态 → Idle（Has Exit Time，自动回退）
Idle ↔ Walk ↔ Attack（State 参数控制，最低优先级）
```

- Trigger 触发后立即切换到特效状态，播放完成后自动回到 Idle
- Bool 设为 true 后切换到持续状态，设为 false 后回到 Idle
- 多个 Trigger 同时触发时，按 Animator 窗口中的状态排序决定优先级

### 14.3 SimpleAnimator 字段配置

SimpleAnimator 组件支持 18 种动画片段配置（含 4 种 BOSS 技能）：

**基础动画（3 种）：**
| 字段 | 说明 | 匹配关键词 |
|:---|:---|:---|
| `idleClip` | 待机动画 | idle |
| `walkClip` | 行走动画 | walk |
| `attackClip` | 攻击动画 | attack |

**Trigger 特效动画（12 种）：**
| 字段 | 说明 | 匹配关键词 | 对应系统 |
|:---|:---|:---|:---|
| `deathClip` | 死亡 | death | —（始终可用，由 PlayDeathAnimCoroutine 触发） |
| `shockwaveClip` | 震波 | shockwave | UnitPassives `enableShockwave` |
| `splashClip` | 溅射 | splash | UnitPassives `enableSplash` |
| `stunHitClip` | 眩晕命中 | stunhit | UnitPassives `enableStunOnHit` |
| `kingAuraClip` | 君王光环 | kingaura | UnitPassives `enableKingAura` |
| `deathExplosionClip` | 死亡爆炸 | deathexplosion | UnitPassives `enableDeathExplosion` |
| `burnClip` | 燃烧 | burn | UnitPassives `enableBurnOnDeath` |
| `summonClip` | 召唤 | summon | UnitPassives `enableSummoner` |
| `dashClip` | 冲刺 | dash | BossSkillSystem `animTrigger="Dash"` |
| `bossSkill1Clip` | BOSS 技能 1 | bossskill1 | BossSkillSystem `animTrigger="BossSkill1"` |
| `bossSkill2Clip` | BOSS 技能 2 | bossskill2 | BossSkillSystem `animTrigger="BossSkill2"` |
| `bossSkill3Clip` | BOSS 技能 3 | bossskill3 | BossSkillSystem `animTrigger="BossSkill3"` |

**Bool 特效动画（3 种）：**
| 字段 | 说明 | 匹配关键词 | 对应 UnitPassives 开关 |
|:---|:---|:---|:---|
| `chargeClip` | 冲锋 | charge | `enableCharge` |
| `tauntClip` | 嘲讽 | taunt | `enableTaunt` |
| `shieldWallClip` | 盾墙 | shieldwall | `enableShieldWall` |

**匹配规则**：大小写不敏感，文件名包含关键词即可（如 `__idle_placeholder`、`Idle.anim`、`MyIdleClip` 均可匹配）

### 14.4 动画优先级（高 → 低）

| 优先级 | 类型 | 机制 | 说明 |
|:---|:---|:---|:---|
| 1 | 死亡保护 | `_isDying=true` 阻止所有新 Trigger | 只允许 Death/DeathExplosion/Burn |
| 2 | Any State Trigger | 立即打断当前状态 | 互相竞争，按添加顺序决定优先级 |
| 3 | Bool 持续状态 | Taunt/ShieldWall/Charge 开关切换 | 常驻覆盖基础状态 |
| 4 | Int 基础状态 | Idle(0)/Walk(1)/Attack(2) | 最低优先级 |

**Any State Trigger 内部顺序**（同时触发时，先添加的优先）：
```
Death > Shockwave > Splash > StunHit > KingAura > DeathExplosion > Burn > Summon > Dash > BossSkill1 > BossSkill2 > BossSkill3
```

**BossSkillSystem 保护机制**：
- `_isDying=true` → TriggerAnim 只允许 Death/DeathExplosion/Burn
- `_isCasting=true` → BossSkillSystem 跳过所有触发检查，不启动新技能
- `StunTimer>0` → Update() 直接 return，技能不启动
- `Invulnerable` → 只挡伤害，不影响动画

### 14.5 CardUnit 动画接口

```csharp
// 基础状态（互斥）
UpdateAnimatorState(0);  // Idle
UpdateAnimatorState(1);  // Walk
UpdateAnimatorState(2);  // Attack

// 一次性效果（叠加触发）
TriggerAnim("Charge");
TriggerAnim("Shockwave");
TriggerAnim("Splash");
TriggerAnim("StunHit");
TriggerAnim("KingAura");
TriggerAnim("DeathExplosion");
TriggerAnim("Burn");

// 持续效果（开关切换）
SetAnimBool("Taunt", true);      // 开启嘲讽
SetAnimBool("Taunt", false);     // 关闭嘲讽
SetAnimBool("ShieldWall", true); // 开启盾墙

// 动画播放速度控制
SetAnimSpeed(float speed);       // 1=正常，>1=加速，<1=减速
```

### 14.6 被动→动画映射

| 被动 | 开关字段 | 触发点 | 动画方法 | 参数类型 | 参数名 |
|:---|:---|:---|:---|:---|:---|
| 嘲讽光环 | `enableTaunt` | `Awake()` | `SetAnimBool` | Bool | Taunt |
| 盾墙线 | `enableShieldWall` | `Awake()` | `SetAnimBool` | Bool | ShieldWall |
| 冲锋一击 | `enableCharge` | `ApplyCharge()` | `TriggerAnim` | Trigger | Charge |
| 攻击眩晕 | `enableStunOnHit` | `OnAttack()` | `TriggerAnim` | Trigger | StunHit |
| 君王光环 | `enableKingAura` | `UpdateKingAura()` | `TriggerAnim` | Trigger | KingAura |
| 出场震波 | `enableShockwave` | `EmitShockwave()` | `TriggerAnim` | Trigger | Shockwave |
| 死亡爆炸 | `enableDeathExplosion` | `EmitDeathExplosion()` | `TriggerAnim` | Trigger | DeathExplosion |
| 死亡燃烧 | `enableBurnOnDeath` | `EmitBurn()` | `TriggerAnim` | Trigger | Burn |
| 溅射攻击 | `enableSplash` | `EmitSplash()` | `TriggerAnim` | Trigger | Splash |

### 14.7 Animator Controller 生成

使用菜单 `Tools → 生成兵种 Animator Controller` 生成，包含：
- 3 个基础状态：Idle、Walk、Attack
- 12 个 Trigger 特效状态：Death、Shockwave、Splash、StunHit、KingAura、DeathExplosion、Burn、Summon、Dash、BossSkill1、BossSkill2、BossSkill3
- 3 个 Bool 特效状态：Charge、Taunt、ShieldWall
- 所有 Transition 已配置条件和退出时间

**Boss 技能动画配置流程**：
1. 运行 `Tools → 生成兵种 Animator Controller`
2. 在 Boss prefab 的 Visual 子物体上 SimpleAnimator 组件拖入 Clip（dashClip/bossSkill1Clip 等）
3. 在 BossSkillSystem 的 `animTrigger` 字段填入对应 Trigger 名（"Dash"/"BossSkill1" 等）
4. 留空 `animTrigger` 则不播放动画，只执行效果

## 15. 性能约束（联机防御编码协议）

### 高频路径禁止操作

| 禁止 | 替代方案 |
|:---|:---|
| `FindObjectsByType<T>()` 在 Update/OnAttack/TakeDamage 中 | `Physics2D.OverlapCircleNonAlloc` + 预分配缓存数组 |
| `StartCoroutine` 用于计时 | `TimerQueue.Schedule` 或实体字段 `float timer` 在 Update 中驱动 |
| `static Dictionary<CardUnit, T>` 存储战斗状态 | 状态存在 CardUnit 实例字段中，`OnPoolDespawn()` 清空 |

### 当前已整改

| 位置 | 原代码 | 整改后 |
|:---|:---|:---|
| `ApplyShieldWallGlobal` | `FindObjectsByType` | `OverlapCircle` + ContactFilter2D + static `_shieldBuffer` |
| `ApplySwarm` | `FindObjectsByType` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `FindNearestTauntSourceFor` | `FindObjectsByType` | `OverlapCircle` + ContactFilter2D + instance `_tauntBuffer` |
| `UpdateKingAura` | `OverlapCircleAll` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `EmitShockwave` | `OverlapCircleAll` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `EmitDeathExplosion` | `OverlapCircleAll` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `EmitSplash` | `OverlapCircleAll` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `UpdateSlowAura` | `OverlapCircleAll` | `OverlapCircle` + ContactFilter2D + instance `_overlapBuffer` |
| `_overlapBuffer` | `private static` | 改为实例字段，消除多单位并发污染 |
| `_tauntBuffer` | `private static` | 改为实例字段，消除多单位并发污染 |
| `RestoreSpeedAfterDelay` | `StartCoroutine` | `CardUnit.SlowRestoreTimer` 在 Update 中驱动 |
| `static _tears Dictionary` | 静态字典 | `CardUnit.TearStacks` + `TearTimer` 实例字段 |
| `GetWorldRadius()` | 圆形近似补偿 | 删除，改为 `GetEdgeDistance()` + `BuildingCollider` O(1) 属性 |
| `RedistributeDamage` (B5) | `FindObjectsByType<CardUnit>` | `_allUnits` 缓存列表 |
| `BombingRunCoroutine` (B11) | `Physics2D.OverlapCircleAll` 每帧分配 | 实例 `_overlapCache[128]` + `OverlapCircleNonAlloc` |
| `BurnZone.Update` (B10) | `OverlapCircleAll` 每帧分配 | `_burnCache[64]` + `OverlapCircleNonAlloc` + 0.25s Tick 间隔 |
| `BuildingAI.MakeDecision` (B4) | 全组合枚举 C(20,5)≈38K 次/4s | k 从大到小 + 上限 1000 次评估 |
| `FindNearestEnemyBuilding` (B16) | `FindObjectsByType<CardUnit>` 在 OnUpdate 中每帧调用 | `_enemyBuildings` 缓存数组（`BattleManager._allBuildingTargets` 注入），O(m) 遍历建筑而非 O(n) 全场景扫描 |

---

## 16. 音频优先级管理系统

### 16.1 四通道优先级架构

`AudioManager` 维护 4 个 `AudioSource`，通过 `priority` 值控制通道满时的丢弃顺序：

| 通道 | priority | 用途 | 调用方法 |
|:---|:---|:---|:---|
| `uiSource` | 0 | 按钮点击/悬停、出牌、抽牌、领域激活/破封 | `PlayUI(clip, vol)` |
| `combatHighSource` | 64 | 反制护盾、死亡、死亡爆炸 | `PlayCombatHigh(clip, vol)` |
| `combatSource` | 128 | 受击（兼容旧 `PlaySFX` 接口） | `PlayCombat(clip, vol)` |
| `combatLowSource` | 200 | 攻击、技能（冲锋/震波/溅射等） | `PlayCombatLow(clip, vol)` |

**兼容接口**：`PlaySFX()` 和 `PlaySFXAtPosition()` 保留为包装方法，走 `combatSource`。

**SfxChannel 枚举**：用于指定音效播放通道
- `UI` → uiSource (priority=0)
- `CombatHigh` → combatHighSource (priority=64)
- `Combat` → combatSource (priority=128)
- `CombatLow` → combatLowSource (priority=200)

**BGM 系统**：
- `PlayBGM(AudioClip clip)` - 播放背景音乐
- `PlayBGMForScene(string sceneName)` - 根据场景名自动切换 BGM（`sceneBGMPairs` 配置）
- `StopBGM()` - 停止背景音乐

### 16.2 UnitAudio 并发限制

**按 Clip 分组计数**：`Dictionary<AudioClip, int> _clipCounts`（静态共享），每种音效独立计数，不同兵种互不干扰。

```csharp
// 每种音效最多同时 maxPerClipConcurrent 个（默认 3）
_clipCounts.TryGetValue(clip, out int clipCount);
if (clipCount >= maxPerClipConcurrent) return;
```

**配额归还**：`OnDisable` 时停止所有协程，遍历 `_pendingClips` 立即归还配额，防止兵种死亡导致配额泄漏。

### 16.3 屏幕可见性裁剪

`CanPlay()` 使用 `Camera.main.WorldToViewportPoint` 检查兵种是否在屏幕内（含 5% 边距）。屏幕外兵种不播放攻击/技能音效，受击和死亡音效不受此限制。

### 16.4 音效触发点汇总

| 音效 | 触发方法 | 通道 | 批量？ |
|:---|:---|:---|:---|
| 按钮点击/悬停 | `PlayButtonClick()` / `PlayButtonHover()` | UI | 立即 |
| 出牌/抽牌 | `PlayCardDeploy()` / `PlayDrawCard()` | UI | 立即 |
| 领域激活/关闭 | `PlayDomainActivate()` / `PlayDomainDeactivate()` | UI | 立即 |
| 领域/反制被破解 | `PlayDomainBroken()` / `PlayCounterShieldBroken()` | UI | 立即 |
| 攻击音效 | `UnitAudio.OnAttack` → `PlayCombatLow` | CombatLow | 立即 |
| 受击音效 | `UnitAudio.OnTakeDamage` → `PlayCombat` | Combat | 立即（入队前触发） |
| 死亡音效 | `UnitAudio.OnDeath` → `PlayCombatHigh` | CombatHigh | 批量结算时 |
| 背景音乐 | `PlayBGM()` / `PlayBGMForScene()` | BGM | 按场景自动切换 |
| 网络攻击音效 | `PlayAttackNetwork()` | CombatLow | 立即（Client 端） |
| 网络受击音效 | `PlayHitNetwork()` | Combat | 立即（Client 端） |
| 网络死亡音效 | `PlayDeathNetwork()` | CombatHigh | 批量结算时（Client 端） |

---

## 17. 伤害批量结算系统（DamageQueue）

### 17.1 设计动机

消除 `Update()` 执行顺序对战斗结果的影响：当两个兵种同帧互相攻击时，先执行 `Update()` 的兵种先造成伤害，可能导致后执行的兵种未出手就被击杀。

### 17.2 架构

```
Frame N:
  所有 CardUnit.Update() 执行
    → OnAttackHitFrame() → TakeDamage()
    → 批量模式：入队 DamageQueue.Enqueue(target, finalDamage)
    → 非批量模式：直接扣血（兼容关闭批量模式）
  CardUnit.LateUpdate()
    → DamageQueue.ProcessAll()
    → 遍历队列，调用 target.ApplyDamage(finalDamage)
    → 级联循环：死亡爆炸等触发的新伤害同帧处理（最多 10 轮）
```

### 17.3 关键方法

| 方法 | 位置 | 说明 |
|:---|:---|:---|
| `DamageQueue.Enqueue(target, finalDamage)` | DamageQueue.cs | 入队一条伤害 |
| `DamageQueue.ProcessAll()` | DamageQueue.cs | 帧末结算所有伤害，支持级联 |
| `DamageQueue.HasPending` | DamageQueue.cs | 检查当前帧是否有待结算的伤害 |
| `DamageQueue.Clear()` | DamageQueue.cs | 清空队列（场景切换时调用，防止残留引用） |
| `CardUnit.SetBatchDamageEnabled(bool)` | CardUnit.Combat.cs | 启用/禁用批量模式 |
| `CardUnit.TakeDamage(rawDamage, type)` | CardUnit.Combat.cs | 批量模式下入队，非批量模式下直接扣血 |
| `CardUnit.ApplyDamage(finalDamage)` | CardUnit.Combat.cs | 实际 HP 扣除 + 死亡判定，仅由 DamageQueue 调用 |

### 17.4 伤害流程

```
TakeDamage(rawDamage, type)
  ├─ 真实伤害 → 入队 rawDamage（跳过减伤）
  ├─ 屏障消耗 → 吸收整击
  ├─ 盾墙减免 → 乘法减少
  ├─ 伤害减免 → 百分比减少
  ├─ 伤害吸收 → 护盾池扣除
  ├─ OnTakeDamageEvent → 受击音效（立即触发，使用原始伤害）
  ├─ 分担重定向 → SharedDamageOverride 替代原始伤害（主目标 60%，其他各 20%，总计 100%）
  ├─ 撕裂易伤 → 最终倍率
  ├─ OnDamageCalculated → 飘字（含撕裂加成）
  └─ 批量模式 → DamageQueue.Enqueue(finalDamage)
```

### 17.5 级联处理

`ProcessAll()` 使用快照 + 循环处理：
- 每轮快照当前队列，清空后逐条 `ApplyDamage`
- `ApplyDamage` 可能触发死亡爆炸、伤害共享等，产生新入队伤害
- 下一轮处理新入队伤害，最多 10 轮防无限循环

### 17.6 启用方式

`GameBootstrapper.Start()` 中调用 `CardUnit.SetBatchDamageEnabled(true)`。

---

## 18. 传送飞筒系统

### 18.1 数据流

```
玩家拖牌到飞筒
  → LaunchTubeUI 检查队友暂存槽是否为空
  → 空: OnCardTransmitted(card) → GameBootstrapper 移除手牌 → teammateTempSlotUI.ReceiveCard(card)
  → 满: 飞筒变红，拒绝传送

队友 AI (BuildingAI.Update)
  → 检测暂存槽有牌 + 手牌未满 + 延迟到达
  → 自动加入手牌 + 清空暂存槽
```

### 18.2 暂存槽模式

| 模式 | 条件 | 行为 |
|:---|:---|:---|
| 交互模式 | `handArea != null`（玩家暂存槽） | 显示"加入手牌"/"弃置"按钮 |
| 只读模式 | `handArea == null`（队友暂存槽） | 隐藏按钮，仅展示牌面 |

### 18.3 AI 取牌延迟

`BuildingAI.takeCardDelay`（默认 0.5s）：暂存槽有牌后等待指定时间再取牌，让玩家能看到暂存槽中的牌。

### 18.4 联机模式

联机模式下飞筒系统通过 `FusionGameManager` 的 `IntentBuffer` 同步：
- 农民可用飞筒（地主隐藏），传牌通过 `IntentBuffer.AddTransfer()` → `ProcessTransfers()` 处理
- Host 验证手牌 → `ApplyTransfer()` 移动牌 → 同步 `WorldState.PlayerX.HandCount`
- `FindTeammateSlot()` 自动查找同阵营队友槽位

### 18.5 基地摧毁处理

- 玩家基地摧毁 → 清空玩家暂存槽 + 锁定飞筒
- 队友基地摧毁 → 清空队友暂存槽 + 锁定飞筒

---

## 19. 要不起领域系统（DomainSystem）

### 19.1 状态机

```
Idle → Pending（点击按钮）→ Active（出牌触发）→ Cooldown（到期）→ Idle
                ↓
         Counter Pending（农民点反击）→ Counter Shield Active → Cooldown → Idle
```

### 19.2 两阶段激活

- **领域**：点击按钮 → 设 Pending → 出非单张牌型时激活（单张不能激活）
- **反制护盾**：点击反击按钮 → 设 Counter Pending → 出能管上领域的牌型时激活
- **反击按钮 UI**：农民视角始终可见（§11.11），通过 `interactable` + `ButtonEffect` 状态统一管理

### 19.3 炸弹破封机制

| 场景 | 行为 | 音效 |
|:---|:---|:---|
| 被封印方出更大炸弹 | 领域关闭，不触发反制护盾 | `PlayDomainBroken()` |
| 被封印方出更大炸弹击破反制护盾 | 反制护盾关闭 | `PlayCounterShieldBroken()` |
| 炸炸弹管不上（同点数/更小） | 被封印，无法打出 | — |

### 19.4 封印规则（SealRuleEngine）

`GetUnsealedCards(hand, sealType)` 返回手中可反制指定牌型的卡牌：
- 火箭（双 Joker）：永远解封
- 炸弹：非炸弹领域时解封；炸弹领域时更大炸弹解封
- 同类型更高级牌型：解封（如领域是对 10，手中对 J 解封）

### 19.5 配置参数

| 参数 | 默认值 | 说明 |
|:---|:---|:---|
| `domainDuration` | 5s | 领域持续时间 |
| `domainCooldown` | 30s | 领域冷却 |
| `counterShieldDuration` | 2s | 反制护盾持续时间 |
| `counterShieldCooldown` | 45s | 反制护盾冷却 |

**动态配置方法**：
- `SetDomainDuration(float)` / `SetDomainCooldown(float)` - 动态修改领域参数
- `SetCounterShieldDuration(float)` / `SetCounterShieldCooldown(float)` - 动态修改反制护盾参数
- `ResetDomainCooldown()` / `ResetCounterShieldCooldown()` / `ResetAllCooldowns()` - 重置冷却（调试/测试用，当前无调用方）

**冷却剩余时间查询**：
- `DomainCooldownRemaining` - 领域冷却剩余时间（只读）
- `CounterShieldCooldownRemaining` - 反制护盾冷却剩余时间（只读）

---

## 附录 A：Prefab 结构清单

```
Assets/Prefabs/
├── Army/ArmyPrefabs/               # 兵种预制体
│   ├── Warrior.prefab              # 近战战士
│   ├── Archer.prefab               # 远程弓箭手
│   ├── Lancer.prefab               # 长枪兵
│   ├── Hero.prefab                 # 英雄（5 种类型，每个预制体挂载 HeroUnitConfig + 被动技能组件）
│   ├── King.prefab                 # 君王
│   ├── CrazyWolf.prefab            # 狼骑兵
│   ├── Wizard.prefab               # 法师
│   ├── DeathWizard.prefab          # 暗黑法师
│   ├── Bat.prefab                  # 蝙蝠（飞行单位）
│   ├── BossGiantBird.prefab        # BOSS：巨鸟（BossController + BuildingAI + SpawnPool + BossSkillSystem）
│   ├── BossSword.prefab            # BOSS：剑圣（BossController + BuildingAI + SpawnPool + BossSkillSystem）
│   └── PlaneTest.prefab            # 飞机/轰炸机
├── Buildings/TowerEntities/        # 建筑预制体
│   ├── FarmerA.prefab              # 农民基地
│   └── LandLord.prefab             # 地主基地
├── BulletAndHitEffect/             # 弹道特效
│   ├── Bullet/Arrow.prefab         # 箭矢投射物
│   ├── Fire.prefab                 # 火焰投射物
│   └── KingArea.prefab             # 君王领域投射物
├── UI/UIPrefabs/                   # UI 预制体
│   ├── GamePrefabs/
│   │   ├── GameCanvas.prefab       # 主游戏画布
│   │   ├── CardWidget.prefab       # 卡牌控件
│   │   ├── DamegeFloatText.prefab  # 伤害飘字（注意：文件名有拼写错误）
│   │   └── UnitInfoPanel.prefab    # 兵种信息面板
│   └── LevelCard.prefab            # 关卡选择卡片
└── VFX/
    └── ...（由 VFXManager 对象池管理）
```

---

## 20. 存档系统（SaveSystem）

基于 `PlayerPrefs` 的轻量存档，无文件 IO。

### 20.1 存储内容

| Key | 类型 | 说明 |
|:---|:---|:---|
| `Save_Gold` | float | 累计金币 |
| `Save_FirstWin` | int (0/1) | 是否已有首次胜利 |
| `Save_GamesPlayed` | int | 总对局数 |
| `Save_GamesWon` | int | 胜利次数 |
| `Codex_{id}` | int (0/1) | 图鉴条目解锁状态（按 ID 存储） |

### 20.2 生命周期

```
GameBootstrapper.Start()  → SaveSystem.Load()（读取存档到内存）
GameBootstrapper.onGameEnded → SaveSystem.OnGameEnded()（更新 + 写盘）
```

### 20.3 对外接口

```csharp
SaveSystem.Load();                              // 读取
SaveSystem.Save();                              // 写盘
SaveSystem.OnGameEnded(playerWon, goldEarned);  // 游戏结束更新
SaveSystem.Reset();                             // 清除存档（调试用）
SaveSystem.Data.Gold                            // 读取当前金币
SaveSystem.Data.HasFirstWin                     // 是否首次胜利
SaveSystem.UnlockCodexEntry(id);                // 解锁图鉴条目
SaveSystem.IsCodexEntryUnlocked(id);            // 查询图鉴是否已解锁
SaveSystem.LoadAllCodexEntries(allIds);         // 批量加载图鉴状态
SaveSystem.GetUnlockedCodexCount();             // 获取已解锁数量
```

---

## 21. 叫分期系统（BiddingManager + NetworkBiddingManager + BiddingConfig）

### 21.1 场景流程

```
主菜单 → SceneLoader.LoadBidding() → Bidding 场景
  → BiddingSceneBootstrap 检测联机状态
  → 单机: BiddingManager（30s 倒计时 + 玩家/AI 轮流叫分）
  → 联机: NetworkBiddingManager（3 人网络轮流叫分 + AI 槽位）
  → 结果确定 → GameSession.SetResult() → SceneLoader.LoadGame()
  → GameBootstrapper 读取 GameSession 启动游戏
```

**BiddingSceneBootstrap**：挂载到叫分场景，`Awake()` 中检测 `NetworkManager.Instance.IsInRoom`，激活对应的管理器 GameObject。

### 21.2 叫分规则

- 玩家先叫，轮流响应（单机=AI，联机=真人+AI 槽位）
- 叫 3 分直接结束
- 所有人叫完一轮取最高分
- 超时无人叫分 → 随机分配（可配置）

### 21.3 联机叫分（NetworkBiddingManager）

**架构**：Master 客户端负责轮次管理和结果判定，所有叫分动作通过 `NetworkProtocol.BID_ACTION` 上报 Master，Master 验证后广播。

```
玩家点击叫分按钮 → SendToMaster(BID_ACTION, bid)
  → Master 验证合法性（轮次/分数范围/高于当前最高）
  → 广播 SendToAll(BID_ACTION, [slot, bid])
  → 所有客户端 ProcessBid() 更新 UI
  → Master 判定下一步：继续下一轮 / 结束叫分

AI 槽位（仅 Master 执行）:
  → Update() 中检测 _aiSlots.Contains(_currentTurnSlot)
  → 延迟 1.2s 后 AIDecideBid() → SendToAll(BID_ACTION)
```

**断线处理**：玩家断线时检测 `realPlayerCount + aiSlots.Count < 3`，若不足则取消叫分。

**基地映射**：Master 端 `MasterEndBidding()` 生成 `baseMapping[3]`（地主=2，农民=0/1）+ 随机 seed，通过 `BID_RESULT` 广播。所有客户端调用 `GameSession.SetResultNetwork()` 存储结果。

### 21.3 BiddingConfig 参数

| 参数 | 默认值 | 说明 |
|:---|:---|:---|
| `biddingDuration` | 30 | 叫分总时长（秒） |
| `maxBid` | 3 | 最高叫分 |
| `aiPassChance` | 0.6 | AI 不叫概率 |
| `aiBid1Weight` | 0.5 | AI 叫 1 分权重 |
| `aiBid2Weight` | 0.3 | AI 叫 2 分权重 |
| `aiBid3Weight` | 0.2 | AI 叫 3 分权重 |
| `randomAssignOnTimeout` | true | 超时随机分配 |

### 21.4 基地映射约定

`baseBuildings` 数组索引约定：
- `[0]` = 地主基地（IsLandlord = true）
- `[1]` = 农民基地 A（IsLandlord = false）
- `[2]` = 农民基地 B（IsLandlord = false）

叫分结束后，农民基地随机分配。

---

## 22. 跨场景数据传递（GameSession — 已废弃降级）

> ⚠️ Phase 5 后 GameSession 仅作为桥接数据缓存，核心逻辑由 FusionGameManager WorldState 替代。

### 22.1 单机模式

```csharp
GameSession.SetResult(isLandlord, multiplier, landlordIdx, farmerIndices);
GameSession.PlayerIsLandlord  // 本机玩家是否地主
GameSession.BidMultiplier     // 叫分倍数
GameSession.HasResult         // 是否有叫分结果
GameSession.Reset()           // 清除所有会话数据
```

### 22.2 联机模式

```csharp
GameSession.IsNetworkMode = true;
GameSession.NetworkSeed = seed;
GameSession.SetResultNetwork(landlordSlot, multiplier);
GameSession.SetLocalPlayerIsLandlord(isLandlord);
GameSession.AISlots           // AI 槽位（Lobby 阶段临时存储）
```

**AI 槽位同步**：通过 `FusionGameManager.AddAISlot()`/`RemoveAISlot()` 写入 `WorldState.PlayerX.IsAI`，Fusion 自动同步到所有客户端。

### 22.3 联机初始化流程

```
NetworkBiddingManager.HandleBidResult()
  → GameSession.IsNetworkMode = true
  → GameSession.NetworkSeed = seed
  → GameSession.SetResultNetwork(landlordSlot, multiplier)
  → GameSession.SetLocalPlayerIsLandlord(localIsLandlord)
  → LoadScene(GAME_SCENE)
  → GameBootstrapper 读取 GameSession.LandlordSlot
```

---

## 23. BOSS 系统（BossController + BuildingAI）

### 23.1 生命周期

```
场景加载
  → BossController.Awake()
    ├─ _isBoss = true
    ├─ 缓存 Renderer/Collider/HealthBar
    ├─ 禁用 Renderer/Collider/HealthBar（初始隐藏）
    └─ 禁用 BossSkillSystem（激活时恢复）
  → CardUnit.Start()
    └─ Initialize(0, Rank.Three, Lane.None, Inspector._isLandlord)
  → GameBootstrapper.Start() 协程
    ├─ Awake: 跳过 _isBoss 单位的 BuildingAI 禁用
    ├─ Step 5b:
    │   ├─ SetLandlord(!PlayerIsLandlord) 纠正阵营
    │   ├─ 刷新血条颜色
    │   ├─ BossDialogueBubble.Initialize()（订阅演出+技能对话事件）
    │   ├─ 启用 BuildingAI（确保 enabled=true）
    │   └─ boss.Inject(battleManager, deck)
    │       └─ OnStart → ActivateBoss()（若 _onStartDelay>0 则延迟）
    │           ├─ 恢复 Renderer/Collider/HealthBar/BossSkillSystem
    │           ├─ 解锁路线
    │           ├─ BattleManager.ActivateBoss(route)（设置 FollowPath + SetEnemyUnits）
    │           ├─ RegisterBossAsSummoner()
    │           │   └─ BuildingAI.Initialize(hand, economy, ...)
    │           └─ OnBossAwakened 事件 → PlaySequence(_bossSequence)
    │               ├─ CameraDirector.SyncOriginalPosition + IsBusy=true
    │               ├─ 镜头 FocusTarget → FollowTarget → Return（并行）
    │               ├─ BossDialogueBubble 打字机效果（并行）
    │               └─ 镜头归位 IsBusy=false
  → BuildingAI.Update()
    ├─ 摸牌（每 drawInterval 秒）
    ├─ 经济增长
    └─ MakeDecision()（每 decisionInterval 秒）
        └─ DeployCards() → SpawnPool 出兵
```

### 23.2 Prefab 结构

```
BossGiantBird / BossSword (CardUnit, BossController, BuildingAI, SpawnPool, RouteGroup, UnitPassives, UnitVFX)
├── Visual (SpriteRenderer, SimpleAnimator, Animator, AttackEventRelay)
├── HealthBar (UnitHealthBar, SpriteRenderer)
├── DialogueBubble (BossDialogueBubble)
│   └── Canvas (World Space, CanvasGroup, SpeakerText, ContentText)
├── Audio (UnitAudio)  ← 解耦到子物体
├── KingPoint (Transform, SpawnPool._spawnPoint)
└── AttackPoint (Transform)
```

### 23.3 关键配置

| 组件 | 字段 | 说明 |
|:---|:---|:---|
| CardUnit | `_isBoss = true` | 标记为 BOSS 单位 |
| CardUnit | `_isLandlord` | Inspector 值会被 Step 5b 强制纠正 |
| BossController | `_trigger` | OnStart / OnTimer / OnBuildingDestroyed |
| BossController | `_bossRoute` | BOSS 行进路线（RoutePath） |
| BossController | `_playerRouteToBoss` | 玩家主堡到 BOSS 的路线（BOSS 激活后解锁） |
| BossController | `_enableSummoner` | 启用召唤师能力 |
| BossController | `_onStartDelay` | OnStart 模式注入后固定延迟（秒），确保演出时间一致 |
| BossController | `_bossSequence` | 本 Boss 的演出配置（引用场景中的 PresentationSequence 组件） |
| BossController | `IsActive` | 只读属性，`_activated` 的公开访问。供 BattleManager.GetEnemiesFor 检查 |
| BuildingAI | `decisionInterval` | 出牌判定间隔（秒） |
| SpawnPool | `_rankPrefabs[13]` | 按点数映射的召唤物预制体 |
| SpawnPool | `_spawnPoint` | 召唤物生成位置（应为 BOSS 子物体） |
| RouteGroup | `_routes[]` | 召唤物行进路线 |
| BossSkillSystem | `skills[]` | BOSS 技能列表（按数组顺序检查触发） |
| BossSkillSystem.Skill | `skillDialogue` | 释放技能时显示的对话文本（留空不显示） |
| BossSkillSystem.Skill | `skillSpeaker` | 技能对话的说话人名字（留空默认 Boss） |

### 23.4 路径跟随

BOSS 的 RoutePath 需要**取消勾选 `_cachePositions`**，使路径点实时跟随 BOSS 移动。否则召唤物会在初始位置生成。

### 23.5 BossSkillSystem（BOSS 技能系统）

独立组件，挂在 BOSS 根节点上，与 UnitPassives 共存。

**触发条件（SkillTrigger）：**

| 触发类型 | 说明 |
|---|---|
| `OnHPThreshold` | HP 降到阈值时触发（只触发一次，不会重复） |
| `OnTimer` | 按冷却时间循环触发 |
| `OnKill` | 击杀敌人时触发 |

**效果类型（SkillEffectType）：**

| 效果 | 说明 | 关键参数 |
|---|---|---|
| `AoeDamage` | 范围伤害 | effectValue=ATK 倍率, effectRadius=半径 |
| `AoeStun` | 范围眩晕 | effectValue=眩晕秒数, effectRadius=半径 |
| `Heal` | 治疗 | effectValue=MaxHP 百分比 |
| `Knockback` | 范围击退 | effectValue=击退距离, effectRadius=半径 |
| `Buff` | ATK 增益 | effectValue=倍率 |
| `Dash` | 冲刺伤害 | dashDistance=距离, dashSpeed=速度, effectValue=ATK 倍率 |

**施法特性：**

| 字段 | 说明 |
|---|---|
| `castDuration` | 施法持续时间（0=瞬发） |
| `invulnerable` | 施法期间不可选取（`CardUnit.Invulnerable = true`） |
| `clearCC` | 施法时清除眩晕/减速 |
| `animTrigger` | 施法动画名（多个技能可共用同一个 Trigger，如不同 HP 阶段的 Dash 共用 "Dash" 动画） |
| `effectDelay` | 施法后多久触发效果 |

**技能对话：**

| 字段 | 说明 |
|---|---|
| `skillDialogue` | 释放技能时显示的对话文本（留空则不显示） |
| `skillSpeaker` | 对话显示的说话人名字（留空默认 "Boss"） |

- `BossSkillSystem.OnSkillActivated(speaker, text)` 事件在 `ExecuteEffect()` 中触发
- `BossDialogueBubble` 监听此事件，自动显示 2 秒后淡出，SpeakerText 隐藏
- 演出对话（BattlePresentationManager 触发）与技能对话使用同一个气泡，互斥（新对话中断旧对话）

**冲刺（Dash）机制：**
- 方向：朝当前目标 → 路径前进方向 → 默认向右
- 碰撞检测：使用 Boss 碰撞箱实际宽度扫描路径上的敌人
- 每个敌人每次冲刺只受一次伤害（`HashSet` 去重）

**门禁过滤：**
- 所有 OverlapCircle 伤害/控制效果均经过 `IsValidCombatTarget()` 过滤
- 涉及方法：`ExecuteAoeDamage`、`ExecuteAoeStun`、`ExecuteKnockback`、`UpdateDash`
- 未激活 BOSS 不会被自身技能误伤

**特效缩放：**
- 特效根据 `effectRadius` 缩放：`scale = effectRadius / 2f`
- 与 Projectile 爆炸特效缩放规则一致

**生命周期：**
```
Update() → 检查触发条件 → StartCast() → 施法动画+效果 → EndCast()
          ↓ 死亡时
          ForceEndCast() → 清除 Invulnerable
```

### 23.6 注意事项

- BOSS 不在 `baseBuildings` 的 AI 注入流程中，由 `BossController` 独立管理
- BOSS 的 BuildingAI 必须在 `Inject` 之前被显式启用（Step 5b 处理）
- BOSS 死亡后由 `UnitFactory.Despawn` 回收（`SourcePrefab = null` → `Destroy`）
- BOSS 的 UnitId 由 `BattleManager.RegisterUnit` 统一分配，不使用 `CardUnit.Start` 的默认值
- `Invulnerable` 状态在 BOSS 死亡时由 `ForceEndCast()` 自动清除，防止残留
- **BOSS 未激活时完全不参与战斗**：Renderer/Collider/HealthBar/BossSkillSystem 全部禁用，GetEnemiesFor 排除未激活 BOSS
- **BossSkillSystem 组件级禁用**：Awake 禁用 → ActivateBoss 启用，确保 Update 不执行
- **演出序列挂场景**：PresentationSequence 为 MonoBehaviour，每关场景各自挂载，BossController._bossSequence 引用。镜头 target 运行时由 GameBootstrapper 自动绑定 Boss Transform，无需手动拖入
- **CameraDirector.IsBusy 演出锁**：演出期间 IsBusy=true，CameraController.Update() 跳过，防止镜头冲突颤抖
- **技能对话**：BossSkill.skillDialogue 非空时触发 OnSkillActivated 事件，BossDialogueBubble 订阅后自动显示（无 SpeakerText，2 秒自动消失）
- 攻击状态超时安全阀：`AttackInterval×3` 秒未完成自动重置
- 多个 HP 阶段技能可共用同一个 `animTrigger`（如 75%/50%/25% 都填 "Dash"），动画相同但效果参数独立配置

### 23.7 CoolDownEffect 配置要求

`CoolDownEffect` 使用 `Image.fillAmount` 实现圆形冷却进度。冷却遮罩 Image 必须配置为：
- **Image Type = Filled**
- **Fill Method = Radial 360**

### 23.8 BOSS 施法动画同步

BOSS 技能施法时间与动画同步机制：

| 参数 | 说明 |
|:---|:---|
| `useAnimLength` | 是否使用动画长度作为施法时间 |
| `castDuration` | 手动配置的施法时间（秒） |

**同步规则**：
- `useAnimLength = true` 且 `castDuration > 0`：调整动画速度匹配 `castDuration`
- `useAnimLength = true` 且 `castDuration = 0`：使用动画长度作为施法时间
- `useAnimLength = false`：使用手动配置的 `castDuration`

**公式**：`speedMult = animLength / castDuration`

### 23.9 BOSS 技能触发保护

| 保护 | 说明 |
|:---|:---|
| `IsCasting` | 公开属性，供 CardUnit 检查施法状态 |
| `Invulnerable` | 施法期间不可选取，免疫所有伤害 |
| 效果强制触发 | `effectDelay > castDuration` 时，在施法结束前强制触发效果 |

---

## 24. 架构债务登记

### Fusion 游戏场景集成状态（历史记录：2026-06-26）

> 下表保留旧版联机排查记录；其中“已完成/未完成”是 2026-06-26 的状态快照，不能当作当前实现结论。当前代码入口见 §13.6–§13.7；联机状态仍需多客户端验证。

| 子系统 | 状态 | 说明 |
|:---|:---|:---|
| 叫分输入 RPC | ✅ 完成 | Client→Host RpcSubmitBid |
| 摸牌 RPC | ✅ 完成 | Client→Host RpcDrawCard |
| 出牌 RPC | ✅ 完成 | Client→Host RpcPlayCard |
| IdentityReady 锁 | ✅ 完成 | 防止 Client 在 Host 初始化前读取 WorldState |
| Slot/Identity 同步 | ✅ 完成 | WorldState 映射 + GetLocalSlot 回退 |
| AI 叫分 | ✅ 完成 | 双重节流已修复 |
| 出牌执行 | ✅ 完成 | ApplyPlayCards 连接 BattleManager.DeployCards |
| IsPlayingFinished | ✅ 完成 | 返回 false，由 BattleManager 驱动结束 |
| GameSession 模式锁定 | ✅ 完成 | SetNetworkMode 单次写入 |
| 退出联机解锁 | ✅ 完成 | ResetNetworkModeLock |
| Client 手牌 UI 同步 | ❌ 未完成 | HandArea 仍读本地引用 |
| Client 牌堆显示 | ❌ 未完成 | CardCounterUI 不同步 |
| Client 经济显示 | ❌ 未完成 | EconomyManager 不读 _syncedGold |
| Client 领域 UI | ❌ 未完成 | DomainUIController 不同步 |
| BattleManager→Fusion 桥接 | ❌ 未完成 | OnGameEnded 不触发状态机 |
| 兵种视觉同步 | ❌ 未完成 | Client 无 UnitView |

以下为既有架构债务登记，部分条目同样来自早期实现状态。修复或关闭条目前应先对照当前代码和多客户端验证结果，避免把历史问题当作现行故障。

| 债务 | 现状 | 触发偿还条件 |
|:---|:---|:---|

| Authority 未抽象 | `IsMasterClient` 判断分散在 GameBootstrapper + BuildingAI 等处 | 新增观战/AI/回放/专用服务器中任意 2 项 |
| 网络协议未模型化 | 消息使用 `string Key + object[]` 模式，协议定义散落在 NetworkProtocol 常量中 | 协议数量超过 30 种或需要版本兼容 |

| 客户端预测 | 无。所有操作等 Master 确认后才执行，高延迟下操作感差 | 延迟 > 100ms 时玩家体验明显下降 |
| 文档职责过重 | ARCHITECTURE.md 承担架构/规范/决策/债务 4 种职责（当前 2946 行） | 联机稳定化完成后拆分为 Architecture.md + Debt.md + ADR/ |

| **[ARCH-016] Client 手牌 UI 不同步（P0）** | HandArea 读本地 `playerHand` 引用，不从 WorldState 或 RPC 同步的 `_slotHandCards` 读取 | 联机模式出牌后 Client 手牌不更新 |
| **[ARCH-017] BattleManager→Fusion 状态机未桥接（P0）** | BattleManager.OnGameEnded 触发但 FusionGameManager 不响应，游戏无法正常结束 | 联机模式游戏永不结束或无结算界面 |
| **[ARCH-018] Client 经济/领域 UI 未同步（P1）** | _syncedGold 和领域 RPC 存在，但 EconomyManager/DomainUIController 不读取 | 联机模式金币/领域显示错误 |
| **[ARCH-019] _slotHandCards 跨进程不同步（P1）** | Host 本地字典，Client 依赖 RPC 同步，但同步时机和完整性不足 | Client 手牌与 Host 不一致 |
| **[ARCH-020] GameSession 静态污染源未完全清除（P2）** | AISlots/RawAISlots 仍为 static HashSet，退出联机后可能残留。已通过 ResetNetworkModeLock() 改善 | 多次切换单机/联机后状态异常 |
| **[ARCH-021] 联机模式游戏场景运行不完整（P0）** | 血条/手牌已渲染，但兵种生成/出牌/经济/战斗等核心逻辑未正常运行。疑似 FusionGameManager 与 BattleManager 之间的桥接不完整 | 联机模式可进入游戏但无法正常对战 |
| **[ARCH-022] LobbyIdentityService 与 FusionGameManager 双重 slot 管理（P1）** | 两套独立的 slot 字典（_playerToSlot / LobbyIdentityService._playerToSlot），需 AssignExistingPlayerSlots 补偿同步 | 联机模式 slot 分配不一致 |
| **[ARCH-023] GameBootstrapper 联机/单机逻辑耦合（P1）** | GameBootstrapper.Awake() 在联机模式仍执行部分逻辑（CardUnit.PlayerIsLandlord），Start() 通过 yield break 跳过但依赖 Activate NetworkGameBootstrapper 兜底 | 联机模式初始化路径脆弱 |

如果 Image Type 为 Simple，`fillAmount = 1` 时遮罩完全覆盖按钮，配合 `coolDownColor`（深灰 80%）会看起来全黑。

---

## 25. Event + Snapshot + Tick 三层确定性模型 v2.0（Final Lock）

> **目标规范，不等于当前实现状态。** 本节描述期望中的 Event/Snapshot/Tick 收敛模型；当前 Fusion 玩法主要使用 Fusion `[Networked]` 属性和类型化 RPC，尚未全面切换到本节的完整 Event→Snapshot 应用流程。实现状态以 §13 为准。

### 25.1 系统目标（最终约束）

| 目标 | 说明 |
|:---|:---|
| 多客户端强一致（Strong Consistency） | 所有客户端收敛到同一权威状态 |
| 弱网可恢复（Recoverable） | 网络延迟/乱序/丢包容忍 |
| 任意客户端可自愈（Self-Healing） | 不依赖事件历史，Snapshot 可完全重建游戏 |
| 支持无限循环对局（Loop Safe） | Round-based / Session-based 稳定运行 |
| 不依赖事件顺序 | Event 是不可变输入流，Snapshot 才是真相源 |
| 不允许"隐式状态修改" | Event 仅作为输入，Client 缓存后由 Snapshot 授权执行 |

### 25.2 三层架构定义（锁定版）

#### 🥇 L1: Event Layer（事件层 / 增量事实）

**定义**：Event = 不可变事实记录（Append-only Log）

```
struct GameEvent {
    int tick;           // 绑定的 Tick
    string type;        // 事件类型
    object[] payload;   // 事件数据
}
```

**规则（强约束）**：
- ✔ Event 不直接修改状态
- ✔ Event 必须携带 Tick
- ✔ Event 允许乱序到达
- ✔ Event 允许丢弃（由 Tick 判定）
- ✔ Event 不参与最终一致性计算（只做输入）

#### 🥈 L2: Snapshot Layer（状态层 / 权威世界）

**定义**：Snapshot = 某 Tick 下的完整世界快照

```
class GameSnapshot {
    int tick;                                // 快照 Tick
    int deckId;                              // 牌堆唯一标识
    int remaining;                           // 牌堆剩余张数
    string gamePhase;                        // 游戏阶段
    Dictionary<int, int[]> slotHands;        // 每个槽位手牌
    Dictionary<int, float> slotGold;         // 每个槽位金币
    Dictionary<int, float> slotIncomeRates;  // 每个槽位回金速度
    Dictionary<int, float> unitHPs;          // 所有存活单位 HP
}
```

**规则**：
- ✔ Snapshot 是唯一"真相源（Truth Source）"
- ✔ Snapshot 永远可覆盖 Event
- ✔ Snapshot 只前进不回退（tick monotonic）
- ✔ Snapshot 用于修正 & 重建

#### 🥉 L3: Tick Layer（确定性核心）

**定义**：Tick = Master 单调递增逻辑时钟

**规则（最关键）**：
- ✔ Tick 只由 Master 增长
- ✔ Tick 不可回退
- ✔ 所有 Event 必须绑定 Tick
- ✔ 所有 Snapshot 必须绑定 Tick

**判定规则（铁律）**：
```csharp
if (incoming.tick < local.tick) → 丢弃
```

### 25.3 核心运行流程（最终锁定）

#### 🧠 Master 流程（唯一权威）

```
1. Receive Request
2. AdvanceTick()
3. Execute Logic
4. Modify State
5. Build Snapshot(tick)
6. Broadcast Event + Snapshot
```

#### 📡 Client 流程（纯投影）

```
1. Receive Event → 缓存（不执行）
2. Receive Snapshot:
   if snapshot.tick > local.tick:
        apply snapshot (overwrite state)
```

#### 🔁 自愈流程（Reconciliation Loop）

```
每 N 秒（默认 5s）：
  Client → Request Snapshot(tick)
  Master → Return Snapshot(latest)
  Client → 强制对齐
```

### 25.4 五条一致性铁律（最终锁死）

| # | 铁律 | 违反后果 |
|:---|:---|:---|
| 1 | **Tick 单调递增** — `tick[n+1] > tick[n]` | 状态回滚 → 客户端分叉 |
| 2 | **旧数据永远无效** — `if (incoming.tick < local.tick) ignore` | 乱序覆盖 → 状态污染 |
| 3 | **Event 不改变状态** — Event = 输入，Snapshot = 真相 | Event 丢失 → 状态不可恢复 |
| 4 | **Snapshot 是唯一修正源** — 任何偏差必须被 Snapshot 覆盖 | Client 本地推导 → 状态分叉 |
| 5 | **Master 唯一写状态** — Client 永远只投影 | 多端写入 → 状态分裂 |

### 25.5 生命周期（最终工业循环）

```
INIT
  ↓
SYNC (Snapshot Sync)
  ↓
RUN (Event Flow)
  ↓
RECONCILE LOOP (Self-Heal)
  ↓
END
  ↓
RESET (FULL STATE WIPE)
  ↓
INIT
```

### 25.6 Deck 系统映射

```
struct DeckState {
    int tick;        // 快照时的 Tick
    int deckId;      // 局级隔离
    int remaining;   // Snapshot 权威值
}
```

### 25.7 Bug 根因解释

三类系统缺陷叠加导致当前问题：

| 问题 | 根因 | v2.0 解决方式 |
|:---|:---|:---|
| Event 丢失/延迟 | PLAYER_READY 不稳定 | Snapshot 覆盖 |
| 状态依赖 Event 顺序 | 某客户端"永远不同步" | Tick 丢弃旧事件 |
| 没有强制全局收敛点 | 局部冻结（只有 Master 正常） | Reconcile 修复 |

### 25.8 与当前系统的映射

| 当前系统 | v2.0 目标 | 改造要点 |
|:---|:---|:---|
| `_stateVersion` | `_tick` | 已完成 ✅ |
| Event 直接修改状态 | Event 仅缓存，Snapshot 授权执行 | **部分完成** — `GameEvent` struct 已实现（append-only），`GameSnapshot` 类已实现（完整快照），FusionGameManager 中尚未完全切换到 Event→Snapshot 授权执行模式 |
| `MASTER_STATE_SYNC` | `GameSnapshot` 完整快照 | 已完成 ✅（`GameSnapshot.cs` 已独立实现） |
| `HP_CORRECTION` 独立修正 | 合并入 Snapshot | 已完成 ✅ |
| `SyncState` 状态机 | 生命周期阶段 | **待升级** |

### 25.9 Trace 日志关键搜索词

| 搜索词 | 说明 |
|:---|:---|
| `TICK_ADVANCE` | Tick 递增 |
| `EVENT_BUFFERED` | Event 已缓存，等待 Snapshot 授权 |
| `EVENT_DISCARD_STALE` | Event 因 tick < localTick 被丢弃 |
| `SNAPSHOT_APPLIED` | Snapshot 成功覆盖本地状态 |
| `SNAPSHOT_STALE` | Snapshot 因 tick <= localTick 被丢弃 |
| `RECONCILE_REQUEST` | 客户端请求 Snapshot 校正 |
| `PHASE_TRANSITION` | 生命周期阶段切换 |

---

## 26. Truth Source Convergence（真相源收敛）

> 本节是收敛目标与历史核对清单。表中的完成状态来自早期代码审查，尚未由本次文档更新做多人运行验证；应结合 §13.7 和当前多客户端测试结果复核。

> 本节定义所有游戏状态的唯一真相源，确保 Master 是唯一权威写入方。
> Client 为纯投影层，只读取状态、显示 UI、播放表现。

### 26.1 收敛原则

```
Master = 唯一真相源
Client = 纯投影层
UI     = 只读显示
AI     = 只读决策（联机模式）
```

**禁止**：
- Client 直接修改游戏状态
- UI 层写回业务数据
- AI 在联机模式修改金币/Buff/HP

### 26.2 真相源清单

| 系统 | 真相源 | 保护机制 | 状态 |
|:---|:---|:---|:---|
| 牌堆剩余 | `CardDeck.Remaining` | 计算属性 `TotalCards - _cursor` | ✅ 已收敛 |
| 手牌 | `_slotHands[slot]` | 字典存储，`_playerHand` 为只读投影 | ✅ 已收敛 |
| 金币 | `_slotEconomies[slot]` | `IsMaster` 检查 | ✅ 已收敛 |
| Buff | `CardUnit._buffs` | `SimulatesCombat` 门控 | ✅ 已收敛 |
| Stun | `CardUnit.StunTimer` | `SimulatesCombat` 门控 | ✅ 已收敛 |
| 击退 | `transform.position` | `SimulatesCombat` 门控 | ✅ 已收敛 |
| HP | `CardUnit._currentHP` | `SimulatesCombat` 门控 | ✅ 已收敛 |
| 目标 | `CardUnit.Target/CurrentTarget` | `SimulatesCombat` 门控（`OnUpdate` 入口） | ✅ 已收敛 |
| 位移 | `transform.position` | `SimulatesCombat` 门控（战斗位移） | ✅ 已收敛 |

### 26.3 SimulatesCombat 保护矩阵

| 方法 | SimulatesCombat 检查 | 说明 |
|:---|:---|:---|
| `TakeDamage()` | ✅ `if (!SimulatesCombat) return;` | 仅 Master 处理伤害 |
| `Die()` | ✅ `if (!SimulatesCombat) return;` | 仅 Master 触发死亡 |
| `Heal()` | ✅ `if (!SimulatesCombat) return;` | 仅 Master 治疗 |
| `ApplyBuff()` | ✅ `if (!SimulatesCombat) return;` | 仅 Master 修改 Buff |
| `RemoveBuff()` | ✅ `if (!SimulatesCombat) return;` | 仅 Master 移除 Buff |
| `StunTimer` 递减 | ✅ `if (SimulatesCombat && StunTimer > 0f)` | 仅 Master 递减眩晕 |
| `UpdateBuilding()` 回血 | ✅ `if (SimulatesCombat && _isBuilding)` | 仅 Master 回血 |
| `OnUpdate()` 入口 | ✅ `if (!SimulatesCombat) return;` | Client 仅行军 |
| `BossSkillSystem.ExecuteAoeStun()` | ✅ `if (!_owner.SimulatesCombat) return;` | 仅 Master 眩晕 |
| `BossSkillSystem.ExecuteKnockback()` | ✅ `if (!_owner.SimulatesCombat) return;` | 仅 Master 击退 |
| `BossSkillSystem.ExecuteDash()` | ✅ `if (!_owner.SimulatesCombat) return;` | 仅 Master 冲刺 |
| `BossSkillSystem.ClearCC()` | ✅ `if (!_owner.SimulatesCombat) return;` | 仅 Master 清除 CC |
| `UnitPassives.EmitShockwave()` | ✅ `if (_owner.SimulatesCombat)` | 仅 Master 执行震波 |
| `UnitPassives.KnockbackCoroutine()` | ✅ `if (_owner.SimulatesCombat)` | 仅 Master 执行击退 |

### 26.4 经济系统保护

| 方法 | IsMaster 检查 | 说明 |
|:---|:---|:---|
| `EconomyManager.Update()` | ✅ 联机模式不调用 `UpdateEconomy()` | Client 不自动增长金币 |
| `EconomyManager.TrySpendGold()` | ✅ `!IsMasterClient → return false` | Client 不消耗金币 |
| `EconomyManager.AddGold()` | ✅ `!IsMasterClient → return` | Client 不增加金币 |
| `BuildingAI.Update()` | ✅ 联机模式不调用 `UpdateEconomy()` | AI 不自动增长金币 |
| `BuildingAI.MakeDecision()` | ✅ `!IsMasterClient → continue` | AI 不执行 `TrySpend()` |

### 26.5 数据流

```
Master 端:
_slotEconomies[slot].UpdateEconomy(dt)
    ↓
EconomySystem.CurrentGold += increment
    ↓
BuildCurrentSnapshot()
    ↓
_net.SendToAll(MASTER_STATE_SYNC, snapshot.Serialize())

Client 端:
HandleMasterStateSync()
    ↓
_slotEconomies[slot].SetGold(kvp.Value)
    ↓
EconomySystem.CurrentGold = amount
    ↓
OnGoldChanged?.Invoke(CurrentGold)
    ↓
EconomyManager.UpdateGoldUI()
```

### 26.6 验证清单

| 验证项 | 状态 |
|:---|:---|
| Client 不执行 `UpdateEconomy()` | ✅ |
| Client 不执行 `TrySpend()` | ✅ |
| Client 不执行 `AddGold()` | ✅ |
| Client 不修改 `Buff` | ✅ |
| Client 不修改 `StunTimer` | ✅ |
| Client 不执行 `Knockback` | ✅ |
| Client 不修改 `HP` | ✅ |
| Client 不选择 `Target` | ✅ |
| Client 仅执行路径行军（视觉） | ✅ |
| Master 唯一写入所有游戏状态 | ✅ |

---

## 27. Client 战斗表现层（Event-Driven Presentation）

> 本节描述 Client 表现层的设计目标。当前项目的 Fusion 玩法表现同时使用网络状态、Fusion RPC 和本地事件；完整战斗视觉同步尚需多客户端验收。本节的事件名称/实现状态不应被视为传输协议清单。

### 27.1 设计原则

```
Master = 唯一战斗逻辑权威
Client = 纯投影层（动画 + 特效 + UI + 音效）
FusionGameManager = 事件转发层（不直接播放音效）
```

**禁止**：
- Client 修改任何战斗状态
- Client 执行战斗计算
- FusionGameManager 直接播放音效
- 新增事件类型（CombatEvent struct 等）

### 27.2 事件体系（已冻结）

| 事件 | 方向 | 用途 |
|:---|:---|:---|
| `UNIT_ATTACK` | Master → Client | 播放攻击动画 + 攻击音效 |
| `UNIT_HIT` | Master → Client | 播放受击动画 + 受击音效 + 飘字 |
| `UNIT_STUN` | Master → Client | 播放眩晕视觉（VisualStunTimer） |
| `UNIT_KNOCKBACK` | Master → Client | 播放击退视觉协程 |
| `UNIT_DIED` | Master → Client | 播放死亡动画 + 死亡音效（已有） |
| `HP_CORRECTION` | Master → Client | 血量同步（已有） |

### 27.3 音效系统架构

```
Master: TakeDamage() → OnTakeDamageEvent → UnitAudio.OnTakeDamage() → 播放音效
Client: UNIT_HIT → FusionGameManager → UnitAudio.PlayHitNetwork() → 播放音效
```

**关键规则**：
- Master 保留本地事件驱动音效（不修改）
- Client 通过网络事件驱动音效
- `FusionGameManager` 不直接播放音效，转发到 `UnitAudio`
- `HandleUnitAttack()` 检查 `IsMasterClient`，Master 不执行

### 27.4 VisualStunTimer（Client 专用）

```
StunTimer      → 逻辑层，仅 Master 写入
VisualStunTimer → 表现层，Client 用于视觉效果
```

**防止逻辑污染**：Client 设置 `VisualStunTimer`，不修改 `StunTimer`。

### 27.5 HP 事件拆分

| 事件 | 触发时机 | 用途 |
|:---|:---|:---|
| `OnHPChanged` | TakeDamage / Heal / SetHP | 血条动画 + 闪烁 |
| `OnStatsChanged` | ApplyBuff / RemoveBuff / RecalculateStats | 属性 UI 刷新 |

**禁止**：`RecalculateStats()` 调用 `OnHPChanged`（属性变化不应触发血条闪烁）。

### 27.6 实现状态

| 功能 | 状态 |
|:---|:---|
| 攻击动画同步 (UNIT_ATTACK) | ✅ 完成 |
| 受击反馈 (UNIT_HIT) | ✅ 完成 |
| 血条平滑动画 | ✅ 完成 |
| 事件拆分 (OnHPChanged + OnStatsChanged) | ✅ 完成 |
| 音效系统重构 | ✅ 完成 |
| 攻击特效 | ⏳ P1 待实现 |
| 技能特效 | ⏳ P1 待实现 |

---

## 28. Combat Gate System（战斗统一门禁）

### 28.1 设计目标

统一所有战斗相关逻辑的入口判断，解决：
- 未激活 BOSS 参与战斗（卡单位/空挥/移动异常）
- GetEnemiesFor 直接污染目标列表
- 战斗逻辑与"是否可参与战斗"混在一起
- 嘲讽/死亡/距离判断互相打架
- 溅射/多目标单位被错误中断攻击
- 攻击状态机被"目标变化"强行打断导致重复动画

### 28.2 核心门禁函数

```csharp
// BattleManager.cs
public bool IsValidCombatTarget(CardUnit unit, CardUnit target)
{
    if (target == null || !target.IsAlive || target == unit) return false;
    var boss = target.GetComponent<BossController>();
    if (boss != null && !boss.IsActive) return false;
    if (target.IsLandlord == unit.IsLandlord) return false;
    if (unit.Lane != Lane.None && target.Lane != Lane.None && target.Lane != unit.Lane) return false;
    return true;
}
```

**唯一入口**：所有"是否能被选为目标"的判断必须通过此函数。

### 28.3 三层架构

| 层 | 职责 | 入口 |
|:---|:---|:---|
| 门禁层 | 是否能参与战斗 | `IsValidCombatTarget()` |
| 战斗状态机 | 攻击生命周期 | `_animDone && _hitTimelineDone` |
| 控制层 | 打断攻击 | `InterruptAttack()`（仅眩晕/死亡/强制控制） |

### 28.4 攻击状态机（最终版）

```
TryAttack()
  → _isAttacking = true
  → 创建 AttackTimeline（hitTimes[] 数组）
  → _attackSnapshotTargets = FindAllTargets()（多目标单位）
  → 播放动画（纯表现）

OnUpdate() 攻击中:
  → 超时安全阀（3×AttackInterval，仅防卡死）
  → UpdateAttackTimeline()（时间驱动攻击帧）
  → 嘲讽记录（_pendingTauntTarget，不中断攻击）
  → _animDone && _hitTimelineDone → FinishAttack()

攻击结束后:
  → 检查 _pendingTauntTarget → 切换目标
```

**唯一退出条件**：`_animDone && _hitTimelineDone`

**禁止中断**：
- 目标死亡
- 目标超范围
- 目标消失

**允许中断**：
- 眩晕（Stun）
- 死亡（Die）
- 强制控制效果（BossSkillSystem 中断等）

### 28.5 嘲讽延迟切换

```csharp
// 攻击中：记录嘲讽目标，不中断
if (tauntDuringAttack != null && tauntDuringAttack != _attackTarget)
    _pendingTauntTarget = tauntDuringAttack;

// 攻击结束后：切换到嘲讽目标
if (_pendingTauntTarget != null && _pendingTauntTarget.IsAlive)
    Target = _pendingTauntTarget;
```

### 28.6 溅射目标快照

```csharp
// TryAttack()：攻击开始时锁定目标
if (_maxTargets > 1)
    _attackSnapshotTargets = FindAllTargets();

// OnAttackHitFrame()：使用快照，不重新搜索
var targets = _attackSnapshotTargets ?? FindAllTargets();
```

### 28.7 修改点汇总

| 位置 | 改动 |
|:---|:---|
| `BattleManager.IsValidCombatTarget()` | 新增统一门禁函数 |
| `BattleManager.GetEnemiesFor()` | 替换为使用门禁 |
| `CardUnit.OnUpdate()` | 嘲讽改为延迟记录 |
| `CardUnit.OnUpdate()` 攻击结束 | 检查 _pendingTauntTarget |
| `CardUnit.TryAttack()` | 多目标单位创建快照 |
| `CardUnit.OnAttackHitFrame()` | 多目标使用快照 |
| `CardUnit.InterruptAttack()` | 重置新字段 |
| 6 处重置点 | 补齐 _pendingTauntTarget/_attackSnapshotTargets 重置 |

### 28.8 AttackTimeline 系统（替代 Animation Event + HitFrameCoroutine）

**核心思想**：攻击帧由时间驱动，不再依赖 Animation Event。

**数据结构**：
```csharp
float[] _hitTimes;    // normalized 0~1，如 [0.5, 1.0] 表示 50% 和 100% 时触发
float _attackTimer;   // 当前攻击已过时间
int _nextHitIndex;    // 下一个待触发的攻击帧索引
bool _hitTimelineDone; // Timeline 是否完成
```

**TryAttack() 创建 Timeline**：
```csharp
float interval = Stats.AttackInterval;
int hitCount = Mathf.Max(1, Stats.HitCount);
_hitTimes = new float[hitCount];
for (int i = 0; i < hitCount; i++)
    _hitTimes[i] = (float)(i + 1) / hitCount;
```

**UpdateAttackTimeline() 每帧执行**：
```csharp
float t = _attackTimer / interval;
while (_nextHitIndex < _hitTimes.Length && t >= _hitTimes[_nextHitIndex])
{
    ExecuteHit(_nextHitIndex);
    _nextHitIndex++;
}
if (_nextHitIndex >= _hitTimes.Length)
    _hitTimelineDone = true;
```

**ExecuteHit() → OnAttackHitFrame()**：
- ExecuteHit 负责授权验证（BossController.IsActive、IsValidCombatTarget）
- OnAttackHitFrame 负责纯伤害逻辑

### 28.9 Animation Event 授权验证

Animation Event 仍可能残留触发（旧动画未清理）。为防止未激活单位通过 Event 造成伤害：

```csharp
// OnAttackHitFrame() 入口
var boss = GetComponent<BossController>();
if (boss != null && !boss.IsActive) return;
```

**双重防护**：
- Timeline 路径：`UpdateAttackTimeline()` 检查 IsActive → InterruptAttack
- Event 路径：`OnAttackHitFrame()` 检查 IsActive → return

### 28.10 UnitHealthBar 死亡帧闪烁修复

**问题**：单位死亡同一帧触发受击闪烁，`LateUpdate` 因 `_owner.IsAlive=false` 直接返回，闪烁颜色永远不重置。

**修复**：`LateUpdate` 中移除 `_owner.IsAlive` 检查，闪烁逻辑始终执行：
```csharp
// 修复前
if (_owner == null || !_owner.IsAlive) return;

// 修复后
if (_owner == null) return;
```

---

## 29. 对话系统（DialogueBox）

### 29.1 设计目标

提供关卡剧情对话能力，支持打字机效果、角色立绘展示、全区域点击继续。

### 29.2 组件

| 组件 | 位置 | 说明 |
|:---|:---|:---|
| `DialogueData` | UI/Dialogue | ScriptableObject，定义对话序列 |
| `DialogueLine` | UI/Dialogue | 单条对话数据（说话人/立绘/内容/速度/颜色） |
| `DialogueBox` | UI/Dialogue | 对话框 UI 控制器（打字机/点击/键盘） |
| `DialogueTrigger` | UI/Dialogue | 场景进入触发器（自动/手动触发） |

### 29.3 DialogueLine 数据结构

```csharp
[System.Serializable]
public class DialogueLine
{
    public string speakerName;         // 说话人名称
    public Sprite portrait;            // 角色立绘
    public float portraitWidth;        // 立绘宽度（独立配置）
    public float portraitHeight;       // 立绘高度（独立配置）
    public float portraitOffsetX;      // 立绘水平偏移
    public float portraitOffsetY;      // 立绘垂直偏移
    public string content;             // 对话文本
    public float typeSpeed;            // 打字速度（秒/字）
    public Color speakerColor;         // 名称颜色
}
```

### 29.4 触发时机

| 触发点 | 实现方式 | 说明 |
|:---|:---|:---|
| 关卡开始 | `LevelConfig.enterDialogue` | 场景加载后自动播放 |
| 关卡胜利 | `LevelConfig.victoryDialogue` | 胜利面板弹出前播放 |
| 手动触发 | `dialogueBox.Show(data)` | 任意代码调用 |
| 场景触发器 | `DialogueTrigger` 组件 | 挂在场景中，支持延迟和单次触发 |

### 29.5 交互方式

| 输入 | 行为 |
|:---|:---|
| 点击对话框任意区域 | 打字中 → 跳过显示全部；已完成 → 播放下一句 |
| 空格键 / 回车 | 同上 |
| 跳过按钮 | 同上 |

### 29.6 DialogueBox API

```csharp
public class DialogueBox : MonoBehaviour
{
    // 播放对话序列
    public void Show(DialogueData data, Action onComplete = null);

    // 跳过当前打字或播放下一句
    public void SkipOrAdvance();

    // 强制关闭对话框
    public void ForceClose();

    // 状态查询
    public bool IsActive { get; }   // 对话框是否显示
    public bool IsTyping { get; }   // 是否正在打字

    // 事件
    public event Action OnDialogueStarted;
    public event Action<int, DialogueLine> OnLineStarted;
    public event Action<int, DialogueLine> OnLineCompleted;
    public event Action OnDialogueCompleted;
}
```

### 29.7 与现有系统的关系

- **BossDialogueBubble**：战斗中 BOSS 气泡对话（轻量级），与 DialogueBox 独立不冲突
- **BattlePresentationManager**：演出系统，可配合 DialogueBox 使用
- **GameBootstrapper**：初始化完成后可触发 enterDialogue
- **LevelConfig**：每个关卡配置可关联 `enterDialogue` 和 `victoryDialogue`

### 29.8 目录结构

```
Assets/Scripts/UI/Dialogue/
├── DialogueData.cs        # 对话数据 ScriptableObject
├── DialogueLine.cs        # 单条对话数据（内嵌在 DialogueData.cs）
├── DialogueBox.cs         # 对话框 UI 控制器
└── DialogueTrigger.cs     # 场景进入触发器

Assets/Config/Dialogues/   # 对话配置资产（可选）
└── Level_01_Enter.asset
```

### 29.9 预览

HTML 预览文件：`Preview/dialogue_preview.html`（打字机效果 + 立绘 + 全区域点击）
