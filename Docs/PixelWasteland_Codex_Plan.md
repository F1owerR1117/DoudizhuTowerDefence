# 像素废土化改造方案 · Codex 场景

> 目标：把 `Assets/Scenes/Codex.unity`（图鉴 / 档案）从「暗黑奇幻写实 UI」改造为「像素废土像素 UI」。
> 本文档只描述**美术风格改造**，不改玩法逻辑；对必要的代码/数据修补单独标注。
> 配套可视化预览：`Docs/PixelWasteland_Codex_Preview.html`（可交互，含「当前风格 / 像素废土」对比与美术规范页）。

---

## 0. 结论摘要

| 项目 | 现状 | 目标 |
|---|---|---|
| 整体风格 | 深色写实石/木材质、高分辨率手绘贴图 | 16 色系废土像素、硬边、4px 栅格 |
| 字体 | 思源黑体 SDF（柔边） | 方舟像素字体 12px（RASTER 采样） |
| 贴图 | `GUI_Parts` 512×256 手绘九宫格 | 128×128 以内像素九宫格，Point 采样 |
| 图标 | 3D 渲染小图 / 无图标 | 16×16 手绘像素图标 |
| 背景 | 一张深色石纹铺满 | 480×270 像素废土天际线 ×4 放大 |
| 动效 | 无 / 默认 Tween | 8 帧步进、瞬时切换、CRT 扫描线 |

**改造量**：约 **10 类九宫格贴图 + 28 个图标 + 1 张背景 + 2 个 Prefab 重制 + 1 个场景整体换肤**。
预估 **3.5 ~ 5 人日**（美术 2.5 日 + Unity 实现 1.5 日 + 联调 0.5 日）。

---

## 1. 项目理解

### 1.1 项目定位

`DoudizhuTower`（斗地主塔防）：把**斗地主牌型**当作**塔防出兵指令**的 2D 混合玩法。

- 玩家出牌（单张 / 对子 / 顺子 / 飞机 / 炸弹 / 王炸）→ 按牌型召唤单位 → 单位沿路线推进 → 攻破建筑与 BOSS。
- 核心循环：**出牌 → 出兵 → 推进 → 经济 → 再出牌**（`EconomySystem` / `CardTypeDetector` / `BattleManager`）。
- 支持单机与联机（Photon Fusion，`NetworkFacade` / `FusionBattleManager`）。

**Codex（图鉴）在游戏中的位置**：不属于核心循环，是**长期留存驱动的收集系统**——玩家通过战斗解锁条目（`SaveSystem.IsCodexEntryUnlocked / UnlockCodexEntry`），在图鉴里查阅牌型、BOSS、建筑、被动、规则。它承担「世界观包装 + 新手教学 + 收集欲」三重职责，因此**风格表现力优先级高**：玩家会在这里停留阅读，废土包装能把「斗地主 + 塔防」这个混搭题材统一到一个可信的世界观里。

### 1.2 技术基线（已实测）

| 项 | 值 |
|---|---|
| Unity | `2023.2.20f1c1` |
| 渲染管线 | **Built-in**（非 URP） |
| 目标平台 | `StandaloneWindows64` |
| 2D 包 | `com.unity.feature.2d 2.0.0`（含 `2d.pixel-perfect 5.0.3`、2D Sprite/Animation/Tilemap） |
| UI | `com.unity.ugui 2.0.0`（uGUI + TextMeshPro） |
| 网络 | Photon Fusion |
| 场景 | `Assets/Scenes/Codex.unity`，Build Index 存在于 `Assets/Scenes/` |

**渲染管线是 Built-in 这一点的直接影响**：不能用 URP 的 Renderer Feature 做全屏像素化/扫描线后处理，方案必须走「UI 叠加层 + Point 采样」路线（详见 2.3）。

### 1.3 Codex 场景现状审计

**Canvas 配置**

| 参数 | 值 |
|---|---|
| Render Mode | `ScreenSpaceOverlay` |
| Canvas Scaler | `ScaleWithScreenSize` |
| Reference Resolution | `1920 × 1080` |
| Match | `0.5` |
| 场景图层级 | `Canvas` / `EventSystem` / `Main Camera`（正交） |

**UI 对象清单（25 个 Graphic，逐个体检）**

