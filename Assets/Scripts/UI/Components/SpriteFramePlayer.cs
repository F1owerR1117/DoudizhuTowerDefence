using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 极简 UI 帧动画：按固定帧率循环切换 Image 的 Sprite。
/// 用于关卡选择界面的篝火等小循环动画，不依赖 Animator。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class SpriteFramePlayer : MonoBehaviour
{
    [Tooltip("循环播放的帧，按顺序切")]
    public Sprite[] frames;

    [Tooltip("每秒帧数")]
    public float framesPerSecond = 9f;

    [Tooltip("随机起始帧，避免多个实例同步跳动")]
    public bool randomStartFrame = true;

    private Image _image;
    private float _timer;
    private int _index;

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (frames == null || frames.Length == 0) return;

        if (randomStartFrame) _index = Random.Range(0, frames.Length);
        if (_image != null && frames[_index] != null) _image.sprite = frames[_index];
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2 || _image == null) return;
        if (framesPerSecond <= 0f) return;

        _timer += Time.deltaTime * framesPerSecond;
        if (_timer < 1f) return;

        int steps = Mathf.FloorToInt(_timer);
        _timer -= steps;
        _index = (_index + steps) % frames.Length;
        if (frames[_index] != null) _image.sprite = frames[_index];
    }
}
