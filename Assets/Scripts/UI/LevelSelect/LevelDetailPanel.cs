using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 右侧滑出的关卡详情面板。
    /// 只做「显示/隐藏 + 按 config 填内容 + 进入/关闭」，绝不触碰地图滚动位置。
    /// 数据全部来自 LevelConfig，不写死任何关卡信息。
    /// </summary>
    public class LevelDetailPanel : MonoBehaviour
    {
        [Header("根节点")]
        [SerializeField] private GameObject root;
        [Tooltip("全屏遮罩：吃掉点击与拖拽，防止穿透到地图")]
        [SerializeField] private Image blocker;
        [Tooltip("实际面板（从右侧滑入/滑出）")]
        [SerializeField] private RectTransform panelRect;

        [Header("内容")]
        [SerializeField] private Image thumbnail;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI indexText;

        [Header("按钮")]
        [SerializeField] private Button enterButton;
        [SerializeField] private TextMeshProUGUI enterButtonText;
        [SerializeField] private Button closeButton;

        [Header("动效")]
        [Tooltip("面板显示时相对右锚点的位置，默认 (0,0) 即贴右")]
        [SerializeField] private Vector2 shownAnchoredPosition = Vector2.zero;
        [SerializeField] private float slideDuration = 0.16f;
        [Tooltip("隐藏时面板相对显示位置的水平位移（正值=向右移出）")]
        [SerializeField] private float slideDistance = 640f;

        private LevelConfig _current;
        private int _currentIndex = -1;
        private Vector2 _shownPos;
        private bool _initialized;
        private bool _closing;
        private Coroutine _anim;

        public LevelConfig CurrentConfig { get { return _current; } }
        public int CurrentIndex { get { return _currentIndex; } }
        public bool IsOpen { get { return root != null && root.activeSelf && !_closing; } }

        private void Awake() { Init(); }

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;

            if (root == null) root = gameObject;
            // 显示位必须显式给出：若从当前位置推断，首次 Init 发生在 ForceClose（已在隐藏位）时会记错
            _shownPos = shownAnchoredPosition;

            if (enterButton != null) enterButton.onClick.AddListener(OnEnterClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            // 非模态：不再使用全屏遮罩拦截输入（blocker 字段仅作兼容保留，可为空）
            if (blocker != null) blocker.raycastTarget = false;

            if (panelRect != null) panelRect.anchoredPosition = _shownPos + new Vector2(slideDistance, 0f);
            root.SetActive(false);
        }

        /// <summary>显示指定关卡；index 为排序后列表下标</summary>
        public void Show(LevelConfig config, int index)
        {
            Init();
            if (config == null) return;

            _current = config;
            _currentIndex = index;
            _closing = false;

            Bind(config, index);

            // 已经打开：只更新内容，不重播完整的开启动画
            if (root.activeSelf && !_closing)
            {
                if (panelRect != null) panelRect.anchoredPosition = _shownPos;
                return;
            }

            root.SetActive(true);
            if (panelRect != null)
            {
                if (_anim != null) StopCoroutine(_anim);
                _anim = StartCoroutine(Slide(panelRect.anchoredPosition, _shownPos, true));
            }
        }

        public void Hide()
        {
            Init();
            if (root == null || !root.activeSelf || _closing) return;
            _closing = true;

            if (panelRect == null)
            {
                root.SetActive(false);
                _closing = false;
                return;
            }

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Slide(panelRect.anchoredPosition, _shownPos + new Vector2(slideDistance, 0f), false));
        }

        // ─────────────────────────────────────── 内容绑定（全部来自 LevelConfig）
        private void Bind(LevelConfig config, int index)
        {
            if (nameText != null) nameText.text = config.levelName;

            if (difficultyText != null)
            {
                int d = Mathf.Clamp(config.difficulty, 0, 5);
                string s = "";
                for (int i = 0; i < d; i++) s += "★";
                difficultyText.text = "难度　" + (s.Length > 0 ? s : "—");
            }

            if (descriptionText != null)
                descriptionText.text = string.IsNullOrEmpty(config.description) ? "（暂无简介）" : config.description;

            if (thumbnail != null)
            {
                thumbnail.sprite = config.thumbnail;
                thumbnail.enabled = config.thumbnail != null;
            }

            // 项目没有通关记录系统：如实显示，不伪造
            if (statusText != null) statusText.text = "通关记录：暂无记录";

            if (indexText != null)
                indexText.text = string.Format("编号 {0:00}　·　{1}", index + 1, config.sceneName);

            if (enterButton != null) enterButton.interactable = config.isUnlocked;
            if (enterButtonText != null)
                enterButtonText.text = config.isUnlocked ? "进入关卡" : "未解锁";
        }

        // ─────────────────────────────────────── 动效
        private System.Collections.IEnumerator Slide(Vector2 from, Vector2 to, bool opening)
        {
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / slideDuration);
                k = 1f - (1f - k) * (1f - k);      // ease out
                panelRect.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            panelRect.anchoredPosition = to;
            _anim = null;

            if (!opening)
            {
                root.SetActive(false);
                _current = null;
                _currentIndex = -1;
                _closing = false;
            }
        }

        /// <summary>立即关闭（不播动画），供建图/重进场景时保证默认隐藏</summary>
        public void ForceClose()
        {
            Init();
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            if (panelRect != null) panelRect.anchoredPosition = _shownPos + new Vector2(slideDistance, 0f);
            _current = null;
            _currentIndex = -1;
            _closing = false;
            if (root != null) root.SetActive(false);
        }

        /// <summary>
        /// 面板在 viewport 本地坐标中遮挡的宽度。
        /// 用「贴右锚点 + 面板宽度」解析计算，与当前滑动/显隐状态无关，也不受 Canvas 缩放影响。
        /// </summary>
        public float GetCoveredWidthIn(RectTransform viewport)
        {
            if (panelRect == null || viewport == null) return 0f;
            var parent = panelRect.parent as RectTransform;
            if (parent == null) return panelRect.rect.width;

            float pScale = panelRect.lossyScale.x;
            float vScale = viewport.lossyScale.x;
            float parentRightWorld = parent.position.x + parent.rect.xMax * parent.lossyScale.x;
            float panelLeftWorld = parentRightWorld - panelRect.rect.width * pScale;
            float viewportRightWorld = viewport.position.x + viewport.rect.xMax * vScale;

            return Mathf.Max(0f, (viewportRightWorld - panelLeftWorld) / vScale);
        }

        /// <summary>沿用项目现有跳转逻辑：索引 + 场景名</summary>
        public void OnEnterClicked()
        {
            if (_current == null || _currentIndex < 0) return;
            if (!_current.isUnlocked) return;

            SceneLoader.SetCurrentLevel(_currentIndex);
            Debug.Log(string.Format("[LevelSelect] 进入关卡: {0}（索引 {1}，场景 {2}）",
                _current.levelName, _currentIndex, _current.sceneName));
            SceneLoader.LoadScene(_current.sceneName);
        }
    }
}
