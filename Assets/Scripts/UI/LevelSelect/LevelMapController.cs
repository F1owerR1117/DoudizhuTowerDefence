using System;
using System.Collections.Generic;
using System.Text;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 横向关卡地图主控。
    /// 只负责：排序并注册 LevelSceneNames → 生成节点与连线 → 选中与详情。
    /// 拖动/惯性/边界交给 ScrollRect，不在此处处理输入。
    /// </summary>
    public class LevelMapController : MonoBehaviour
    {
        [Header("关卡数据（来自 Resources/Config/LevelConfig）")]
        [SerializeField] private LevelConfig[] levelConfigs;

        [Header("场景引用")]
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private LevelMapNode nodePrefab;
        [SerializeField] private LevelDetailPanel detailPanel;

        [Header("布局")]
        [Tooltip("相邻节点水平间距")]
        [SerializeField] private float spacing = 420f;
        [Tooltip("地图左右留白（保证首尾节点不被裁切）")]
        [SerializeField] private float padding = 260f;
        [Tooltip("纵向起伏波形（按索引循环），单位像素")]
        [SerializeField] private float[] verticalPattern = { 0f, 70f, 20f, -60f, 0f };
        [Tooltip("右侧额外预留滚动空间（在自动计算的遮挡量之上再叠加）")]
        [SerializeField] private float extraRightPadding = 0f;
        [Tooltip("选中节点与详情面板之间的最小间距")]
        [SerializeField] private float nodePanelGap = 24f;
        [Tooltip("自动避让动画时长（秒）")]
        [SerializeField] private float avoidDuration = 0.15f;
        [Tooltip("节点参考尺寸，用于计算 Content 高度与边界")]
        [SerializeField] private Vector2 nodeSize = new Vector2(220f, 200f);
        [Tooltip("节点内的对比连线粗细")]
        [SerializeField] private float roadThickness = 14f;
        [SerializeField] private Color roadColor = new Color(0.42f, 0.31f, 0.22f, 1f);

        [Header("调试（仅开发用，默认关闭；不写任何资产或存档）")]
        [SerializeField] private bool debugLockEnabled = false;
        [Tooltip("按排序后的下标强制锁定，例如 2,3,4 表示第 3~5 关锁定")]
        [SerializeField] private int[] debugLockedIndices = new int[0];

        [Tooltip("迁移期开关：旧控制器仍启用时是否中止建图。正式交接后应保持 true")]
        [SerializeField] private bool abortIfLegacyActive = true;

        private readonly List<LevelConfig> _sorted = new List<LevelConfig>();
        private readonly List<LevelMapNode> _nodes = new List<LevelMapNode>();
        private LevelMapNode _selected;

        /// <summary>供外部读取的关卡顺序（与 LevelSceneNames 一一对应）</summary>
        public IReadOnlyList<LevelConfig> SortedLevels { get { return _sorted; } }

        private void Start()
        {
            BindBackButton();
            if (!AssertLegacyControllerDisabled()) return;
            BuildMap();
        }

        /// <summary>
        /// 返回主菜单。旧控制器被禁用后它的 Start 不会再注册监听，
        /// 因此由新控制器接管；若旧控制器仍启用则交给它，避免重复绑定。
        /// </summary>
        private void BindBackButton()
        {
            if (backButton == null) return;

            var legacy = FindObjectOfType<LevelSelectController>();
            if (legacy != null && legacy.isActiveAndEnabled) return;

            backButton.onClick.AddListener(() => SceneLoader.LoadMainMenu());
        }

        // ─────────────────────────────────────── 旧控制器防重复
        private bool AssertLegacyControllerDisabled()
        {
            var legacy = FindObjectOfType<LevelSelectController>();
            if (legacy != null && legacy.isActiveAndEnabled)
            {
                if (abortIfLegacyActive)
                {
                    Debug.LogError("[LevelMap] 旧的 LevelSelectController 仍在运行，已中止建图，避免重复生成卡片。");
                    return false;
                }
                Debug.LogWarning("[LevelMap] 旧控制器仍启用（迁移对照模式），继续建图。交接完成后请将其禁用。");
            }
            return true;
        }

        // ─────────────────────────────────────── 建图
        public void BuildMap()
        {
            if (nodePrefab == null || content == null)
            {
                Debug.LogError("[LevelMap] nodePrefab / content 未赋值");
                return;
            }

            // 1) 排序（唯一顺序来源）
            _sorted.Clear();
            if (levelConfigs != null)
                foreach (var c in levelConfigs)
                    if (c != null) _sorted.Add(c);
            _sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

            if (_sorted.Count == 0)
            {
                Debug.LogWarning("[LevelMap] 没有关卡配置");
                return;
            }

            // 2) 注册场景名（与旧逻辑一致，必须早于任何 SetCurrentLevel）
            SceneLoader.LevelSceneNames = _sorted.ConvertAll(c => c.sceneName).ToArray();

            // 3) 清旧节点（重复调用时安全）
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
            _nodes.Clear();
            _selected = null;

            // 4) 计算坐标与内容尺寸
            int n = _sorted.Count;
            float[] xs = new float[n];
            float[] ys = new float[n];
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                xs[i] = padding + i * spacing;
                ys[i] = PatternAt(i);
                minY = Mathf.Min(minY, ys[i]);
                maxY = Mathf.Max(maxY, ys[i]);
            }

            // 右侧预留：让「最后一个节点」能滚到详情面板左侧而不被遮挡。
            // 依据 = 节点半宽 + 面板在 viewport 中实际遮挡宽度 + 间距 - 既有 padding；
            // 遮挡宽度由面板贴右锚点与自身宽度解析得出，随 Canvas 缩放与 Viewport 尺寸自适应。
            float covered = (detailPanel != null && scrollRect != null && scrollRect.viewport != null)
                ? detailPanel.GetCoveredWidthIn(scrollRect.viewport) : 0f;
            float neededRight = nodeSize.x * 0.5f + covered + nodePanelGap - padding;
            float rightPadding = Mathf.Max(0f, neededRight) + Mathf.Max(0f, extraRightPadding);

            float contentWidth = padding * 2f + (n - 1) * spacing + rightPadding;

            float contentHeight = Mathf.Max(nodeSize.y, (maxY - minY) + nodeSize.y * 2f);

            // 规范 Content 锚点：左中对齐 + 点锚点，这样 sizeDelta 才是真实尺寸
            // （场景里原本是拉伸锚点，若不改会把大小叠加到视口尺寸上，导致节点被推出屏幕）
            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(contentWidth, contentHeight);

            // 5) 先画连线（置于节点下层）
            BuildRoads(xs, ys);

            // 6) 生成节点
            for (int i = 0; i < n; i++)
            {
                var node = Instantiate(nodePrefab, content);
                node.name = string.Format("Node_{0}_{1}", i, _sorted[i].levelName);
                var rt = (RectTransform)node.transform;
                // 强制左中锚点：坐标以内容左边缘为原点，不受预制体自带锚点影响
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = nodeSize;
                rt.anchoredPosition = new Vector2(xs[i], ys[i]);

                bool unlocked = ResolveUnlocked(i, _sorted[i]);
                node.Setup(_sorted[i], i, unlocked, OnNodeClicked);
                _nodes.Add(node);
            }

            // 用实测节点视觉宽度（含选中环）校正右侧预留，避免选中时贴到面板
            if (_nodes.Count > 0)
            {
                Bounds nb = RectTransformUtility.CalculateRelativeRectTransformBounds(content, _nodes[0].transform);
                float visualHalf = (nb.max.x - nb.min.x) * 0.5f;
                float extra = Mathf.Max(0f, visualHalf - nodeSize.x * 0.5f);
                if (extra > 0.01f)
                    content.sizeDelta = new Vector2(contentWidth + extra, contentHeight);
            }

            LogMapping();
            ScrollToStart();

            // 默认不打开详情面板
            if (detailPanel != null) detailPanel.ForceClose();
        }

        private float PatternAt(int i)
        {
            if (verticalPattern == null || verticalPattern.Length == 0) return 0f;
            return verticalPattern[i % verticalPattern.Length];
        }

        // ─────────────────────────────────────── 连线
        private void BuildRoads(float[] xs, float[] ys)
        {
            for (int i = 0; i < xs.Length - 1; i++)
            {
                Vector2 a = new Vector2(xs[i], ys[i]);
                Vector2 b = new Vector2(xs[i + 1], ys[i + 1]);
                Vector2 mid = (a + b) * 0.5f;
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len <= 0.01f) continue;

                var go = new GameObject("Road_" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(content, false);
                rt.SetAsFirstSibling();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(len, roadThickness);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

                var img = go.GetComponent<Image>();
                img.color = roadColor;         // 临时占位：正式道路贴图接入后替换此处
                img.raycastTarget = false;
            }
        }

        // ─────────────────────────────────────── 解锁判定
        /// <summary>正式逻辑 = config.isUnlocked；调试只在开启时做额外「否定」</summary>
        public bool ResolveUnlocked(int index, LevelConfig config)
        {
            if (config == null) return false;
            if (!debugLockEnabled) return config.isUnlocked;

            if (debugLockedIndices != null && Array.IndexOf(debugLockedIndices, index) >= 0)
                return false;

            return config.isUnlocked;
        }

        // ─────────────────────────────────────── 选中
        private void OnNodeClicked(LevelMapNode node)
        {
            if (node == null || !node.IsUnlocked) return;

            if (_selected != null && _selected != node) _selected.SetSelected(false);
            _selected = node;
            node.SetSelected(true);

            if (detailPanel != null) detailPanel.Show(node.Config, node.Index);

            // 打开详情后，若节点会被面板遮挡则自动把地图左移，使其完整露在面板左侧
            EnsureNodeVisible(node);
        }

        // ─────────────────────────────────────── 自动避让
        private Coroutine _avoidRoutine;

        /// <summary>取消正在进行的避让动画（拖动地图时调用）</summary>
        public void CancelScrollAnimation()
        {
            if (_avoidRoutine != null) { StopCoroutine(_avoidRoutine); _avoidRoutine = null; }
        }

        /// <summary>
        /// 若节点右边缘落进详情面板遮挡区，则平滑调整 Content 水平位置；
        /// 未被遮挡时不做任何移动。目标位置始终钳制在合法滚动范围内。
        /// </summary>
        private void EnsureNodeVisible(LevelMapNode node)
        {
            if (node == null || scrollRect == null || content == null) return;
            var viewport = scrollRect.viewport;
            if (viewport == null) return;
            if (detailPanel == null) return;

            Canvas.ForceUpdateCanvases();

            float covered = detailPanel.GetCoveredWidthIn(viewport);
            if (covered <= 0f) return;

            // 节点与面板在 viewport 本地坐标中的边界
            Bounds nb = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, node.transform);
            float limit = viewport.rect.xMax - covered - nodePanelGap;

            if (nb.max.x <= limit) return;               // 未被遮挡 → 不动

            float overflow = nb.max.x - limit;
            float currentOffset = -content.anchoredPosition.x;
            float maxScroll = Mathf.Max(0f, content.rect.width - viewport.rect.width);
            float targetOffset = Mathf.Clamp(currentOffset + overflow, 0f, maxScroll);

            if (Mathf.Approximately(targetOffset, currentOffset)) return;

            CancelScrollAnimation();
            _avoidRoutine = StartCoroutine(ScrollToOffset(targetOffset));
        }

        private System.Collections.IEnumerator ScrollToOffset(float targetOffset)
        {
            float startOffset = -content.anchoredPosition.x;
            float t = 0f;
            float dur = Mathf.Max(0.01f, avoidDuration);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                k = 1f - (1f - k) * (1f - k);            // ease out
                float o = Mathf.LerpUnclamped(startOffset, targetOffset, k);
                content.anchoredPosition = new Vector2(-o, content.anchoredPosition.y);
                yield return null;
            }
            content.anchoredPosition = new Vector2(-targetOffset, content.anchoredPosition.y);
            _avoidRoutine = null;
        }

        public void ClearSelection()
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = null;
        }

        // ─────────────────────────────────────── 详情面板
        /// <summary>关闭详情面板（供地图空白区点击调用）</summary>
        public void CloseDetail()
        {
            if (detailPanel != null) detailPanel.Hide();
        }

        // ─────────────────────────────────────── 滚动位置
        /// <summary>地图回到最左（不改动 content 的其它状态）</summary>
        public void ScrollToStart()
        {
            if (content == null) return;
            Canvas.ForceUpdateCanvases();
            // 归零即可让「左中对齐 + pivot(0,0.5)」的内容贴住视口左边缘
            content.anchoredPosition = Vector2.zero;
            if (scrollRect != null) scrollRect.StopMovement();
        }

        // ─────────────────────────────────────── 对照日志
        private void LogMapping()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[LevelMap] 关卡索引对照表（索引 = 排序后列表下标 = LevelSceneNames 下标）");
            sb.AppendLine("  索引 | 关卡名称 | sceneName | LevelSceneNames[i] | 解锁");
            for (int i = 0; i < _sorted.Count; i++)
            {
                var c = _sorted[i];
                string reg = (SceneLoader.LevelSceneNames != null && i < SceneLoader.LevelSceneNames.Length)
                    ? SceneLoader.LevelSceneNames[i] : "<越界>";
                bool match = reg == c.sceneName;
                sb.AppendLine(string.Format("  {0,4} | {1} | {2} | {3} | {4}{5}",
                    i, c.levelName, c.sceneName, reg,
                    ResolveUnlocked(i, c) ? "已解锁" : "锁定",
                    match ? "" : "  <<< 不一致!"));
            }
            sb.AppendLine("  共 " + _sorted.Count + " 关，LevelSceneNames.Length = " +
                          (SceneLoader.LevelSceneNames != null ? SceneLoader.LevelSceneNames.Length : 0));
            Debug.Log(sb.ToString());
        }
    }
}