| 路径 | 类型 | 尺寸 / 锚点 | 当前贴图 | 问题 |
|---|---|---|---|---|
| `BackGround` | Image | Stretch 全屏 | `barmid_ready` | 512×256 石纹被拉伸到 1920×1080，严重模糊 |
| `TopBar/BackButton` | Image | 200×60 @左上 | `button_ready_on` | 手绘木牌，与像素风冲突 |
| `TopBar/BackButton/Text (TMP)` | TMP | — | 24px 思源黑体 | 字号非 12 倍数 |
| `MainArea/LeftPanel` | Image | 700×780 | `Mini_background` | 手绘石纹 |
| `LeftPanel/SearchInput` | Image | 600×60 | `Hp_frame` | 边框贴图非九宫格 |
| `SearchInput/Text Area/Placeholder` | TMP | — | 36px | 过大 |
| `LeftPanel/Scroll View` | Image | 650×650, α=0.39 | `mid_background` | 半透明白底，读不清 |
| `Scroll View/Viewport` | Image | — | `mid_background` | 与父级重复 |
| `Scroll View/Scrollbar Vertical` | Image | 20 宽 | `Hp_frame` | 非九宫格 |
| `.../Sliding Area/Handle` | Image | 20×20 | `Hp_line` | 同上 |
| `MainArea/RightPanel` | Image | 1100×780 | `Mini_background` | 手绘石纹 |
| `RightPanel/TitleText` | TMP | 36px | 占位「标题名称」 | 字号/颜色未定型 |
| `RightPanel/IconImage` | Image | 180×180 | **无贴图** | 空图 |
| `IconImage/GameObject` | Image | 200×200 | `Mini_frame2` | 装饰框 |
| `RightPanel/DescirptionText` | TMP | 30px | 占位「New Text」 | 拼写错误 + 占位 |
| `RightPanel/ExtraInfoContainer/ExtraInfoText` | TMP | 30px | 占位「New Text」 | 占位 |
| `CategoryTabs` | Transform | — | 运行时生成 | 空容器 |
| `Content` | Transform | — | 运行时生成 | 空容器 |
| `Main Camera` | Camera | 正交 | — | 纯 UI 场景，可保留做背景层 |

**数据与绑定状态（关键发现）**

| 项 | 实测 | 影响 |
|---|---|---|
| `categoryDatabases` | 6 槽位，**4 个已绑**（CardValues / CardType / BOSS / Rules），**Building、Passive 为空** | 这两个分类点进去是空列表 |
| 条目总数 | CardValues 13 + CardType 12 + BOSS 1 + Rules 2 = **28** | 图标需求量 = 28 |
| 条目 `Id` 字段 | **全部为空** | `SaveSystem` 解锁键全部相同 → 解锁一个等于解锁全部；`CodexDatabase.GetById` 索引失效 |
| `Icon` 字段 | 多数 `{fileID: 0}`；`王国骑士` 绑的是 3D 渲染图 | 图标需整体重做 |
| `Keywords` / `RelatedEntries` | 全部为空 | 搜索与「关联档案」功能实际无处可用 |
| `unlockStatusText` | `{fileID: 0}` 未绑 | 解锁状态文字不显示 |
| `relatedEntriesParent` / `relatedEntryPrefab` | `{fileID: 0}` 未绑 | 关联条目区不生成 |
| `progressText` | `{fileID: 0}` 未绑 | 进度不显示 |

> **结论**：这个场景目前是「皮换了、骨头还没接完」的状态——数据有 28 条真实内容，但 Id/图标/关联字段是空的，三个 UI 字段没绑；同时美术全是写实手绘。
> 因此本方案把「换肤」和「补齐最小可用绑定」放在同一条主线上：先补数据骨架，再换皮，避免美术做完发现界面跑不出内容。

**已存在的可复用工具**

- `Assets/Editor/ReplaceAllTMPFonts.cs` → 菜单 `Tools/替换场景所有 TMP 字体`：选中一个 `TMP_FontAsset` 即可把**本场景**所有 TMP 文本换成该字体，并清理 fallback 与残留 `TMP_SubMeshUI`。→ 换像素字体时直接用，不用手点。
- `CodexDatabase.ImportFromCsv(string csvPath)` + `Assets/Editor/CsvIO.cs`：支持 `Id,DisplayName,Category,Description,ExtraInfo,Keywords(|分隔)` 的批量导入。→ 补 Id/关键词/关联时用 CSV 批量刷，比手填 28 条快。

