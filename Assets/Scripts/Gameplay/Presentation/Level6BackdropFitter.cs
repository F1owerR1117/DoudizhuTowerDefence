using UnityEngine;

/// <summary>
/// Level_6「废土高地」背景适配器。
/// 把一张 16:9 的背景精灵按相机实际宽高比等比放大到铺满屏幕，
/// 同时保证背景图里的「路面」那条线始终落在战斗线（默认 y = -90）上，
/// 这样不同分辨率下单位都踩在路面上。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class Level6BackdropFitter : MonoBehaviour
{
    [Tooltip("目标相机，留空则用 Camera.main")]
    public Camera targetCamera;

    [Tooltip("战斗线（单位行走）的世界 Y")]
    public float roadWorldY = -90f;

    [Tooltip("背景图中路面中心距图片底部的比例（0=底部，1=顶部）")]
    [Range(0f, 1f)]
    public float roadFractionFromBottom = 0.45f;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    public void Apply()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null || _sr.sprite == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        Vector2 size = _sr.sprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;

        float s = Mathf.Max(viewW / size.x, viewH / size.y);

        var t = transform;
        t.localScale = new Vector3(s, s, 1f);
        float centerY = roadWorldY + (0.5f - roadFractionFromBottom) * size.y * s;
        t.position = new Vector3(cam.transform.position.x, centerY, t.position.z);
    }
}
