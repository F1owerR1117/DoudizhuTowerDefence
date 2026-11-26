using System;
using System.Collections.Generic;
using System.Reflection;
using DoudizhuTower.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 兵种预览编辑器：不进入 Play 模式即可预览兵种动画与特效，并编辑其数值/组件。
/// 菜单：Tools/PixelWasteland/9 - 兵种预览编辑器
///
/// 核心做法：
/// - PreviewRenderUtility 提供独立的预览场景与相机，不污染当前场景
/// - 编辑期 Animator 不会自动 tick，这里用 Animator.Update 手动推进
/// - 编辑期没有 VFXManager/AudioManager，特效改为直接实例化并手动 Simulate
/// </summary>
public class UnitPreviewWindow : EditorWindow
{
    // ── 预览渲染 ──
    private PreviewRenderUtility _prv;
    private GameObject _root;

    // ── 目标预制体 ──
    [SerializeField] private GameObject _prefab;

    // ── 动画 ──
    private Animator _anim;
    private bool _playing = true;
    private float _speed = 1f;
    private float _lastTick;

    // ── 特效 ──
    private UnitVFX _vfx;
    private UnitAudio _audio;
    private SerializedObject _vfxSO;
    private SerializedObject _audioSO;
    private readonly List<ActiveVfx> _activeVfx = new List<ActiveVfx>();
    private GameObject _tempVfxPrefab;
    private bool _audioPreview = true;

    private class ActiveVfx
    {
        public GameObject go;
        public float expireAt;
    }

    // ── 相机 ──
    private Vector2 _camCenter;
    private float _camSize = 4f;

    // ── Inspector（逐组件渲染预制体资产，含子物体上的组件）──
    private readonly List<Editor> _compEditors = new List<Editor>();
    private readonly List<string> _compTitles = new List<string>();
    private readonly List<bool> _compFoldouts = new List<bool>();
    private Vector2 _inspScroll;
    private Vector2 _ctrlScroll;
    private bool _needsPreviewReload;
    private float _reloadAt;
    private bool _hasOverrides;

    private static readonly Color BgColor = new Color(0.16f, 0.17f, 0.19f, 1f);

    private static readonly string[] VfxFields =
    {
        "bleedVFX", "igniteVFX", "impairVFX", "heavyStrikeVFX",
        "overheatVFX", "slowAuraVFX", "swarmAuraVFX", "leaderAuraVFX",
        "burstVFX", "executeVFX", "pounceVFX", "sniperMarkVFX",
        "splashExplosionVFX", "healVFX", "chargeVFX", "shockwaveVFX",
        "stunHitVFX", "kingAuraVFX", "deathExplosionVFX", "burnVFX",
        "summonVFX", "spawnVFX", "shieldVFX", "tauntAuraVFX", "tearVFX",
    };

    private static readonly Dictionary<string, string> VfxLabels = new Dictionary<string, string>
    {
        { "bleedVFX", "流血" }, { "igniteVFX", "燃烧" }, { "impairVFX", "损伤" },
        { "heavyStrikeVFX", "重击" }, { "overheatVFX", "过热" }, { "slowAuraVFX", "减速光环" },
        { "swarmAuraVFX", "人海光环" }, { "leaderAuraVFX", "领袖光环" }, { "burstVFX", "连击" }, { "executeVFX", "处决" },
        { "pounceVFX", "飞扑" }, { "sniperMarkVFX", "点杀" }, { "splashExplosionVFX", "群体攻击" },
        { "healVFX", "治疗" }, { "chargeVFX", "冲锋" }, { "shockwaveVFX", "震波" },
        { "stunHitVFX", "眩晕" }, { "kingAuraVFX", "君王" }, { "deathExplosionVFX", "死爆" },
        { "burnVFX", "火海" }, { "summonVFX", "召唤" }, { "spawnVFX", "出场" },
        { "shieldVFX", "护盾" }, { "tauntAuraVFX", "嘲讽" }, { "tearVFX", "撕裂" },
    };

    // ══════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════