---

## 2. 风格定义（Style Bible）

### 2.1 关键词与三条硬原则

**关键词**：锈蚀钢板 / 沥青 / 辐射尘 / 警示条纹 / 铆钉 / CRT 终端 / 拾荒拼装 / 落日沙尘。

**三条不可妥协的原则**

1. **硬边**：所有边缘 1px 实边，禁止半透明过渡像素、禁止羽化、禁止抗锯齿描边。
2. **有限色**：单张资产 ≤ 8 色，全局 ≤ 24 色；颜色全部从调色板取，不新造。
3. **整数倍**：图形资产只按 ×1 / ×2 / ×4 显示；字号只取 12 的整数倍。

### 2.2 调色板（24 色）

| 组 | 用途 | 色值 |
|---|---|---|
| 暗部 6 | 沥青 → 磨边 | `#0B0908` `#191412` `#2B2320` `#3F342C` `#574A3F` `#726151` |
| 锈 3 | 锈蚀层次 | `#4F2617` `#7D3A1F` `#A85226` |
| 强调 1 | 警示橙（交互/高亮） | `#F0A050` |
| 毒 1 | 辐射绿（成功/激活） | `#A8EE54` |
| 危 1 | 血锈红（未解锁/警告） | `#C04A38` |
| 字 3 | 骨白 / 骨灰 / 陈灰 | `#E9DCC0` `#AB9B80` `#776853` |
| 屏 2 | 屏幕底 / 磷光 | `#0A1310` `#6ADFA6` |
| 天 4 | 夜空 / 暮土 / 尘光 / 远日 | `#422B3C` `#573022` `#9C4E22` `#E08D3F` |
| 景 3 | 远景楼 / 中景楼 / 近景废墟 | `#3A2530` `#241619` `#150E0C` |

**用色规则**

- 正文 `#E9DCC0`；次级说明 `#AB9B80`；禁用/未解锁 `#776853`。
- 交互高亮只用**警示橙**（hover/选中边框）；成功态只用**辐射绿**；危险只用**血锈红**。
- 大色块面积顺序：暗部 > 锈 > 天；**毒绿面积 < 3%**，否则失去废土感变赛博朋克。

### 2.3 像素密度与缩放策略（本项目最容易翻车的地方）

**问题**：Built-in 管线 + `ScaleWithScreenSize(1920×1080, match 0.5)` 在非 1080p 分辨率下会得到**非整数缩放**（如 1600×900 → 0.833），像素贴图的 1px 边会变成 0.83px → 出现**宽度不一致的边框**（有的边 1px、有的边不显示）。

**方案（分三档，按严格程度递增，建议先做 A，视验收情况再升 B）**

| 档 | 做法 | 代价 |
|---|---|---|
| **A（推荐起步）** | 贴图**按 1920×1080 下的最终屏幕像素 1:1 出图**，Canvas 保持现状。允许 0.75~1.5 的非整数缩放。 | 边缘会有 1px 抖动，但整体可接受；零代码改动 |
| **B（严格）** | Canvas Scaler 改 `ConstantPixelSize`，新增 `UIPixelPerfectScaler` 脚本把 `scaleFactor` 量化为 `0.5` 的整数倍（0.5 / 1 / 1.5 / 2），余量做黑边或裁切。 | 需处理超宽/超窄屏适配 |
| **C（战斗场景适用）** | 2D 相机加 `PixelPerfectCamera`（含在 `2d.pixel-perfect` 包内），`Assets PPU = 32`，`Reference Resolution = 480×270`。 | 仅适用于世界空间，不适用于 Overlay UI |

> **注意**：`PixelPerfectCamera` 组件作用于**正交相机渲染**，对 `ScreenSpaceOverlay` 的 UI **不生效**。UI 的像素对齐只能靠 A/B 两档。这是本次改造最需要在验收时盯住的点。

**背景的特殊处理**：像素背景**不要**画 1920×1080 的原图（文件大且像素点太小）。

- 画 **480×270**，用 UI Image 铺满并放大 **4×**（1920×1080）。
- 导入设置：`Filter Mode = Point (no filter)`，`Max Size ≥ 512`，`Compression = None`。
- 放大 4 倍后每个美术像素 = 4×4 屏幕像素，与 1:1 的 UI 边框形成**清晰的层级差**（背景粗、UI 细），这正是像素游戏的标准做法。

