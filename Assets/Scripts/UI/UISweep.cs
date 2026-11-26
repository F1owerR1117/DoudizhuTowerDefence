using UnityEngine;

/// <summary>
/// 全屏扫光：把一张竖直柔光带在画面上缓慢下移，复刻预览里的 CRT 扫过效果。
/// 挂在扫光带的 RectTransform 上。
/// </summary>
public class UISweep : MonoBehaviour
{
    [Tooltip("一次完整扫过所需秒数")]
    public float cycle = 9f;

    [Tooltip("上下移动范围（像素）")]
    public float travel = 1300f;

    private RectTransform _rt;
    private float _baseX;
    private float _t;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (_rt != null) _baseX = _rt.anchoredPosition.x;
    }

    private void OnEnable()
    {
        _t = 0f;
    }

    private void Update()
    {
        if (_rt == null) return;
        _t += Time.unscaledDeltaTime;
        float p = Mathf.Repeat(_t / Mathf.Max(0.01f, cycle), 1f);
        float y = Mathf.Lerp(travel, -travel, p);
        _rt.anchoredPosition = new Vector2(_baseX, y);
    }
}
