using UnityEngine;

namespace DoudizhuTower.UI.Theme
{
    /// <summary>
    /// 像素废土 UI 色板：与 PixelWasteland_Codex_Preview.html 的 CSS 变量一一对应。
    /// 换主题只换这个资产，不用碰任何 prefab。
    /// </summary>
    [CreateAssetMenu(menuName = "PixelWasteland/Codex Palette", fileName = "CodexPalette")]
    public class CodexPalette : ScriptableObject
    {
        [Header("钢灰阶梯 steel-0..5")]
        public Color steel0 = C("#0b0908");
        public Color steel1 = C("#191412");
        public Color steel2 = C("#2b2320");
        public Color steel3 = C("#3f342c");
        public Color steel4 = C("#574a3f");
        public Color steel5 = C("#726151");

        [Header("骨白阶梯")]
        public Color bone = C("#e9dcc0");
        public Color boneDim = C("#ab9b80");
        public Color boneDark = C("#776853");

        [Header("功能色")]
        public Color amber = C("#f0a050");
        public Color amberDim = C("#a56328");
        public Color toxic = C("#a8ee54");
        public Color toxicDim = C("#6f9c2b");
        public Color blood = C("#c04a38");
        public Color phosphor = C("#6adfa6");

        [Header("状态：常态")]
        public Color normalBg = C("#2b2320");
        public Color normalText = C("#e9dcc0");
        public Color normalBorder = C("#0b0908");

        [Header("状态：悬浮（升一阶 + 琥珀光晕）")]
        public Color hoverBg = C("#3f342c");
        public Color hoverText = Color.white;
        public Color hoverBorder = C("#a56328");

        [Header("状态：按下（下沉 3px + 变暗）")]
        public Color pressBg = C("#191412");
        public Color pressText = C("#e9dcc0");
        public Color pressBorder = C("#0b0908");
        public Vector2 pressOffset = new Vector2(3f, -3f);

        [Header("状态：选中（半透明毒绿）")]
        public Color selectedBg = new Color(0.659f, 0.933f, 0.329f, 0.20f);
        public Color selectedText = C("#a8ee54");
        public Color selectedBorder = C("#6f9c2b");

        [Header("交互参数")]
        [Tooltip("按下时缩放（不依赖 anchoredPosition，布局组下也安全）")]
        public float pressScale = 0.97f;
        [Tooltip("按下时替换的九宫格贴图（斜面明暗反转）；仅普通按钮生效")]
        public Sprite pressedSprite;
        [Tooltip("悬浮时是否加一圈外描边（用 _Outline 子物体实现）")]
        public bool useHoverOutline = true;

        private static Color C(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }
    }
}