### 2.4 字体方案

**选型**：**方舟像素字体 12px 比例版**（Ark Pixel Font，TakWolf，SIL OFL 1.1）——覆盖简繁中文，有 10/12px 两档，比例版对拉丁/数字更友好。

**TMP 生成设置**（关键，设错就变糊）

| 参数 | 值 | 原因 |
|---|---|---|
| Sampling Point Size | `12`（或 24 做 2× 精度） | 与字体原生像素网格一致 |
| Padding | `0` | 任何 padding 都会引入插值 |
| Rendering Mode | **`RASTER`** | 用 SDF 会得到柔边，直接毁掉像素感 |
| Atlas Render Mode | `SMOOTH_HINTED` 或关闭抗锯齿 | — |
| 材质 Shader | `TextMeshPro/Bitmap` | 配合 RASTER |
| 图集尺寸 | `2048×2048`，按需加 `1024` 补充集 | 中文常用字约 3500 个 |
| Fallback | **清空** | 已有 `ReplaceAllTMPFonts` 会自动清 |

**字号阶梯**（全部为 12 的整数倍）

| 层级 | 字号 | 用途 |
|---|---|---|
| Display | 48 | 条目大标题 `TitleText` |
| H1 | 36 | 终端标题、分区标题 |
| Body | 24 | 正文、列表项、页签、按钮 |
| Label | 18 | 关键词、编号、次级标签 |
| Meta | 12 | 英文小字、版本号、状态栏 |

**落地步骤**：
1. 导入 `ark-pixel-12px-proportional-zh_cn.ttf`。
2. `Window > TextMeshPro > Font Asset Creator` 按上表生成 `ArkPixel12 SDF.asset`（命名保留 SDF 后缀但用 RASTER 模式，避免工程里引用混乱）。
3. 打开 `Codex.unity`，选中该 Font Asset，执行菜单 `Tools/替换场景所有 TMP 字体`。
4. 再按字号阶梯逐个改字号（工具只换字体不换字号）。

> 若美术不接受像素字体（中文小字号可读性争议），退路是：**标题用像素字体，正文用思源黑体**（保持 `#E9DCC0` + 24px）。这属于「半像素风」，会明显削弱风格统一度，不建议作为首选。

### 2.5 材质与质感（5 类，够覆盖全场景）

| 材质 | 视觉特征 | 用于 |
|---|---|---|
| **锈蚀钢板** | 深色底 + 顶部 4px 冷钢高光 + 底部 4px 深阴影 + 四角铆钉 + 随机锈斑 | 左右主面板、页签 |
| **凹陷屏幕** | 近黑底 + 内投影 + 底部 2px 磷光边 + 细扫描线 | 搜索框、列表凹槽、图标插槽 |
| **警示条纹** | 45° 斜纹，橙 `#F0A050` / 黑 `#17120E`，8px 一节 | 标题下沿、警告框顶边 |
| **混凝土** | 中灰 + 噪点 + 裂缝（1px 折线） | 按钮底、进度条槽 |
| **玻璃/CRT** | 极低透明度横向扫描线 + 暗角 | 全屏覆盖层 |

**禁止**：渐变（除 CRT 覆盖层）、圆角 > 4px、投影模糊 > 0、发光 > 1 层。

### 2.6 动效规范

| 交互 | 表现 | 时长 |
|---|---|---|
| 按钮 hover | 背景升一档色 + 2px 橙色描边 | 瞬时（0 帧） |
| 按钮按下 | 内容位移 `(2,2)`，阴影收缩 | 瞬时 |
| 页签切换 | 激活页签下沉 2px + 顶部 4px 强调条 | 瞬时 |
| 列表选中 | 左侧 4px 辐射绿竖条 + 背景渐隐（硬边） | 瞬时 |
| 解档 | 8 帧步进：噪点闪 3 帧 → 文字逐字显现 5 帧 | 200ms，`step` 曲线 |
| 扫描线 | 4×4 贴图平铺，不滚动（滚动会晕） | 静态 |

> 一律使用 `Mathf.Step` / 帧动画，**禁止** `SmoothStep` 缓动——缓动是写实 UI 的语言，不是像素的。

---

## 3. 资产清单

### 3.1 九宫格切片（10 类）

> 九宫格的硬性约束：`宽 - 左 - 右 > 0` 且 `高 - 上 - 下 > 0`，否则中间没有可拉伸区，Unity 会警告。

