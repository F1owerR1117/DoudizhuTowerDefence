using System;
using DoudizhuTower.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Panels
{
    public class PauseMenu : MonoBehaviour
    {
        [Header("面板引用")]
        [SerializeField] private GameObject pausePanel;

        [Header("按钮")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsBackButton;

        public static bool IsPaused { get; private set; }
        public static bool IsGameOver { get; set; }

        /// <summary>设置联机模式：隐藏重启按钮</summary>
        private bool _isMultiplayer;

        public void SetMultiplayerMode(bool isMultiplayer)
        {
            _isMultiplayer = isMultiplayer;
            if (restartButton != null)
                restartButton.gameObject.SetActive(!isMultiplayer);
        }

        public event Action OnRestartRequested;
        public event Action OnQuitRequested;

        private CanvasGroup _pauseCanvasGroup;
        private CanvasGroup _settingsCanvasGroup;

        private void Awake()
        {
            _pauseCanvasGroup = EnsureCanvasGroup(pausePanel);
            _settingsCanvasGroup = EnsureCanvasGroup(settingsPanel);

            HidePanel(_pauseCanvasGroup);
            HidePanel(_settingsCanvasGroup);
        }

        private void OnDestroy()
        {
            if (IsPaused)
            {
                IsPaused = false;
                if (!_isMultiplayer)
                    Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
            if (volumeSlider != null)
            {
                volumeSlider.value = AudioListener.volume;
                volumeSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
            }
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
            }
        }

        private void Update()
        {
            if (IsGameOver) return;

            if (IsEscapePressed())
            {
                if (_settingsCanvasGroup != null && _settingsCanvasGroup.alpha > 0f)
                    CloseSettings();
                else
                    TogglePause();
            }
        }

        private static bool IsEscapePressed()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            IsPaused = true;
            // 联机模式下不暂停游戏逻辑（其他玩家仍在运行）
            if (!_isMultiplayer)
                Time.timeScale = 0f;
            ShowPanel(_pauseCanvasGroup);
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            HidePanel(_pauseCanvasGroup);
            HidePanel(_settingsCanvasGroup);
        }

        private void Restart()
        {
            GameSession.Reset();
            Resume();
            OnRestartRequested?.Invoke();
        }

        private void OpenSettings()
        {
            ShowPanel(_settingsCanvasGroup);
        }

        private void CloseSettings()
        {
            HidePanel(_settingsCanvasGroup);
        }

        private void OnFullscreenToggleChanged(bool isFullscreen)
        {
            if (isFullscreen)
            {
                var maxRes = Screen.currentResolution;
                Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }

        private void Quit()
        {
            GameSession.Reset();
            Resume();
            OnQuitRequested?.Invoke();
        }

        // ── 辅助方法 ──

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null) return null;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static void ShowPanel(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        private static void HidePanel(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }
}
