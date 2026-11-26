# PixelWasteland_Codex_Preview.html · 设计手法全解

> 对象：`Docs/PixelWasteland_Codex_Preview.html`
> 目的：把它"好看"的原因拆成可复用的手法，并标注在 Unity 里的落地方式。
> 每条格式：**手法** → 代码依据 → 为什么好看 → Unity 怎么实现

---

## 一、色彩系统：变量化 + 分组

**手法**：两套完整调色板，用 CSS 变量挂在 `:root` / `.mode-wasteland` / `.mode-fantasy` 上，同一套 class 结构换主题只改变量。

**依据**
```css
--sky-1..12        /* 天空 12 阶，夜空也有渐变层次 */
--steel-0..6       /* 钢灰 7 阶，从 #0b0908 到 #8d7a66 */
--rust-1..3        /* 锈色，用于强调 */
--bone/-dim/-dark  /* 骨白 3 阶，承载正文 */
--amber / --toxic / --blood / --phosphor  /* 功能色：强调 / 正常 / 危险 / 电子 */
--radius: 0px      /* 圆角归零，像素风硬约束 */
```

**为什么好看**：颜色不是"随手取"，而是**同色相的明度阶梯**（steel 7 阶、sky 12 阶）。所以界面里有大量"同一材质的不同受光面"，层次自然。

**Unity**：一个 `CodexPalette` ScriptableObject，字段与变量一一对应。所有组件读它，换主题只换 SO。

---

## 二、立体感三板斧（关键：不用真阴影）

**手法 1：4px 黑描边**
```css
border: 4px solid var(--steel-0);  /* #0b0908 */
```
像素风里描边就是轮廓，比任何抗锯齿都清晰。

**手法 2：inset 四边明暗 = 斜面**
```css
box-shadow:
  inset 0  4px 0 rgba(255,236,200,.12),  /* 顶：受光 */
  inset 4px 0  0 rgba(255,236,200,.05),  /* 左：次受光 */
  inset 0 -4px 0 rgba(0,0,0,.55),        /* 底：背光 */
  inset -4px 0 0 rgba(0,0,0,.35);        /* 右：次背光 */
```
**这是整套 UI 的"体积感"来源**——不是画出来的，是四条内阴影算出来的。光从左上来。

**手法 3：硬投影（零模糊）**
```css
--shadow-hard: 10px 10px 0 rgba(0,0,0,.5);   /* 面板 */
box-shadow: 4px 4px 0 rgba(0,0,0,.45);       /* 按钮 */
```
模糊阴影会让像素风"变糊"，硬偏移反而更贴纸感。

**手法 4：角钉**
```css
.panel::before{left:8px;top:8px}  .panel::after{right:8px;bottom:8px}
width:10px;height:10px;background:var(--steel-5);border:2px solid var(--steel-0);
box-shadow:inset 2px 2px 0 rgba(255,240,210,.35);
```
一颗铆钉 = 3 层（边框 / 底色 / 内高光）。四个角放两颗就够，多了就吵。

**Unity**：前三项做成**九宫格贴图**（已做）；角钉烘焙进贴图角落；硬投影用偏移的暗色 Image。

---

## 三、状态反馈（你最感兴趣的部分）

### 3.1 按钮：hover 升阶 + 描边光晕
```css
.btn:hover{
  background: var(--steel-3);   /* steel-2 → steel-3，升一阶 */
  color: #fff;
  box-shadow: …原有斜面…, 0 0 0 2px var(--amber-dim);  /* 外圈 2px 琥珀光晕 */
}
```
**妙处**：不是改边框，是**外面再加一圈 0 偏移的实色阴影**。等于凭空长出一个描边，不占布局空间。

### 3.2 按钮：active 按压（**重点**）
```css
.btn:active{
  transform: translate(3px,3px);
  box-shadow:
    inset -2px -2px 0 var(--panel-hi),   /* 左上高光 ← 跑到右下 */
    inset  2px  2px 0 rgba(0,0,0,.5),    /* 右下变暗 ← 跑到左上 */
    1px 1px 0 rgba(0,0,0,.5);            /* 投影从 4px 缩到 1px */
}
```
三个动作同时发生：
1. **位移 3px**（跟着鼠标走）
2. **斜面明暗反转**（光从左上变成从右下 → 视觉上"凹进去"）
3. **投影坍缩**（4px → 1px → 贴回地面）

这就是"按下去"的完整物理暗示，比单纯变暗高级得多。