| # | 资源名 | 贴图尺寸 | 边框 L/R/T/B | 用途 |
|---|---|---|---|---|
| 1 | `panel_large` | 128×128 | 48 / 48 / 48 / 48 | 左右主面板 |
| 2 | `panel_inset` | 128×128 | 40 / 40 / 40 / 40 | 搜索框、列表凹槽 |
| 3 | `btn_normal` | 96×48 | 32 / 32 / 12 / 12 | 返回、解档（正常） |
| 4 | `btn_hover` | 96×48 | 同上 | hover / 选中态 |
| 5 | `btn_press` | 96×48 | 同上 | 按下态 |
| 6 | `tab_idle` | 64×64 | 24 / 24 / 12 / 12 | 页签（未选） |
| 7 | `tab_active` | 64×64 | 同上 | 页签（选中） |
| 8 | `socket_frame` | 128×128 | 48 / 48 / 48 / 48 | 图标插槽 |
| 9 | `scroll_track` / `scroll_thumb` | 16×16 | 5 / 5 / 5 / 5 | 滚动条（可平铺） |
| 10 | `hazard_strip` | 32×8 | 平铺，无边框 | 警示条 |
| 附 | `extra_bg` | 128×128 | 24 / 24 / 24 / 24 | 「数据 / DATA」信息块 |

### 3.2 图标（28 个 + 6 个分类图标）

- 规格：**16×16** 原生网格，单色为主（线 `#0B0908` + 填充 `#E9DCC0` + 1 个强调色）。
- 显示尺寸：列表 `×2 = 32px`；插槽 `×6 = 96px`；规范页 `×1/×2/×4`。
- 打包：一张 `CodexIcons` Sprite Atlas（2048×2048 足够），`Allow Rotation = off`，`Tight Packing = off`（避免像素被重新裁切）。
- 分类图标复用：`card`（卡牌数值）/ `ladder`（牌型）/ `skull`（BOSS）/ `tower`（建筑）/ `chip`（被动）/ `doc`（规则）。

### 3.3 背景

| 资源 | 尺寸 | 说明 |
|---|---|---|
| `codex_bg` | 480×270 → 显示 1920×1080 | 废土城市天际线：分层天空色带（12 带）+ 远景/中景/近景三层剪影 + 地平线雾 + 前景废墟（带落日边缘光）+ 辐射积水 |
| `scanline_overlay` | 4×4 | 2px 黑 / 2px 透明，平铺，α≈0.2 |

### 3.4 统一导入设置（所有像素资产）

```
Texture Type      : Sprite (2D and UI)
Sprite Mode       : Single（图集内用 Multiple）
Mesh Type         : Full Rect        ← 必须，Tight 会导致 9-slice 拉伸错误
Pixels Per Unit    : 16（图标）/ 100（UI 面板）
Generate Mip Maps : off              ← 必须
Filter Mode       : Point (no filter) ← 必须
Compression       : None             ← 必须（压缩会产生色块）
Max Size          : ≥ 贴图尺寸
Wrap Mode         : Clamp
sRGB              : on
Sprite Editor → Border : 按 3.1 表填写
```

> 这 4 个「必须」项（Full Rect / Mip off / Point / None）任何一项设错，画面立刻回到「糊」的状态。建议做成 `AssetPostprocessor` 自动设置（见 §6）。

---

## 4. 场景改造映射表

> 坐标基于 1920×1080 参考分辨率。建议顺手把左右面板改成**左右各留 40px 边距**（现在 LeftPanel 贴左边 0，RightPanel 右侧留 120，视觉不平衡）。

