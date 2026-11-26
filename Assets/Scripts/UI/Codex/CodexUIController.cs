using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Codex
{
    public class CodexUIController : MonoBehaviour
    {
        [Header("数据")]
        [SerializeField] private CodexDatabase[] categoryDatabases;

        [Header("顶部栏")]
        [SerializeField] private Button backButton;
        [SerializeField] private Transform categoryTabsParent;
        [SerializeField] private GameObject categoryTabPrefab;

        [Header("左侧面板")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject entryButtonPrefab;

        [Header("右侧面板")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI extraInfoText;
        [SerializeField] private GameObject extraInfoContainer;
        [SerializeField] private TextMeshProUGUI unlockStatusText;
        [SerializeField] private Transform relatedEntriesParent;
        [SerializeField] private GameObject relatedEntryPrefab;

        [Header("进度显示")]
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressFill;

        [Header("页签样式")]
        [SerializeField] private Sprite tabIdleSprite;
        [SerializeField] private Sprite tabActiveSprite;

        [Header("档案头信息")]
        [SerializeField] private TextMeshProUGUI categoryChipText;
        [SerializeField] private TextMeshProUGUI entryIdText;
        [SerializeField] private TextMeshProUGUI subText;

        [Header("底部栏")]
        [SerializeField] private TextMeshProUGUI footerCountText;
        [SerializeField] private TextMeshProUGUI footerSourceText;

        [Header("关键词")]
        [SerializeField] private Transform keywordParent;
        [SerializeField] private Sprite keywordChipSprite;

        private CodexCategory _currentCategory = CodexCategory.CardValue;
        private string _searchQuery = "";
        private List<GameObject> _spawnedEntries = new();
        private List<GameObject> _spawnedRelatedEntries = new();
        private readonly List<Image> _tabImages = new();
        private CodexEntry _selectedEntry;

        private readonly string[] _categoryNames = { "卡牌数值", "牌型", "Boss", "建筑", "被动", "规则" };
        private readonly string[] _categoryCodes = { "CV", "CT", "BS", "BD", "PS", "RL" };

        private void Start()
        {
            InitializeDatabases();
            CreateCategoryTabs();
            ClearDetail();
            RefreshList();

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (searchInput != null)
                searchInput.onValueChanged.AddListener(OnSearchChanged);
        }

        private void InitializeDatabases()
        {
            if (categoryDatabases == null) return;
            foreach (var db in categoryDatabases)
            {
                if (db != null) db.Initialize();
            }
        }

        private CodexDatabase GetCurrentDatabase()
        {
            if (categoryDatabases == null || categoryDatabases.Length == 0) return null;
            int index = (int)_currentCategory;
            return index < categoryDatabases.Length ? categoryDatabases[index] : null;
        }

        private void CreateCategoryTabs()
        {
            if (categoryTabsParent == null || categoryTabPrefab == null) return;

            _tabImages.Clear();
            var categories = (CodexCategory[])System.Enum.GetValues(typeof(CodexCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                var tab = Instantiate(categoryTabPrefab, categoryTabsParent);
                var button = tab.GetComponent<Button>();
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();
                var image = tab.GetComponent<Image>();

                if (text != null) text.text = _categoryNames[i];
                var allTexts = tab.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in allTexts)
                {
                    if (t != null && t.gameObject.name == "Code") t.text = _categoryCodes[i];
                }
                _tabImages.Add(image);
                if (button != null)
                {
                    int categoryIndex = i;
                    button.onClick.AddListener(() => OnCategoryChanged(categories[categoryIndex]));
                }
            }
            RefreshTabVisuals();
        }

        /// <summary>高亮当前分类页签（否则玩家看不出选中了哪个分类）</summary>
        private void RefreshTabVisuals()
        {
            if (tabIdleSprite == null || tabActiveSprite == null) return;
            int current = (int)_currentCategory;
            for (int i = 0; i < _tabImages.Count; i++)
            {
                if (_tabImages[i] == null) continue;
                _tabImages[i].sprite = (i == current) ? tabActiveSprite : tabIdleSprite;
            }
        }

        private void OnCategoryChanged(CodexCategory category)
        {
            _currentCategory = category;
            RefreshTabVisuals();
            ClearDetail();
            RefreshList();
            UpdateProgress();
        }

        /// <summary>切换分类时清空右侧详情，避免出现「左侧暂无记录、右侧还是上个分类内容」</summary>
        private void ClearDetail()
        {
            _selectedEntry = null;
            if (titleText != null) titleText.text = "选择档案";
            if (descriptionText != null) descriptionText.text = "从左侧列表选择一条记录，查看它的详细说明与关联档案。";
            if (extraInfoText != null) extraInfoText.text = "—";
            if (extraInfoContainer != null) extraInfoContainer.SetActive(true);
            if (unlockStatusText != null) unlockStatusText.text = string.Empty;
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (categoryChipText != null) categoryChipText.text = _categoryNames[(int)_currentCategory];
            if (entryIdText != null) entryIdText.text = "—";
            if (subText != null) subText.text = string.Empty;
            if (footerSourceText != null) footerSourceText.text = string.Empty;
            if (keywordParent != null)
                for (int i = keywordParent.childCount - 1; i >= 0; i--)
                    Destroy(keywordParent.GetChild(i).gameObject);
        }

        private void OnSearchChanged(string query)
        {
            _searchQuery = query;
            RefreshList();
        }

        private void RefreshList()
        {
            // 清空旧条目
            if (contentParent != null)
            {
                for (int i = contentParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(contentParent.GetChild(i).gameObject);
                }
            }
            _spawnedEntries.Clear();

            var db = GetCurrentDatabase();
            if (db == null)
            {
                SetFooter(0, 0);
                ShowEmptyState(false);
                return;
            }

            var entries = db.Search(_searchQuery);

            if (entries == null || entries.Count == 0)
            {
                SetFooter(0, db.Entries.Count);
                ShowEmptyState(!string.IsNullOrWhiteSpace(_searchQuery));
                return;
            }

            foreach (var entry in entries)
            {
                if (entryButtonPrefab == null || contentParent == null) continue;

                var buttonObj = Instantiate(entryButtonPrefab, contentParent);
                var button = buttonObj.GetComponent<Button>();

                // 设置文本
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = entry.DisplayName;

                var idT = buttonObj.transform.Find("Id");
                if (idT != null)
                {
                    var idTmp = idT.GetComponent<TextMeshProUGUI>();
                    if (idTmp != null) idTmp.text = string.IsNullOrEmpty(entry.Id) ? "" : "#" + entry.Id.ToUpper();
                }

                // 设置图标
                var icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && entry.Icon != null) icon.sprite = entry.Icon;

                // 设置解锁状态
                var unlockIcon = buttonObj.transform.Find("UnlockIcon")?.GetComponent<Image>();
                if (unlockIcon != null)
                {
                    bool isUnlocked = SaveSystem.IsCodexEntryUnlocked(entry.Id);
                    unlockIcon.color = isUnlocked ? Color.green : Color.gray;
                    unlockIcon.gameObject.SetActive(true);
                }

                // 设置点击事件
                var entryRef = entry;
                if (button != null)
                    button.onClick.AddListener(() => OnEntryClicked(entryRef));

                _spawnedEntries.Add(buttonObj);
            }

            SetFooter(entries.Count, db.Entries.Count);
        }

        /// <summary>空分类 / 搜索无结果时的占位提示</summary>
        private void ShowEmptyState(bool hasQuery)
        {
            if (contentParent == null) return;

            var go = new GameObject("EmptyState", typeof(RectTransform));
            go.transform.SetParent(contentParent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (titleText != null && titleText.font != null) tmp.font = titleText.font;
            tmp.text = hasQuery ? "没有匹配的档案" : "暂无记录";
            tmp.fontSize = 20f;
            tmp.color = new Color(0.47f, 0.41f, 0.33f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 96f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 96f;

            _spawnedEntries.Add(go);
        }

        /// <summary>关键词 chips（运行时生成，无需额外 Prefab）</summary>
        private void BuildKeywords(string[] kws)
        {
            if (keywordParent == null) return;
            for (int i = keywordParent.childCount - 1; i >= 0; i--)
                Destroy(keywordParent.GetChild(i).gameObject);
            if (kws == null) return;

            var font = titleText != null ? titleText.font : null;
            foreach (string k in kws)
            {
                var go = new GameObject("Kw", typeof(RectTransform));
                go.transform.SetParent(keywordParent, false);

                var img = go.AddComponent<Image>();
                img.sprite = keywordChipSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.62f, 0.72f, 0.62f, 1f);
                img.raycastTarget = false;

                var le = go.AddComponent<LayoutElement>();
                le.preferredHeight = 30f;
                le.preferredWidth = 24f + k.Length * 18f;

                var tmp = new GameObject("T", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                tmp.transform.SetParent(go.transform, false);
                if (font != null) tmp.font = font;
                tmp.text = k;
                tmp.fontSize = 18f;
                tmp.color = new Color(0.42f, 0.87f, 0.65f, 1f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                var trt = (RectTransform)tmp.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f, 0f);
                trt.offsetMax = new Vector2(-8f, 0f);
            }
        }

        private void SetFooter(int shown, int total)
        {
            if (footerCountText != null)
                footerCountText.text = string.Format("共 {0} 条 / 全部 {1} 条", shown, total);
        }

        private void OnEntryClicked(CodexEntry entry)
        {
            _selectedEntry = entry;
            UpdateDetailPanel(entry);
            UpdateRelatedEntries(entry);

            // 解锁条目
            if (entry != null)
                SaveSystem.UnlockCodexEntry(entry.Id);

            // 刷新列表以更新解锁状态
            RefreshList();
            UpdateProgress();
        }

        private void UpdateDetailPanel(CodexEntry entry)
        {
            if (entry == null) return;

            if (categoryChipText != null) categoryChipText.text = _categoryNames[(int)entry.Category];
            if (entryIdText != null)
                entryIdText.text = string.IsNullOrEmpty(entry.Id) ? "—" : entry.Id.ToUpper();
            if (subText != null)
                subText.text = string.IsNullOrEmpty(entry.Id) ? "" : entry.Id.ToUpper().Replace('-', ' ');
            if (footerSourceText != null)
                footerSourceText.text = "SOURCE · CodexDatabase / " + entry.Category;
            BuildKeywords(entry.Keywords);

            if (titleText != null) titleText.text = entry.DisplayName;
            if (iconImage != null)
            {
                iconImage.sprite = entry.Icon;
                iconImage.gameObject.SetActive(entry.Icon != null);
            }
            if (descriptionText != null) descriptionText.text = entry.Description;

            if (extraInfoText != null && extraInfoContainer != null)
            {
                bool hasExtra = !string.IsNullOrEmpty(entry.ExtraInfo);
                extraInfoContainer.SetActive(hasExtra);
                if (hasExtra) extraInfoText.text = entry.ExtraInfo;
            }

            // 显示解锁状态
            if (unlockStatusText != null)
            {
                bool isUnlocked = SaveSystem.IsCodexEntryUnlocked(entry.Id);
                unlockStatusText.text = isUnlocked ? "已解锁" : "未解锁";
                unlockStatusText.color = isUnlocked ? Color.green : Color.gray;
            }
        }

        private void UpdateRelatedEntries(CodexEntry entry)
        {
            // 清空旧的相关条目
            if (relatedEntriesParent != null)
            {
                for (int i = relatedEntriesParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(relatedEntriesParent.GetChild(i).gameObject);
                }
            }
            _spawnedRelatedEntries.Clear();

            if (entry == null || entry.RelatedEntries == null || entry.RelatedEntries.Length == 0)
                return;

            // 查找相关条目
            var db = GetCurrentDatabase();
            if (db == null) return;

            foreach (var relatedId in entry.RelatedEntries)
            {
                var relatedEntry = db.GetById(relatedId);
                if (relatedEntry == null) continue;

                if (relatedEntryPrefab == null || relatedEntriesParent == null) continue;

                var buttonObj = Instantiate(relatedEntryPrefab, relatedEntriesParent);
                var button = buttonObj.GetComponent<Button>();
                var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null) text.text = relatedEntry.DisplayName;

                var entryRef = relatedEntry;
                if (button != null)
                    button.onClick.AddListener(() => OnEntryClicked(entryRef));

                _spawnedRelatedEntries.Add(buttonObj);
            }
        }

        private void UpdateProgress()
        {
            if (progressText == null) return;

            var db = GetCurrentDatabase();
            if (db == null)
            {
                progressText.text = "";
                return;
            }

            int total = db.Entries.Count;
            int unlocked = 0;
            foreach (var entry in db.Entries)
            {
                if (entry != null && SaveSystem.IsCodexEntryUnlocked(entry.Id))
                    unlocked++;
            }

            progressText.text = $"解锁进度: {unlocked}/{total}";

            if (progressFill != null)
                progressFill.fillAmount = total > 0 ? (float)unlocked / total : 0f;
        }

        private void OnBackClicked()
        {
            gameObject.SetActive(false);
            SceneLoader.LoadMainMenu();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshList();
            UpdateProgress();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