### 3.3 选中行：**半透明渐变**（你说的那个效果）
```css
.row.sel{
  background: linear-gradient(90deg,
      rgba(168,238,84,.20),
      rgba(168,238,84,.02) 62%,
      transparent);
  border-left-color: var(--toxic);
  color: var(--toxic);
}
```
**要点**：
- 不是整块铺色，而是**从左往右衰减**，眼睛会顺着指向左侧列表
- 用 `rgba` 而不是实色，底下的面板纹理能透出来
- 左边 4px 亮条 + 文字同色，三处呼应

### 3.4 页签激活：竖向渐变 + 下移 2px
```css
.tab.on{
  color: var(--toxic);
  background: linear-gradient(180deg, rgba(168,238,84,.20), rgba(168,238,84,.05));
  box-shadow: inset 0 4px 0 var(--toxic-dim), inset 0 -4px 0 rgba(0,0,0,.45);
  transform: translateY(2px);   /* 物理上"按下去"一点 */
}
```
竖向衰减 + 顶部 4px 亮边，等于给页签"插进"下面的内容区。

### 3.5 关联按钮：hover 换色 + active 位移
```css
.rel-btn:hover{color:var(--toxic); border-color:var(--toxic-dim); background:rgba(168,238,84,.08)}
.rel-btn:active{transform:translate(2px,2px); box-shadow:inset 2px 2px 0 rgba(0,0,0,.5)}
```
小控件用小位移（2px），大按钮用 3px——**位移量与控件尺寸成正比**。

### 3.6 锁定态
```css
.row.locked{color:var(--bone-dark)} .row.locked .row-ico{opacity:.5}
```
不做灰度滤镜，只降文字色 + 图标 50% 透明。

**Unity 落地**：这些全部可以收进一个 `UIStateTint` 组件 + 色板 SO（见文末）。

---

## 四、半透明叠加的用法（不是随便调 alpha）

| 位置 | 用法 | 意图 |
|---|---|---|
| `.row.sel` | `rgba(168,238,84,.20 → transparent)` 横向渐变 | 方向性高亮 |
| `.tab.on` | `rgba(168,238,84,.20 → .05)` 纵向渐变 | 顶部光源 |
| `.chip` | `rgba(240,160,80,.10)` | 让芯片"浮"在面板上 |
| `.stamp` | `rgba(168,238,84,.08)` | 印章是盖上去的，半透明显得像墨水 |
| `.kw i` | `rgba(106,223,166,.06)` | 关键词是最弱层级 |
| `.extra` | `rgba(240,160,80,.07)` | 数据块是"注记"，不能抢正文 |
| `.rel-btn:hover` | `rgba(168,238,84,.08)` | 悬停只轻轻亮一下 |

**规律**：透明度按**层级重要性**递减 —— 选中 .20 > 芯片 .10 > 印章 .08 > 数据 .07 > 关键词 .06。这套"透明度阶梯"是界面显得有秩序的根本原因。

---

## 五、文字排版

```css
--fs-meta:12  --fs-label:18  --fs-body:24  --fs-h1:36  --fs-title:48
```
- **全部是 12 的倍数**——像素字体只有整数倍才不糊
- **字距分级**：正文 1px / 标签 2-3px / 标题 5px / 大标题 6px。字距越大越"机械"
- **硬投影**：`text-shadow: 3px 3px 0 rgba(0,0,0,.9)`（无模糊）
- **双字体分工**：`--font-px` 正文，`--font-mono` 数据/编号/状态（等宽对齐）
- **正文行高 1.85**，标签 1.1——正文松、标签紧

---

## 六、结构与节奏

- **固定栅格**：`grid-template-columns: 700px 1100px; gap: 40px`
- **8px 为单位**：padding 24 / gap 14 / margin 16
- **每个区块都有头-体-脚**：
  - 顶栏：brand + progress
  - 面板：`panel-hd`（标题+英文+计数）→ 内容 → `panel-ft`（计数+提示）
  - 详情：`d-head`（芯片+印章）→ 标题 → 分隔线 → 内容 → `d-foot`
- **分割线都有端帽**：`rule::before/::after` 放 10px 方块，两端"封口"

---

## 七、特效层（全部 `pointer-events:none`）