| 现有对象 | 改造后 | 关键参数 |
|---|---|---|
| `BackGround` | `codex_bg` + `scanline_overlay` | Image 铺满；Point；背景 4× 放大 |
| `TopBar/BackButton` | `btn_normal` 九宫格 | 200×60；图标 `←` 16px ×2；文字 24px |
| `TopBar`（新增） | 顶部标题条 | 半透明黑渐变 + 底部 `hazard_strip` 6px + 终端名 36px |
| `MainArea/LeftPanel` | `panel_large` | 700×670；内边距 24；四角铆钉 |
| `LeftPanel` 头部（新增） | 「数据库 / DATABASE」 | 24px + 12px 英文；底部 2px 分隔线 |
| `LeftPanel/SearchInput` | `panel_inset` | 600×56；左侧放大镜图标 32px ×2；文字 24px 磷光 `#6ADFA6` |
| `LeftPanel/Scroll View` | 移除背景图 | 只留 `RectMask2D`；凹槽由 `panel_inset` 提供 |
| `Scroll View/Scrollbar Vertical` + `Handle` | `scroll_track` + `scroll_thumb` | 宽 14；thumb 最小高 40 |
| `MainArea/RightPanel` | `panel_large` | 1100×670；内边距 26 |
| `RightPanel/TitleText` | 48px 像素字体 | 颜色 `#E9DCC0`，阴影 `4,4` 纯黑硬投影 |
| `RightPanel/IconImage` | 96×96 图标（16×16 ×6） | 居中于插槽 |
| `IconImage/GameObject` | `socket_frame` 九宫格 | 240×240；内投影 8px |
| `RightPanel/DescirptionText` | 24px，行高 1.85 | 颜色 `#E9DCC0`；两端对齐 |
| `RightPanel/ExtraInfoContainer` | `extra_bg` + 顶部 `hazard_strip` | 内边距 16；标签 18px 橙色 |
| `ExtraInfoContainer/ExtraInfoText` | `font-size`... 18px 等宽感 | 颜色 `#AB9B80` |
| `CategoryTabs` | 6 个 `tab_idle/active` | 每个 295×64；间距 14；激活下沉 2px |
| `Content` | 列表项 | 项高 74；左侧 4px 选中条 |
| **新增** `ProgressBar` | 26 段方块进度条 | 按 `已解档/总数` 点亮橙色 |
| **新增** `UnlockStamp` | 旋转 -4° 印章 | `已解档` 绿 / `未解档` 红 |
| **新增** `RelatedPanel` | 关联档案 chips | `rel-btn` 九宫格 |
| **新增** `ScanlineOverlay` | 最上层 Image | 全屏，`raycastTarget = false` |

**Prefab 重制**（两个都要重做，不只是换图）

| Prefab | 现状 | 目标 |
|---|---|---|
| `Assets/Prefabs/UI/UIPrefabs/Codex/CategoryTab.prefab` | 250×60，单个 `button_ready_on` + 文本 | 295×64，`tab_idle`/`tab_active` 双态（`SpriteState`），文本 24px + 英文代码角标 12px |
| `Assets/Prefabs/UI/UIPrefabs/Codex/EntryButton.prefab` | 600×60，单背景 + 文本 | 660×74，`panel_inset` 背景 + 图标 32px + 名称 24px + 编号 12px + 锁图标 16px |

> `EntryButton.prefab` 现在只有一个子物体，而 `CodexUIController.RefreshList()` 会去 `transform.Find("Icon")` 和 `transform.Find("UnlockIcon")` —— 这两个子节点**目前不存在**，所以图标与解锁标记永远不会显示。重制 Prefab 时必须按这两个名字建子节点，否则代码找不到。

---

## 5. 分阶段实施

### P0 · 基线冻结（0.5 日）

1. 打开 `Codex.unity`，全屏截图存档（改造前后对比用）。
2. 导出当前 Graphic 清单（对象名 / sprite / color / rect），确认 §1.3 的表。
3. 建目录：
   ```
   Assets/Art/PixelWasteland/
     UI/      (9-slice)
     Icons/   (16x16)
     BG/      (480x270)
     Fonts/
   ```
4. **先把场景当前状态 commit 一次**（或整包备份），确保可回退。

### P1 · 风格定义与字体接入（0.5 日）

1. 按 §2.2 建一份调色板（建议做成 `.aseprite` 调色板文件 + 一张 `palette.png` 参考图放同目录）。
2. 导入方舟像素字体 → 生成 TMP Font Asset（§2.4 设置）。
3. 打开 `Codex.unity` → 选中字体 → `Tools/替换场景所有 TMP 字体`。
4. 按字号阶梯改字号（12/18/24/36/48）。

> 此时画面会「字变了、图没变」，会很难看，属正常中间态。

### P2 · 贴图生产（美术，2.5 日）

按 §3 出图。建议顺序（先出能覆盖最多界面的）：