    [MenuItem("Tools/PixelWasteland/9 - 兵种预览编辑器", false, 9)]
    public static void Open()
    {
        var win = GetWindow<UnitPreviewWindow>("兵种预览");
        win.minSize = new Vector2(760f, 460f);
        win.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorTick;
        _lastTick = (float)EditorApplication.timeSinceStartup;
        if (_prefab != null) LoadPrefab(_prefab);
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorTick;
        ClearPreview();
        DisposePreviewUtility();
    }

    private void EnsurePreviewUtility()
    {
        if (_prv != null) return;
        _prv = new PreviewRenderUtility();
        _prv.camera.orthographic = true;
        _prv.camera.orthographicSize = _camSize;
        _prv.camera.clearFlags = CameraClearFlags.SolidColor;
        _prv.camera.backgroundColor = BgColor;
        _prv.camera.nearClipPlane = 0.1f;
        _prv.camera.farClipPlane = 200f;

        foreach (var l in _prv.lights)
        {
            if (l == null) continue;
            l.intensity = 1.15f;
            l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    private void DisposePreviewUtility()
    {
        if (_prv != null)
        {
            try { _prv.Cleanup(); } catch { }
            _prv = null;
        }
    }

    private void ClearPreview()
    {
        DestroyInstanceEditor();
        if (_root != null)
        {
            DestroyImmediate(_root);
            _root = null;
        }
        ClearActiveVfx();
        _anim = null;
        _vfx = null;
        _vfxSO = null;
        _audio = null;
        _audioSO = null;
    }

    private void ClearActiveVfx()
    {
        foreach (var v in _activeVfx)
            if (v.go != null) DestroyImmediate(v.go);
        _activeVfx.Clear();
    }

    private void DestroyInstanceEditor()
    {
        foreach (var e in _compEditors)
            if (e != null) DestroyImmediate(e);
        _compEditors.Clear();
        _compTitles.Clear();
        _compFoldouts.Clear();
    }

    /// <summary>
    /// 给预制体上的关键组件（含子物体）各建一个 Editor。
    /// GameObjectInspector 只显示根组件，音效/动画组件挂在子物体上，必须单独渲染。
    /// </summary>
    private void RebuildComponentEditors()
    {
        if (_prefab == null) return;

        var comps = _prefab.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (c == null) continue;
            string n = c.GetType().Name;
            bool key = n == "CardUnit" || n == "UnitPassives" || n == "UnitVFX" || n == "UnitAudio"
                       || n == "SimpleAnimator" || n == "Animator" || n == "BoxCollider2D";
            if (!key) continue;

            _compEditors.Add(Editor.CreateEditor(c));
            _compTitles.Add(n + (c.gameObject == _prefab ? "" : "  @ " + c.gameObject.name));
            _compFoldouts.Add(n == "CardUnit" || n == "UnitPassives");
        }
    }

    // ══════════════════════════════════════════════
    //  加载预制体
    // ══════════════════════════════════════════════

    private void LoadPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        EnsurePreviewUtility();
        ClearPreview();
        ClearActiveVfx();
        _prefab = prefab;

        // 直接实例化到 PreviewRenderUtility 的预览场景（不会弄脏当前场景，且保留预制体关联）
        _root = _prv.InstantiatePrefabInScene(prefab);
        if (_root == null)
        {
            Debug.LogWarning("[UnitPreview] 实例化失败：" + prefab.name);
            return;
        }
        _root.transform.position = Vector3.zero;

        SetupAnimatorForPreview();

        _vfx = _root.GetComponentInChildren<UnitVFX>(true);
        _audio = _root.GetComponentInChildren<UnitAudio>(true);
        if (_vfx != null) _vfxSO = new SerializedObject(_vfx);
        if (_audio != null) _audioSO = new SerializedObject(_audio);

        // Inspector 直接针对「预制体资产」，预览场景里的实例不保证可编辑
        DestroyInstanceEditor();
        RebuildComponentEditors();
        _hasOverrides = false;
        _needsPreviewReload = false;
        FrameCamera();
        ApplyState(0);
        Repaint();
    }

