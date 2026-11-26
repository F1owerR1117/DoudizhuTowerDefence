using DoudizhuTower.Core.Battle;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.UI.Floating
{
    /// <summary>飘字种类</summary>
    public enum FloatTextKind
    {
        /// <summary>伤害数字（原有逻辑）</summary>
        Damage = 0,
        /// <summary>治疗（绿色 +N）</summary>
        Heal = 1,
        /// <summary>处决（黄色「处决」）</summary>
        Execute = 2,
    }

    /// <summary>
    /// 伤害飘字组件。固定在 World Space Canvas 下，
    /// 向上浮动 + 渐隐后自动回池。
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageFloatText : MonoBehaviour
    {
        [Header("动画参数")]
        [Tooltip("飘字向上浮动的总高度（世界坐标单位）")]
        [SerializeField] private float floatHeight = 1.5f;

        [Tooltip("动画总时长（秒），从出现到完全消失")]
        [SerializeField] private float duration = 1f;

        [Tooltip("暴击阈值：伤害 ≥ 此值时变红加粗加大")]
        [SerializeField] private float critThreshold = 50f;

        [Header("颜色")]
        [Tooltip("治疗文字颜色")]
        [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.45f);

        [Tooltip("处决文字颜色")]
        [SerializeField] private Color executeColor = new Color(1f, 0.85f, 0.2f);

        private TextMeshPro _text;
        private float _elapsed;
        private Vector3 _startPos;
        private System.Action _onComplete;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            _startPos = transform.position;
        }

        /// <summary>
        /// 显示伤害数字（兼容旧调用）
        /// </summary>
        public void Show(float damage, Vector3 worldPos, DamageType type, System.Action onComplete)
        {
            Show(FloatTextKind.Damage, damage, worldPos, type, onComplete);
        }

        /// <summary>
        /// 显示飘字
        /// </summary>
        /// <param name="kind">飘字种类：伤害 / 治疗 / 处决</param>
        /// <param name="value">数值（治疗为治疗量，处决忽略）</param>
        /// <param name="worldPos">世界坐标</param>
        /// <param name="type">伤害类型（仅 Damage 使用）</param>
        /// <param name="onComplete">动画结束后回调（回池）</param>
        public void Show(FloatTextKind kind, float value, Vector3 worldPos, DamageType type, System.Action onComplete)
        {
            _elapsed = 0f;
            _startPos = worldPos;
            transform.position = worldPos;
            _onComplete = onComplete;

            bool emphasize = false;

            switch (kind)
            {
                case FloatTextKind.Heal:
                    _text.text = "+" + Mathf.RoundToInt(value).ToString();
                    _text.color = healColor;
                    break;

                case FloatTextKind.Execute:
                    _text.text = "处决";
                    _text.color = executeColor;
                    emphasize = true;   // 处决加粗加大，突出显示
                    break;

                default:
                    _text.text = Mathf.RoundToInt(value).ToString();
                    // ── 颜色规则 ──
                    // 真实伤害 → 橙色；特殊伤害 → 紫色；物理/炸弹/燃烧 → 暴击红色，否则白色
                    emphasize = value >= critThreshold;
                    _text.color = type switch
                    {
                        DamageType.True => new Color(1f, 0.5f, 0f),
                        DamageType.Special => new Color(0.7f, 0.3f, 1f),
                        _ => emphasize ? Color.red : Color.white
                    };
                    break;
            }

            _text.fontStyle = emphasize ? FontStyles.Bold : FontStyles.Normal;
            _text.fontSize = emphasize ? 120f : 100f;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / duration); // 0→1 归一化进度

            // ── 向上浮动 ──
            Vector3 pos = _startPos;
            pos.y += t * floatHeight;
            transform.position = pos;

            // ── 渐隐 ──
            // 前 20% 时间完全不透明，后 80% 渐变到透明
            float alpha = t < 0.2f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.2f) / 0.8f);
            _text.alpha = alpha;

            // ── 动画结束 → 隐藏并回调对象池回收 ──
            if (t >= 1f)
            {
                var cb = _onComplete;
                _onComplete = null;
                gameObject.SetActive(false);
                cb?.Invoke();
            }
        }
    }
}