1. `panel_large` + `panel_inset` → 面板骨架立住，能看到 60% 效果。
2. `btn_*` + `tab_*` → 交互件。
3. `codex_bg` → 背景定调。
4. `socket_frame` + 28 个图标 → 细节。
5. `scroll_*` / `hazard_strip` / `extra_bg` → 收尾。

**验收节点**：`panel_large` + `codex_bg` + 字体三件套到位时，截一张图给策划/主美确认风格方向，避免后面 28 个图标白画。

### P3 · 场景换肤（1 日）

1. 逐对象按 §4 表替换 Sprite、改 RectTransform、改字号。
2. 重制两个 Prefab（注意 `Icon` / `UnlockIcon` 子节点命名）。
3. 新增 `ProgressBar` / `UnlockStamp` / `RelatedPanel` / `ScanlineOverlay` 四个节点。
4. 补 `unlockStatusText` / `relatedEntriesParent` / `relatedEntryPrefab` / `progressText` 四个字段绑定（现在全是 `{fileID: 0}`）。
5. 修正左右面板边距（左右各 40）。

### P4 · 打磨（0.5 日）

1. 解档动效：8 帧步进（噪点 3 帧 + 逐字 5 帧），用协程 + `SetTextAlpha`，不用 DOTween 缓动。
2. 列表滚动条贴图对齐（thumb 最小高度、轨道九宫格）。
3. 在 1600×900 / 1920×1080 / 2560×1440 三档截图，检查边框是否出现 1px 宽度跳动（详见 §2.3 A/B 档）。
4. 深色环境下对比度检查：正文 `#E9DCC0` on `#16110E` 对比度约 12:1，达标；`#776853` 仅用于非关键信息。

### P5 · 数据补齐与联调（0.5 日）

1. 给 28 个 `CodexEntry` 补 `Id`（建议 = 资源名拼音，如 `cardtype-single` / `boss-knight`），否则解锁系统是坏的。
2. 用 CSV 批量补 `Keywords`（每个条目 2~4 个）与 `RelatedEntries`。
3. 补 `Building` / `Passive` 两个 `CodexDatabase` 槽位（现在为 null）。
4. 跑一次完整流程：进场景 → 切 6 个分类 → 搜索 → 点条目 → 解锁 → 看进度与关联。

---

## 6. 代码改动点（最小必要）

| 文件 | 改动 | 必要性 |
|---|---|---|
| 新增 `Assets/Scripts/UI/UIPixelPerfectScaler.cs` | 仅在选择 B 档时：把 `CanvasScaler` 切 `ConstantPixelSize`，按 `Mathf.Max(1, Mathf.FloorToInt(screenW / 1920f * 2f)) / 2f` 量化缩放 | 可选（严格档） |
| 新增 `Assets/Editor/PixelArtImportSettings.cs` | `AssetPostprocessor.OnPreprocessTexture`：路径含 `Art/PixelWasteland/` 时强制 `Point / no mipmap / None / FullRect` | **强烈建议**，防止手动设错 |
| 修改 `CodexUIController.cs` | `RefreshList()` 里 `transform.Find("Icon")` 改为容错并统一用 `GetComponentInChildren`；`UpdateProgress()` 接进度条 | 必须（配合新 Prefab） |
| 修改 `CodexEntry.cs` | `Id` 加 `[Tooltip]` 并在 `OnValidate` 里从资源名自动填充 | 建议（解决 §1.3 的 Id 空问题） |
| 新增 `Assets/Editor/CodexDataFixer.cs` | 一键为 28 个条目补 Id、写 Keywords 模板、校验数据库槽位 | 建议 |

**不建议**在这轮做的：
- 全屏后处理（Built-in 下 `OnRenderImage` 会打断 UI 合批，且 `ScreenSpaceOverlay` 根本不在相机渲染路径里，后处理对 UI **无效**）。扫描线只能用 UI 叠加层实现。
- 引入 URP（改造面太大，收益只在这个场景）。

---

## 7. 验收清单