    /// <summary>
    /// 复刻 SimpleAnimator 的运行时逻辑：把 baseController 的剪辑替换为组件上配置的剪辑。
    /// 编辑期 Awake 不执行，不做这一步 Animator 就没有控制器、动画完全不动。
    /// </summary>
    private void SetupAnimatorForPreview()
    {
        _anim = _root.GetComponentInChildren<Animator>(true);
        if (_anim == null) return;

        var sa = _root.GetComponentInChildren<SimpleAnimator>(true);
        var baseCtrl = sa != null ? sa.baseController : null;
        if (baseCtrl == null)
            baseCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Prefabs/Army/ArmyPrefabs/UnitBaseController.controller");
        if (baseCtrl == null) return;

        RuntimeAnimatorController final = baseCtrl;
        if (sa != null)
        {
            var map = BuildClipMap(sa);
            var overrideCtrl = new AnimatorOverrideController(baseCtrl);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                var orig = overrides[i].Key;
                if (orig == null) continue;
                string nameLower = orig.name.ToLowerInvariant();
                foreach (var kvp in map)
                {
                    if (nameLower.Contains(kvp.Key) && kvp.Value != null)
                    {
                        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, kvp.Value);
                        break;
                    }
                }
            }
            overrideCtrl.ApplyOverrides(overrides);
            final = overrideCtrl;
        }

        _anim.runtimeAnimatorController = final;
        _anim.enabled = true;
        _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _anim.updateMode = AnimatorUpdateMode.Normal;
        _anim.Rebind();
        _anim.Update(0f);
    }

    /// <summary>与 SimpleAnimator 中一致的关键字 → 剪辑映射</summary>
    private static Dictionary<string, AnimationClip> BuildClipMap(SimpleAnimator sa)
    {
        return new Dictionary<string, AnimationClip>
        {
            { "idle", sa.idleClip },
            { "walk", sa.walkClip },
            { "attack", sa.attackClip },
            { "death", sa.deathClip },
            { "charge", sa.chargeClip },
            { "shockwave", sa.shockwaveClip },
            { "splash", sa.splashClip },
            { "stunhit", sa.stunHitClip },
            { "kingaura", sa.kingAuraClip },
            { "deathexplosion", sa.deathExplosionClip },
            { "burn", sa.burnClip },
            { "summon", sa.summonClip },
            { "pounce", sa.pounceClip },
            { "dash", sa.dashClip },
            { "bossskill1", sa.bossSkill1Clip },
            { "bossskill2", sa.bossSkill2Clip },
            { "bossskill3", sa.bossSkill3Clip },
            { "taunt", sa.tauntClip },
            { "shieldwall", sa.shieldWallClip },
        };
    }

    private void FrameCamera()
    {
        if (_root == null) return;

        Bounds b = new Bounds(_root.transform.position, Vector3.one);
        bool has = false;
        foreach (var r in _root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        _camCenter = has ? (Vector2)b.center : (Vector2)_root.transform.position;
        float ext = has ? Mathf.Max(b.extents.x, b.extents.y) : 2f;
        _camSize = Mathf.Max(ext * 1.6f, 1.5f);
        Repaint();
    }

    // ══════════════════════════════════════════════
    //  每帧驱动（动画与粒子）
    // ══════════════════════════════════════════════

    private void OnEditorTick()
    {
        if (_root == null) return;

        float now = (float)EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp(now - _lastTick, 0f, 0.1f);
        _lastTick = now;

        if (_playing && _anim != null && dt > 0f)
            _anim.Update(dt * Mathf.Max(0.01f, _speed));

        for (int i = _activeVfx.Count - 1; i >= 0; i--)
        {
            var v = _activeVfx[i];
            if (v.go == null) { _activeVfx.RemoveAt(i); continue; }

            foreach (var ps in v.go.GetComponentsInChildren<ParticleSystem>(true))
                if (ps != null) ps.Simulate(dt, true, true);

            if (now >= v.expireAt)
            {
                DestroyImmediate(v.go);
                _activeVfx.RemoveAt(i);
            }
        }

        // 只在动画播放或特效存活时重绘，避免空转占用
        if ((_playing && _anim != null) || _activeVfx.Count > 0)
            Repaint();

        // 编辑后自动刷新预览实例（防抖）
        if (_needsPreviewReload && now >= _reloadAt && _prefab != null)
            ReloadPreviewInstance();
    }

    private void SetupCamera()
    {
        var cam = _prv.camera;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(0.2f, _camSize);
        cam.transform.position = new Vector3(_camCenter.x, _camCenter.y, -10f);
        cam.transform.rotation = Quaternion.identity;
    }

    // ══════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════

    private void OnGUI()
    {
        DrawToolbar();

        if (_prefab == null || _root == null)
        {
            EditorGUILayout.HelpBox("把兵种预制体拖到上方槽位（或点「用当前选中」）开始预览。", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.58f)))
            {
                DrawViewport();
                DrawAnimationControls();
                DrawPassiveControls();
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.40f)))
            {
                DrawInspectorPanel();
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var picked = (GameObject)EditorGUILayout.ObjectField(
                _prefab, typeof(GameObject), false, GUILayout.Width(210f));
            if (EditorGUI.EndChangeCheck() && picked != null) LoadPrefab(picked);

            if (GUILayout.Button("用当前选中", EditorStyles.toolbarButton, GUILayout.Width(78f)))
            {
                var sel = Selection.activeGameObject;
                if (sel != null)
                {
                    var asset = PrefabUtility.GetCorrespondingObjectFromSource(sel);
                    LoadPrefab(asset != null ? asset : sel);
                }
            }

            GUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(_root == null))
            {
                if (GUILayout.Button("重置视角", EditorStyles.toolbarButton, GUILayout.Width(66f))) FrameCamera();
                if (GUILayout.Button("刷新预览", EditorStyles.toolbarButton, GUILayout.Width(66f)))
                {
                    AssetDatabase.SaveAssets();
                    ReloadPreviewInstance();
                }
                if (GUILayout.Button("重新加载", EditorStyles.toolbarButton, GUILayout.Width(66f))) Reload();
                if (GUILayout.Button("在 Inspector 打开", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    if (_prefab != null)
                    {
                        Selection.activeObject = _prefab;
                        EditorGUIUtility.PingObject(_prefab);
                    }
                }
            }

            GUILayout.FlexibleSpace();
            _audioPreview = GUILayout.Toggle(_audioPreview, "音效", EditorStyles.toolbarButton, GUILayout.Width(44f));
            if (_needsPreviewReload) GUILayout.Label("● 待刷新预览", EditorStyles.miniLabel);
        }
    }

    private void DrawViewport()
    {
        float w = position.width * 0.58f - 10f;
        float h = Mathf.Max(240f, position.height - 268f);
        Rect rect = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint && _prv != null)
        {
            SetupCamera();
            _prv.BeginPreview(rect, GUIStyle.none);
            _prv.camera.Render();
            var tex = _prv.EndPreview();
            if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        if (rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.ScrollWheel)
            {
                _camSize = Mathf.Clamp(_camSize + Event.current.delta.y * _camSize * 0.03f, 0.3f, 100f);
                Repaint();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag)
            {
                float unitsPerPixel = (_camSize * 2f) / Mathf.Max(rect.height, 1f);
                _camCenter -= Event.current.delta * unitsPerPixel;
                Repaint();
                Event.current.Use();
            }
        }

        GUI.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 18f),
            "滚轮缩放 · 拖拽平移", EditorStyles.miniLabel);
    }

    private void DrawAnimationControls()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button(_playing ? "■ 暂停" : "▶ 播放", GUILayout.Width(70f))) _playing = !_playing;
            if (GUILayout.Button("⏭ 单帧", GUILayout.Width(62f)))
            {
                if (_anim != null) { _anim.Update(1f / 60f); Repaint(); }
            }
            GUILayout.Label("速度", GUILayout.Width(30f));
            _speed = GUILayout.HorizontalSlider(_speed, 0.1f, 2f, GUILayout.Width(86f));
            GUILayout.Label(_speed.ToString("0.00"), GUILayout.Width(36f));
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("状态", GUILayout.Width(30f));
            if (GUILayout.Button("待机", GUILayout.Width(50f))) ApplyState(0);
            if (GUILayout.Button("移动", GUILayout.Width(50f))) ApplyState(1);
            if (GUILayout.Button("攻击", GUILayout.Width(50f))) ApplyState(2);
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("触发", GUILayout.Width(30f));
            if (GUILayout.Button("冲锋", GUILayout.Width(46f))) TriggerAnim("Charge");
            if (GUILayout.Button("震波", GUILayout.Width(46f))) TriggerAnim("Shockwave");
            if (GUILayout.Button("群体", GUILayout.Width(46f))) TriggerAnim("Splash");
            if (GUILayout.Button("眩晕", GUILayout.Width(46f))) TriggerAnim("StunHit");
            if (GUILayout.Button("君王", GUILayout.Width(46f))) TriggerAnim("KingAura");
            if (GUILayout.Button("死爆", GUILayout.Width(46f))) TriggerAnim("DeathExplosion");
            if (GUILayout.Button("燃烧", GUILayout.Width(46f))) TriggerAnim("Burn");
            if (GUILayout.Button("召唤", GUILayout.Width(46f))) TriggerAnim("Summon");
            if (GUILayout.Button("飞扑", GUILayout.Width(46f))) TriggerAnim("Pounce");
            if (GUILayout.Button("嘲讽", GUILayout.Width(46f))) TriggerAnim("Taunt");
            if (GUILayout.Button("盾墙", GUILayout.Width(46f))) TriggerAnim("ShieldWall");
            if (GUILayout.Button("死亡", GUILayout.Width(46f))) TriggerAnim("Death");
        }
    }

    private void ApplyState(int state)
    {
        if (_anim == null) return;
        _anim.SetInteger("State", state);
        _anim.Update(0.01f);
        Repaint();
    }

    private void TriggerAnim(string name)
    {
        if (_anim == null) return;

        bool handled = false;
        foreach (var p in _anim.parameters)
        {
            if (!string.Equals(p.name, name, StringComparison.Ordinal)) continue;

            if (p.type == AnimatorControllerParameterType.Bool)
            {
                _anim.SetBool(name, !_anim.GetBool(name));   // 再点一次关闭
                handled = true;
            }
            else if (p.type == AnimatorControllerParameterType.Trigger)
            {
                _anim.SetTrigger(name);
                handled = true;
            }
            break;
        }

        if (!handled)
        {
            ShowNotification(new GUIContent("控制器中没有参数：" + name));
            return;
        }

        _anim.Update(0.02f);
        Repaint();
    }

    // ══════════════════════════════════════════════
    //  特效
    // ══════════════════════════════════════════════

    private void DrawPassiveControls()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("特效预览（点击在兵种身上播放）", EditorStyles.boldLabel);
            _ctrlScroll = EditorGUILayout.BeginScrollView(_ctrlScroll, GUILayout.Height(76f));

            int perRow = 6;
            int i = 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var field in VfxFields)
                {
                    if (i > 0 && i % perRow == 0)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    string label = VfxLabels.TryGetValue(field, out var l) ? l : field;
                    if (GUILayout.Button(label, GUILayout.Width(56f), GUILayout.Height(20f))) PlayVfx(field);
                    i++;
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("临时特效", GUILayout.Width(56f));
                _tempVfxPrefab = (GameObject)EditorGUILayout.ObjectField(_tempVfxPrefab, typeof(GameObject), false);
                using (new EditorGUI.DisabledScope(_tempVfxPrefab == null))
                {
                    if (GUILayout.Button("播放", GUILayout.Width(50f))) SpawnVfxInstance(_tempVfxPrefab, 1f, 1f);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("☠ 死亡演示", GUILayout.Width(90f))) PlayDeathDemo();
            }
        }
    }

    private bool EnsureVfxSO()
    {
        if (_vfx == null) return false;
        if (_vfxSO == null || _vfxSO.targetObject == null) _vfxSO = new SerializedObject(_vfx);
        return true;
    }

    private void PlayVfx(string fieldName)
    {
        if (!EnsureVfxSO()) return;

        var prop = _vfxSO.FindProperty(fieldName);
        if (prop == null || prop.objectReferenceValue == null)
        {
            ShowNotification(new GUIContent("该特效未配置"));
            return;
        }

        float duration = 1f;
        var durProp = _vfxSO.FindProperty(fieldName.Replace("VFX", "Duration"));
        if (durProp != null) duration = Mathf.Max(0.1f, durProp.floatValue);

        SpawnVfxInstance(prop.objectReferenceValue as GameObject, duration, GetVfxScale(fieldName));
        PlayAudioFor(fieldName);
    }

    /// <summary>
    /// 与运行时 UnitVFX 一致的缩放规则：范围类特效按「半径 / 2」缩放，
    /// 否则预览出来的大小会与实机不符。
    /// </summary>
    private float GetVfxScale(string fieldName)
    {
        var p = _root != null ? _root.GetComponentInChildren<UnitPassives>(true) : null;

        switch (fieldName)
        {
            case "splashExplosionVFX":
                return (p != null ? p.splashRadius : 2f) / 2f;
            case "shockwaveVFX":
                return (p != null ? p.shockwaveRadius : 2f) / 2f;
            case "deathExplosionVFX":
                return (p != null ? p.explosionRadius : 2f) / 2f;
            case "burnVFX":
                return (p != null ? p.burnRadius : 2f) / 2f;
            case "kingAuraVFX":
                return (p != null ? p.kingRadius : 3f) / 2f;
            case "tauntAuraVFX":
                return (p != null ? p.tauntRadius : 3f) / 2f;
            case "slowAuraVFX":
                return (p != null ? p.slowRadius : 3f) / 2f;
            case "swarmAuraVFX":
                return (p != null ? p.swarmRadius : 2f) / 2f;
            case "leaderAuraVFX":
                return (p != null ? p.leaderRadius : 4f) / 2f;
            case "heavyStrikeVFX":
                return Mathf.Max((p != null ? p.heavyStrikePushDistance : 1.5f) / 2f, 0.5f);
            default:
                return 1f;
        }
    }

    private void SpawnVfxInstance(GameObject prefab, float duration, float scale = 1f)
    {
        if (prefab == null || _root == null) return;
        EnsurePreviewUtility();

        var go = _prv.InstantiatePrefabInScene(prefab);
        if (go == null) return;

        go.transform.position = GetUnitCenter();
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
            ps.Simulate(0.02f, true, true);
        }

        _activeVfx.Add(new ActiveVfx
        {
            go = go,
            expireAt = (float)EditorApplication.timeSinceStartup + Mathf.Max(0.1f, duration)
        });
        Repaint();
    }

    /// <summary>死亡演示：死亡动画 + 死亡爆炸 + 火海 + 对应音效</summary>
    private void PlayDeathDemo()
    {
        if (_root == null) return;

        TriggerAnim("Death");
        PlayVfx("deathExplosionVFX");
        PlayVfx("burnVFX");

        PlayAudioField("deathClip");
        PlayAudioField("deathExplosionClip");
        PlayAudioField("burnClip");

        ShowNotification(new GUIContent("播放死亡效果"));
    }

    /// <summary>直接播放 UnitAudio 上某个 AudioClip 字段</summary>
    private void PlayAudioField(string fieldName)
    {
        if (!_audioPreview || _audio == null) return;
        if (_audioSO == null || _audioSO.targetObject == null) _audioSO = new SerializedObject(_audio);

        var prop = _audioSO.FindProperty(fieldName);
        var clip = prop != null ? prop.objectReferenceValue as AudioClip : null;
        if (clip != null) PlayPreviewClip(clip);
    }

    private Vector3 GetUnitCenter()    {
        if (_root == null) return Vector3.zero;
        var cu = _root.GetComponentInChildren<CardUnit>(true);
        return cu != null ? (Vector3)cu.PreviewCenter : _root.transform.position;
    }

    private void PlayAudioFor(string vfxField)
    {
        if (!_audioPreview) return;
        if (_audio == null) return;
        if (_audioSO == null || _audioSO.targetObject == null) _audioSO = new SerializedObject(_audio);

        string audioField = vfxField.Replace("VFX", "Clip");
        var map = new Dictionary<string, string>
        {
            { "splashExplosionVFX", "splashClip" },
            { "deathExplosionVFX", "deathExplosionClip" },
            { "kingAuraVFX", "kingAuraClip" },
            { "burnVFX", "burnClip" },
            { "shieldVFX", "shieldWallClip" },
            { "tauntAuraVFX", "tauntClip" },
            { "spawnVFX", "spawnSound" },
        };
        if (map.TryGetValue(vfxField, out var mapped)) audioField = mapped;

        var prop = _audioSO.FindProperty(audioField);
        var clip = prop != null ? prop.objectReferenceValue as AudioClip : null;
        if (clip != null) PlayPreviewClip(clip);
    }

    /// <summary>编辑期试听：反射 UnityEditor.AudioUtil.PlayPreviewClip</summary>
    private static void PlayPreviewClip(AudioClip clip)
    {
        if (clip == null) return;
        try
        {
            var t = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
            if (t == null) return;
            var m = t.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            m?.Invoke(null, new object[] { clip, 0, false });
        }
        catch { /* 试听失败不影响预览 */ }
    }

    // ══════════════════════════════════════════════
    //  Inspector / 应用修改
    // ══════════════════════════════════════════════

    private void DrawInspectorPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("组件与数值（直接编辑预制体资产）", EditorStyles.boldLabel);
            _inspScroll = EditorGUILayout.BeginScrollView(_inspScroll);

            if (_compEditors.Count == 0 && _prefab != null) RebuildComponentEditors();

            bool changed = false;
            for (int i = 0; i < _compEditors.Count; i++)
            {
                var ed = _compEditors[i];
                if (ed == null || ed.target == null) continue;

                _compFoldouts[i] = EditorGUILayout.Foldout(_compFoldouts[i], _compTitles[i], true);
                if (!_compFoldouts[i]) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    try { ed.OnInspectorGUI(); }
                    catch (Exception) { /* 域重载瞬间可能残留已销毁的 SerializedObject */ }
                    if (EditorGUI.EndChangeCheck()) changed = true;
                }
            }

            if (changed)
            {
                _needsPreviewReload = true;
                _reloadAt = (float)EditorApplication.timeSinceStartup + 0.5f;   // 防抖，避免拖拽数值时反复重建
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndScrollView();
        }

        if (_needsPreviewReload)
            EditorGUILayout.HelpBox("已修改预制体资产；预览实例会在稍后自动刷新。", MessageType.Info);
    }

    private void ApplyToPrefab()
    {
        AssetDatabase.SaveAssets();
        ReloadPreviewInstance();
        ShowNotification(new GUIContent("已保存并刷新预览"));
    }

    private void Reload()
    {
        if (_prefab == null) return;
        AssetDatabase.SaveAssets();
        ReloadPreviewInstance();
        ShowNotification(new GUIContent("已还原"));
    }

    /// <summary>只重建预览实例，保留 Inspector（它指向预制体资产，不受影响）</summary>
    private void ReloadPreviewInstance()
    {
        if (_prefab == null) return;
        EnsurePreviewUtility();

        ClearActiveVfx();
        if (_root != null)
        {
            DestroyImmediate(_root);
            _root = null;
        }

        _root = _prv.InstantiatePrefabInScene(_prefab);
        if (_root == null) return;

        _root.transform.position = Vector3.zero;
        SetupAnimatorForPreview();

        _vfx = _root.GetComponentInChildren<UnitVFX>(true);
        _audio = _root.GetComponentInChildren<UnitAudio>(true);
        _vfxSO = _vfx != null ? new SerializedObject(_vfx) : null;
        _audioSO = _audio != null ? new SerializedObject(_audio) : null;

        FrameCamera();
        ApplyState(0);
        _needsPreviewReload = false;
        Repaint();
    }
}