```css
.ov.scan   repeating-linear-gradient 2px on / 2px off, opacity .20
.ov.sweep  linear-gradient 竖直扫过, animation 9s linear infinite
.ov.vig    radial 压四角 + 上下线性压暗
.ov.glass  linear-gradient(118deg, 白 5% → 0)
```
**顺序**：背景 → 面板 → 内容 → 扫描线 → 暗角 → 玻璃。
注意 `.sweep` 的透明度只有 `.04`——**特效要"几乎看不见"才高级**。

---

## 八、提升完成度的小细节

| 细节 | 代码 | 作用 |
|---|---|---|
| 搜索框闪烁光标 | `@keyframes blink{0%,49%{opacity:1}50%,100%{opacity:0}}` `steps(1)` | 让人以为它是真的终端 |
| 危险条纹 | `repeating-linear-gradient(45deg, amber 0 8px, #17120e 8px 16px)` | 顶栏下沿 6px，警示感 |
| 数据块顶部条纹 | `.extra::before` 同上，`opacity:.6` | 注记框的"胶带" |
| 未解档 | 45° 斜纹 + 虚线框 + 红字 | 不用图标也能表达"禁止" |
| 印章倾斜 | `rotate(-4deg)` / 未解档 `rotate(4deg)` | 手盖的，不是打印的 |
| 进度条分段 | 28 段，`gap:3px`，on 段 `inset 0 -2px 0` | 像 LED 电量条 |
| 自定义滚动条 | track + thumb 都带 2px 描边和内高光 | 连滚动条都不放过 |
| 搜索框内嵌阴影 | `inset 0 6px 0 rgba(0,0,0,.7)` + 底部 2px 磷光辉光 | 凹陷 + 屏幕发光 |

---

## 九、可移植性设计（工程层面的优点）

1. **色值全走变量** → 换主题只改 `:root`
2. **组件类与状态类分离**（`.btn` / `.btn:hover` / `.btn:active`）→ 状态可独立调整
3. **字号/间距走变量** → 整体缩放只改一处
4. **主题用 class 切换**（`.mode-wasteland` / `.mode-fantasy`）→ 同一套 DOM 两套皮

---

## 十、搬到 Unity 的成本对照

| 手法 | Unity 实现 | 成本 | 建议 |
|---|---|---|---|
| 色彩变量 | 色板 SO | 低 | ✅ 必做 |
| 4px 描边 + inset 斜面 | 九宫格贴图烘焙 | 低 | ✅ 已做 |
| 硬投影 | 偏移暗色 Image | 低 | ✅ 已做 |
| **hover 升阶 + 光晕** | 组件改 Image.color + 边框 Image.color | 中 | ✅ **最值得做** |
| **active 位移 + 斜面反转** | 组件改 RectTransform + 换贴图 | 中 | ✅ **最值得做** |
| **选中行半透明渐变** | 渐变贴图（1 张）+ 拉伸 + 左边条 | 低 | ✅ **性价比最高** |
| 页签竖向渐变 | 同上一张竖向渐变图 | 低 | ✅ |
| 印章倾斜 | RectTransform.rotation | 极低 | ✅ 已做 |
| 扫描线 / 暗角 / 玻璃 | 全屏 RawImage | 低 | ✅ 已做 |
| 扫光动画 | 移动 RawImage 的 uvRect / 位移 | 低 | ✅ 已做 |
| 搜索框闪烁光标 | 一个 1×22 的 Image + 定时切换 | 低 | 可选 |
| 自定义滚动条 | Scrollbar 换九宫格 | 中 | 可选 |
| 正文行高 1.85 | TMP lineSpacing | 极低 | ✅ 已做 |
| 硬投影文字 | TMP 不支持 Bitmap 阴影 | ❌ | 用描边贴图替代 |

**结论**：真正难的只有两类 —— ①需要"改渲染"的（inset 阴影、渐变、光晕），②需要"多态素材"的（按钮按下换贴图）。其余全是参数，写一次组件就能全站复用。

---

## 附：Unity 落地的最小方案

一个 `UIStateTint` 组件（约 80 行）+ 一个 `CodexPalette` SO：

```
UIStateTint
  ├─ 引用：底色 Image / 边框 Image / 文字 TMP / 高亮条 Image
  ├─ 五态：normal / hover / press / selected / disabled
  ├─ 每态字段：底色 rgba、边框色、文字色、位移 Vector2、贴图（可选）
  └─ 监听：IPointerEnter / Exit / Down / Up + Selectable
```

挂到按钮、页签、列表行、关联芯片上，颜色从 `CodexPalette` 读。
**新增控件 = 挂组件 + 选色板**，不用再逐个调。