- [ ] 所有像素贴图：`Point / no mipmap / None / FullRect`（用 `Debug` 面板逐张抽查）
- [ ] 所有九宫格：中间区 > 0，四角不拉伸（拉一个长条面板看角是否变形）
- [ ] 字体：TMP 材质为 `TMP_Bitmap`，无 SDF 柔边；字号全为 12 的倍数
- [ ] 字号最小 ≥ 18px（1080p 下等效 18px，低于此中文不可读）
- [ ] 列表 `Icon` / `UnlockIcon` 子节点存在且能显示
- [ ] 6 个分类都能切，且 Building / Passive 有数据
- [ ] 解锁一个条目 → 进度 +1，且**不会**把所有条目一起解锁（验证 Id 已补）
- [ ] 搜索关键词能命中
- [ ] 关联档案能跳转
- [ ] 1600×900 / 1920×1080 / 2560×1440 三档无边框抖动
- [ ] 对比度：正文 ≥ 7:1，次级信息 ≥ 4.5:1
- [ ] 与 `MainMenu` 并排看，风格不割裂（或明确接受「只改 Codex」的过渡态）

---

## 8. 风险与回退

| 风险 | 概率 | 影响 | 对策 |
|---|---|---|---|
| 中文像素字体小字号可读性差 | 中 | 高 | 字号下限 18px；正文可退回思源黑体（半像素风） |
| 非整数缩放导致边框抖动 | 高 | 中 | 先做 A 档验收，不行再上 B 档整数缩放 |
| 28 个图标工作量超预期 | 中 | 高 | 先复用 6 个分类图标，条目图标分批补；不可用则用「分类图标 + 序号」降级 |
| 与其余场景（MainMenu / 战斗）风格割裂 | 高 | 中 | 明确定位为「第一刀」：Codex 先试水，验收后按同一 Style Bible 推广 |
| 像素贴图与现有 3D 渲染单位（`AnotherPhoto` 资源）混搭违和 | 高 | 中 | 本轮不碰战斗单位；若推广，单位需整体重画或加像素化 Shader |
| `Id` 为空导致的解锁逻辑错误被误判为「美术问题」 | 中 | 中 | 放到 P5 先修数据再验收 |

**回退方案**：P0 的 commit 就是回退点。所有旧贴图不删除（`GUI_Parts` 保持原样），只替换引用，可随时指回。

---

## 9. 工作量估算

| 阶段 | 角色 | 人日 |
|---|---|---|
| P0 基线冻结 | Unity | 0.5 |
| P1 风格定义 + 字体 | 主美 + Unity | 0.5 |
| P2 贴图生产（10 九宫格 + 28 图标 + 1 背景） | 像素美术 | 2.5 |
| P3 场景换肤 + Prefab 重制 | Unity | 1.0 |
| P4 打磨 | Unity | 0.5 |
| P5 数据补齐 + 联调 | 策划 + Unity | 0.5 |
| **合计** | | **5.5 人日**（并行可压到 3.5） |

**关键路径**：`panel_large` + `codex_bg` + 像素字体 → 决定风格是否成立，其余都是填充。建议 **P2 前 0.5 日只做这三件**，先开一次评审。

---

## 附录 A：本次预览的实现对应关系

`Docs/PixelWasteland_Codex_Preview.html` 里的每一块都能对应到 Unity 侧的具体资产，可作为「目标态参照图」直接给美术：

| 预览中的元素 | 对应 Unity 资产 |
|---|---|
| 面板外框 + 铆钉 | `panel_large`（128×128, 48/16） |
| 搜索框凹陷 | `panel_inset` |
| 页签（选中态下沉） | `tab_idle` / `tab_active` |
| 列表行 / 选中竖条 | `EntryButton.prefab` |
| 图标插槽 | `socket_frame` + 96×96 图标 |
| 「数据 / DATA」块 | `extra_bg` + `hazard_strip` |
| 进度条 26 段 | `ProgressBar`（新增） |
| 「已解档」印章 | `UnlockStamp`（新增） |
| 全屏细横线 | `scanline_overlay`（4×4） |
| 背景城市剪影 | `codex_bg`（480×270） |
| 字体 | 方舟像素字体 12px |

预览页右上角 `?mode=fantasy` 可切到「当前风格」做同机位对比；`?view=spec` 直达美术规范页。

---

## 附录 B：像素字体许可

本预览内嵌了 `方舟像素字体 Ark Pixel 12px 比例版` 的**子集**（仅预览用到的字符），用于保证预览与目标态一致。

- 授权：**SIL Open Font License 1.1**（可商用、可嵌入，需保留版权声明，衍生字体不得单独售卖）。
- 来源：`https://github.com/TakWolf/ark-pixel-font`
- 接入正式工程时请从官方 release 取完整 TTF，并按 OFL 要求随包附上 `LICENSE` 与版权声明。
