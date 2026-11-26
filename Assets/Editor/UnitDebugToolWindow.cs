using System;
using System.Collections.Generic;
using System.Reflection;
using DoudizhuTower.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 兵种综合调试工具。
/// 菜单入口：Tools/技能可视化/综合调试工具
///
/// 整合功能：
/// - CardUnit 属性编辑
/// - UnitPassives 被动技能配置
/// - UnitAudio 音效配置和预览
/// - UnitVFX 特效配置和预览
/// - SimpleAnimator 动画配置和预览
/// - BossSkillSystem 技能配置和预览
/// - 一键测试所有效果
/// - 批量应用到同类兵种
/// </summary>
public class UnitDebugToolWindow : EditorWindow
{
    // ─── Prefab 选择 ───
    private GameObject _selectedPrefab;
    private SerializedObject _soUnit;
    private SerializedObject _soPassives;
    private SerializedObject _soAudio;
    private SerializedObject _soVFX;
    private SerializedObject _soAnimator;
    private SerializedObject _soSkillSystem;

    // ─── 运行时实例 ───
    private CardUnit _runtimeUnit;
    private UnitPassives _runtimePassives;
    private UnitAudio _runtimeAudio;
    private UnitVFX _runtimeVFX;
    private SimpleAnimator _runtimeAnimator;
    private BossSkillSystem _runtimeSkillSystem;

    // ─── UI 缓存 ───
    private Vector2 _scroll;
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInit;

    // ─── 选中的技能索引 ───
    private int _selectedSkillIndex = -1;

    // ─── Tab 页 ───
    private enum DebugTab { Passives, CardUnit, Audio, VFX, Animation, BossSkills, Batch }
    private DebugTab _currentTab = DebugTab.Passives;

    // ─── VFX 基准尺寸映射 ───
    private static readonly (string label, string field, float vfxBase)[] VfxMapping =
    {
        ("Splash", "splashRadius", 2f),
        ("Shockwave", "shockwaveRadius", 3f),
        ("KingAura", "kingRadius", 3f),
        ("DeathExplosion", "explosionRadius", 2f),
    };

    [MenuItem("Tools/技能可视化/综合调试工具")]
    public static void ShowWindow()
    {
        var w = GetWindow<UnitDebugToolWindow>("综合调试工具");
        w.minSize = new Vector2(900, 650);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    // ─── 选择同步 ───

    private void OnSelectionChanged()
    {
        _runtimeUnit = null;
        _runtimePassives = null;
        _runtimeAudio = null;
        _runtimeVFX = null;
        _runtimeAnimator = null;
        _runtimeSkillSystem = null;
        _soUnit = null;
        _soPassives = null;
        _soAudio = null;
        _soVFX = null;
        _soAnimator = null;
        _soSkillSystem = null;
        _selectedPrefab = null;
        _selectedSkillIndex = -1;

        var go = Selection.activeGameObject;
        if (go == null) return;

        if (Application.isPlaying)
        {
            _runtimeUnit = go.GetComponent<CardUnit>();
            _runtimePassives = go.GetComponent<UnitPassives>();
            _runtimeAudio = go.GetComponent<UnitAudio>();
            _runtimeVFX = go.GetComponent<UnitVFX>();
            _runtimeAnimator = go.GetComponentInChildren<SimpleAnimator>(true);
            _runtimeSkillSystem = go.GetComponent<BossSkillSystem>();
        }
        else
        {
            _selectedPrefab = go;

            var unit = go.GetComponent<CardUnit>();
            if (unit != null) _soUnit = new SerializedObject(unit);

            var passives = go.GetComponent<UnitPassives>();
            if (passives != null) _soPassives = new SerializedObject(passives);

            var audio = go.GetComponent<UnitAudio>();
            if (audio != null) _soAudio = new SerializedObject(audio);

            var vfx = go.GetComponent<UnitVFX>();
            if (vfx != null) _soVFX = new SerializedObject(vfx);

            var animator = go.GetComponentInChildren<SimpleAnimator>(true);
            if (animator != null) _soAnimator = new SerializedObject(animator);

            var skillSystem = go.GetComponent<BossSkillSystem>();
            if (skillSystem != null) _soSkillSystem = new SerializedObject(skillSystem);
        }

        Repaint();
    }

    // ─── GUI ───

    private void InitStyles()
    {
        if (_stylesInit) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        _buttonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 24 };
        _stylesInit = true;
    }

    private void OnGUI()
    {
        InitStyles();

        // 无选择时显示提示
        if (!HasTarget())
        {
            EditorGUILayout.Space(40);
            EditorGUILayout.LabelField("请选择一个兵种 Prefab 或运行时实例", _headerStyle);
            EditorGUILayout.LabelField("（需要至少包含 CardUnit 组件）", EditorStyles.miniLabel);
            return;
        }

        // 验证 SerializedObject 有效性
        ValidateSerializedObjects();

        // 更新 SerializedObject
        _soUnit?.Update();
        _soPassives?.Update();
        _soAudio?.Update();
        _soVFX?.Update();
        _soAnimator?.Update();
        _soSkillSystem?.Update();

        // Tab 页栏
        DrawTabBar();

        EditorGUILayout.Space(4);

        // 内容区域
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        try
        {
            switch (_currentTab)
            {
                case DebugTab.Passives: DrawPassivesTab(); break;
                case DebugTab.CardUnit: DrawCardUnitTab(); break;
                case DebugTab.Audio: DrawAudioTab(); break;
                case DebugTab.VFX: DrawVFXTab(); break;
                case DebugTab.Animation: DrawAnimationTab(); break;
                case DebugTab.BossSkills: DrawBossSkillsTab(); break;
                case DebugTab.Batch: DrawBatchTab(); break;
            }
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }

        // 应用修改
        ApplyModifiedProperties();
    }

    private bool HasTarget()
    {
        return _soUnit != null || _runtimeUnit != null;
    }

    private void ValidateSerializedObjects()
    {
        if (_soUnit != null && _soUnit.targetObject == null)
        { _soUnit = null; _selectedPrefab = null; }
        if (_soPassives != null && _soPassives.targetObject == null)
            _soPassives = null;
        if (_soAudio != null && _soAudio.targetObject == null)
            _soAudio = null;
        if (_soVFX != null && _soVFX.targetObject == null)
            _soVFX = null;
        if (_soAnimator != null && _soAnimator.targetObject == null)
            _soAnimator = null;
        if (_soSkillSystem != null && _soSkillSystem.targetObject == null)
            _soSkillSystem = null;
    }

    private void ApplyModifiedProperties()
    {
        bool changed = false;
        if (_soUnit != null && _soUnit.ApplyModifiedProperties()) changed = true;
        if (_soPassives != null && _soPassives.ApplyModifiedProperties()) changed = true;
        if (_soAudio != null && _soAudio.ApplyModifiedProperties()) changed = true;
        if (_soVFX != null && _soVFX.ApplyModifiedProperties()) changed = true;
        if (_soAnimator != null && _soAnimator.ApplyModifiedProperties()) changed = true;
        if (_soSkillSystem != null && _soSkillSystem.ApplyModifiedProperties()) changed = true;
        if (changed) Repaint();
    }

    // ─── Tab 页栏 ───

    private void DrawTabBar()
    {
        var go = _selectedPrefab ?? _runtimeUnit?.gameObject;
        EditorGUILayout.LabelField(go?.name ?? "Unknown", _headerStyle);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("被动技能")) _currentTab = DebugTab.Passives;
        if (GUILayout.Button("CardUnit")) _currentTab = DebugTab.CardUnit;
        if (GUILayout.Button("音效")) _currentTab = DebugTab.Audio;
        if (GUILayout.Button("特效")) _currentTab = DebugTab.VFX;
        if (GUILayout.Button("动画")) _currentTab = DebugTab.Animation;
        if (GUILayout.Button("BOSS技能")) _currentTab = DebugTab.BossSkills;
        if (GUILayout.Button("批量操作")) _currentTab = DebugTab.Batch;
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: 被动技能
    // ═══════════════════════════════════════════════════════════

    private void DrawPassivesTab()
    {
        if (_soPassives == null && _runtimePassives == null)
        {
            EditorGUILayout.HelpBox("未找到 UnitPassives 组件", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("─── 被动技能 ───", _subHeaderStyle);

        DrawPassiveToggleAndSliders("点杀 (Sniper)", "enableSniper",
            ("sniperRangeBonus", "搜索范围", 0f, 50f),
            ("sniperHpThreshold", "血量阈值", 0f, 1f));

        DrawPassiveToggleAndSliders("人海连击 (Swarm)", "enableSwarm",
            ("swarmRadius", "感知半径", 0.5f, 10f),
            ("swarmDamagePct", "每友军伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("冲锋一击 (Charge)", "enableCharge",
            ("chargeMultiplier", "伤害倍率", 1f, 10f),
            ("chargeCooldown", "冷却(秒)", 1f, 30f),
            ("chargeSpeedMultiplier", "移速倍率", 1f, 3f));

        DrawPassiveToggleAndSliders("君王光环 (KingAura)", "enableKingAura",
            ("kingInterval", "间隔(秒)", 1f, 20f),
            ("kingRadius", "影响半径", 0.5f, 15f),
            ("kingPushDistance", "震退距离", 0.1f, 5f));

        DrawPassiveToggleAndSliders("盾墙 (ShieldWall)", "enableShieldWall",
            ("shieldRange", "影响半径", 0.5f, 15f),
            ("shieldDamageReduction", "减伤%", 0f, 1f));

        DrawPassiveToggleAndSliders("嘲讽 (Taunt)", "enableTaunt");

        DrawPassiveToggleAndSliders("死亡爆炸 (DeathExplosion)", "enableDeathExplosion",
            ("explosionRadius", "爆炸半径", 0.5f, 10f),
            ("explosionDamagePct", "伤害%", 0f, 3f));

        DrawPassiveToggleAndSliders("护盾吸收 (ShieldAbsorb)", "enableShieldAbsorb",
            ("shieldAmount", "护盾值", 0f, 2000f));

        DrawPassiveToggleAndSliders("减速光环 (SlowAura)", "enableSlowAura",
            ("slowRadius", "光环半径", 0.5f, 15f),
            ("slowPercent", "减速%", 0f, 1f),
            ("slowDuration", "持续(秒)", 0.5f, 10f));

        DrawPassiveToggleAndSliders("攻击眩晕 (StunOnHit)", "enableStunOnHit",
            ("stunDuration", "眩晕(秒)", 0.1f, 5f));

        DrawPassiveToggleAndSliders("撕裂 (Tear)", "enableTear",
            ("tearDamagePerStack", "每层增伤%", 0f, 0.5f),
            ("tearMaxStacks", "最大层数", 1, 20),
            ("tearDuration", "持续(秒)", 1f, 15f));

        DrawPassiveToggleAndSliders("出场震波 (Shockwave)", "enableShockwave",
            ("shockwaveRadius", "震波半径", 0.5f, 10f),
            ("shockwaveDamagePct", "伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("死亡燃烧 (BurnOnDeath)", "enableBurnOnDeath",
            ("burnRadius", "火海半径", 0.5f, 10f),
            ("burnDamagePct", "每秒伤害%", 0f, 1f),
            ("burnDuration", "持续(秒)", 1f, 15f));

        DrawPassiveToggleAndSliders("群体攻击 (Splash)", "enableSplash",
            ("splashRadius", "群体半径", 0.5f, 10f),
            ("splashDamagePct", "群体伤害%", 0f, 2f));

        DrawPassiveToggleAndSliders("骑兵追远程 (CavalryChase)", "enableCavalryChase",
            ("cavalryChaseRangeThreshold", "远程阈值", 1f, 20f));

        DrawPassiveToggleAndSliders("召唤师 (Summoner)", "enableSummoner",
            ("summonInterval", "召唤间隔(秒)", 1f, 30f),
            ("maxSummons", "最大召唤数", 1, 10));

        DrawPassiveToggleAndSliders("快速连击 (BurstAttack)", "enableBurstAttack",
            ("burstHitCount", "连击次数", 2, 10),
            ("burstCooldown", "冷却(秒)", 1f, 10f));
    }

    private void DrawPassiveToggleAndSliders(string title, string toggleField, params object[] sliders)
    {
        EditorGUILayout.Space(4);

        bool enabled = false;
        if (_soPassives != null)
        {
            var sp = _soPassives.FindProperty(toggleField);
            if (sp != null)
            {
                sp.boolValue = EditorGUILayout.ToggleLeft(title, sp.boolValue);
                enabled = sp.boolValue;
            }
        }
        else if (_runtimePassives != null)
        {
            var fi = typeof(UnitPassives).GetField(toggleField);
            if (fi != null)
            {
                bool val = (bool)fi.GetValue(_runtimePassives);
                bool newVal = EditorGUILayout.ToggleLeft(title, val);
                if (newVal != val) fi.SetValue(_runtimePassives, newVal);
                enabled = newVal;
            }
        }

        if (!enabled) return;

        EditorGUI.indentLevel++;
        foreach (var raw in sliders)
        {
            var (field, label, min, max, isInt) = ParseSliderDef(raw);
            if (_soPassives != null)
            {
                var sp = _soPassives.FindProperty(field);
                if (sp == null) continue;
                if (isInt)
                    EditorGUILayout.IntSlider(sp, (int)min, (int)max, new GUIContent(label));
                else
                    EditorGUILayout.Slider(sp, min, max, new GUIContent(label));
            }
            else if (_runtimePassives != null)
            {
                var fi = typeof(UnitPassives).GetField(field);
                if (fi == null) continue;
                if (isInt || fi.FieldType == typeof(int))
                {
                    int val = (int)fi.GetValue(_runtimePassives);
                    int nv = EditorGUILayout.IntSlider(label, val, (int)min, (int)max);
                    if (nv != val) fi.SetValue(_runtimePassives, nv);
                }
                else
                {
                    float val = (float)fi.GetValue(_runtimePassives);
                    float nv = EditorGUILayout.Slider(label, val, min, max);
                    if (nv != val) fi.SetValue(_runtimePassives, nv);
                }
            }
        }
        EditorGUI.indentLevel--;
    }

    private static (string field, string label, float min, float max, bool isInt) ParseSliderDef(object raw)
    {
        if (raw is (string f, string l, float mn, float mx))
            return (f, l, mn, mx, false);
        if (raw is (string fi, string la, int mi, int ma))
            return (fi, la, mi, ma, true);
        return ("", "", 0, 0, false);
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: 动画
    // ═══════════════════════════════════════════════════════════

    private void DrawAnimationTab()
    {
        EditorGUILayout.LabelField("─── 动画配置 ───", _subHeaderStyle);

        if (_soAnimator == null && _runtimeAnimator == null)
        {
            EditorGUILayout.HelpBox("未找到 SimpleAnimator 组件", MessageType.Warning);
            return;
        }

        if (_soAnimator != null)
        {
            // 编辑模式：显示所有动画 Clip 配置
            EditorGUILayout.LabelField("基础动画", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("idleClip"), new GUIContent("待机动画"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("walkClip"), new GUIContent("行走动画"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("attackClip"), new GUIContent("攻击动画"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("死亡动画", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("deathClip"), new GUIContent("死亡动画"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Trigger 特效动画", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("chargeClip"), new GUIContent("冲锋"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("shockwaveClip"), new GUIContent("震波"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("splashClip"), new GUIContent("溅射"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("stunHitClip"), new GUIContent("眩晕命中"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("kingAuraClip"), new GUIContent("君王光环"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("deathExplosionClip"), new GUIContent("死亡爆炸"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("burnClip"), new GUIContent("燃烧"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("summonClip"), new GUIContent("召唤"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("BOSS 技能动画", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("dashClip"), new GUIContent("冲刺"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("bossSkill1Clip"), new GUIContent("BOSS 技能 1"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("bossSkill2Clip"), new GUIContent("BOSS 技能 2"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("bossSkill3Clip"), new GUIContent("BOSS 技能 3"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Bool 特效动画", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("tauntClip"), new GUIContent("嘲讽"));
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("shieldWallClip"), new GUIContent("盾墙"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("控制器", _subHeaderStyle);
            EditorGUILayout.PropertyField(_soAnimator.FindProperty("baseController"), new GUIContent("基础控制器"));

            // 播放测试
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("─── 播放测试 ───", _subHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放待机")) PlayAnimation("idleClip");
            if (GUILayout.Button("播放行走")) PlayAnimation("walkClip");
            if (GUILayout.Button("播放攻击")) PlayAnimation("attackClip");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放死亡")) PlayAnimation("deathClip");
            if (GUILayout.Button("播放冲锋")) PlayAnimation("chargeClip");
            if (GUILayout.Button("播放震波")) PlayAnimation("shockwaveClip");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放嘲讽")) PlayAnimation("tauntClip");
            if (GUILayout.Button("播放盾墙")) PlayAnimation("shieldWallClip");
            if (GUILayout.Button("播放冲刺")) PlayAnimation("dashClip");
            EditorGUILayout.EndHorizontal();
        }
        else if (_runtimeAnimator != null)
        {
            // 运行时模式：显示当前播放的动画状态
            var animator = _runtimeAnimator.Animator;
            if (animator != null)
            {
                EditorGUILayout.LabelField($"当前状态: {animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F2}");
                EditorGUILayout.LabelField($"播放速度: {animator.speed:F2}");

                // 控制按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("播放待机")) animator.SetInteger("State", 0);
                if (GUILayout.Button("播放行走")) animator.SetInteger("State", 1);
                if (GUILayout.Button("播放攻击")) animator.SetInteger("State", 2);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("触发死亡")) animator.SetTrigger("Death");
                if (GUILayout.Button("触发冲锋")) animator.SetBool("Charge", true);
                if (GUILayout.Button("停止冲锋")) animator.SetBool("Charge", false);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void PlayAnimation(string fieldName)
    {
        AnimationClip clip = null;

        if (_soAnimator != null)
        {
            var prop = _soAnimator.FindProperty(fieldName);
            if (prop != null)
                clip = prop.objectReferenceValue as AnimationClip;
        }

        if (clip != null)
        {
            // 在 Scene View 中预览动画
            if (_selectedPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(_selectedPrefab);
                instance.name = "[AnimPreview] " + _selectedPrefab.name;
                instance.hideFlags = HideFlags.HideAndDontSave;

                var animator = instance.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.Play(clip.name, 0, 0f);
                    Debug.Log($"[综合调试] 播放动画: {clip.name}");
                }

                // 3秒后自动销毁
                DestroyImmediate(instance, true);
            }
            else
            {
                UnityEditor.EditorUtility.DisplayDialog("播放动画", $"正在播放: {clip.name}", "确定");
                Debug.Log($"[综合调试] 播放动画预览: {clip.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[综合调试] 未配置动画: {fieldName}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: CardUnit
    // ═══════════════════════════════════════════════════════════

    private void DrawCardUnitTab()
    {
        EditorGUILayout.LabelField("─── CardUnit 属性 ───", _subHeaderStyle);

        if (_soUnit != null)
        {
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_hp"), new GUIContent("HP"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_atk"), new GUIContent("ATK"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_range"), new GUIContent("攻击范围"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_moveSpeed"), new GUIContent("移动速度"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_attackInterval"), new GUIContent("攻击间隔"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_hitCount"), new GUIContent("攻击次数"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_maxTargets"), new GUIContent("最大目标数"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_multiTargetRadius"), new GUIContent("多目标半径"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_detectionRange"), new GUIContent("索敌范围"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_regenPerSecond"), new GUIContent("每秒回血"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_isRanged"), new GUIContent("是否远程"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_isLandlord"), new GUIContent("是否地主"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_unitHeight"), new GUIContent("单位高度"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_canAttackHeight"), new GUIContent("可攻击高度"));
            EditorGUILayout.PropertyField(_soUnit.FindProperty("_canBlockHeight"), new GUIContent("可阻挡高度"));
        }
        else if (_runtimeUnit != null)
        {
            var s = _runtimeUnit.Stats;
            EditorGUILayout.LabelField($"HP: {s.HP:F0}  ATK: {s.ATK:F1}  Range: {s.Range:F1}");
            EditorGUILayout.LabelField($"Speed: {s.MoveSpeed:F1}  Interval: {s.AttackInterval:F2}s");
            EditorGUILayout.LabelField($"Height: {_runtimeUnit.UnitHeight}  Ranged: {_runtimeUnit.IsRanged}");
            EditorGUILayout.LabelField($"Invulnerable: {_runtimeUnit.Invulnerable}  Stun: {_runtimeUnit.StunTimer:F1}s");
        }

        // 预制体预览
        if (_selectedPrefab != null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("─── 预制体预览 ───", _subHeaderStyle);
            DrawPrefabPreview();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: 音效
    // ═══════════════════════════════════════════════════════════

    private void DrawAudioTab()
    {
        EditorGUILayout.LabelField("─── 音效配置 ───", _subHeaderStyle);

        if (_soAudio != null)
        {
            EditorGUILayout.PropertyField(_soAudio.FindProperty("attackMeleeClips"), new GUIContent("近战攻击音效"), true);
            EditorGUILayout.PropertyField(_soAudio.FindProperty("attackRangedClip"), new GUIContent("远程攻击音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("hitClip"), new GUIContent("受击音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("deathClip"), new GUIContent("死亡音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("chargeClip"), new GUIContent("冲锋音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("shockwaveClip"), new GUIContent("震波音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("splashClip"), new GUIContent("溅射音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("stunHitClip"), new GUIContent("眩晕音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("kingAuraClip"), new GUIContent("君王光环音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("deathExplosionClip"), new GUIContent("死亡爆炸音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("burnClip"), new GUIContent("燃烧音效"));
            EditorGUILayout.PropertyField(_soAudio.FindProperty("summonClip"), new GUIContent("召唤音效"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("─── 播放测试 ───", _subHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放攻击")) PlayAudio("attackMeleeClips");
            if (GUILayout.Button("播放受击")) PlayAudio("hitClip");
            if (GUILayout.Button("播放死亡")) PlayAudio("deathClip");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放冲锋")) PlayAudio("chargeClip");
            if (GUILayout.Button("播放震波")) PlayAudio("shockwaveClip");
            if (GUILayout.Button("播放溅射")) PlayAudio("splashClip");
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("未找到 UnitAudio 组件", MessageType.Warning);
        }
    }

    private void PlayAudio(string fieldName)
    {
        AudioClip clip = null;
        if (_soAudio != null)
        {
            var prop = _soAudio.FindProperty(fieldName);
            if (prop != null)
            {
                if (prop.isArray && prop.arraySize > 0)
                    clip = prop.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip;
                else
                    clip = prop.objectReferenceValue as AudioClip;
            }
        }

        if (clip != null)
        {
            UnityEditor.EditorUtility.DisplayDialog("播放音效", $"正在播放: {clip.name}", "确定");
            Debug.Log($"[综合调试] 播放音效: {clip.name}");
        }
        else
        {
            Debug.LogWarning($"[综合调试] 未配置音效: {fieldName}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: 特效
    // ═══════════════════════════════════════════════════════════

    private void DrawVFXTab()
    {
        EditorGUILayout.LabelField("─── 特效配置 ───", _subHeaderStyle);

        if (_soVFX != null)
        {
            EditorGUILayout.PropertyField(_soVFX.FindProperty("spawnVFX"), new GUIContent("生成特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("splashExplosionVFX"), new GUIContent("溅射爆炸"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("chargeVFX"), new GUIContent("冲锋特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("shockwaveVFX"), new GUIContent("震波特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("deathExplosionVFX"), new GUIContent("死亡爆炸"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("burnVFX"), new GUIContent("燃烧特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("summonVFX"), new GUIContent("召唤特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("shieldVFX"), new GUIContent("盾墙特效"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("tauntAuraVFX"), new GUIContent("嘲讽光环"));
            EditorGUILayout.PropertyField(_soVFX.FindProperty("tearVFX"), new GUIContent("撕裂特效"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("─── 播放测试 ───", _subHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放生成特效")) PlayVFX("spawnVFX");
            if (GUILayout.Button("播放溅射爆炸")) PlayVFX("splashExplosionVFX");
            if (GUILayout.Button("播放冲锋特效")) PlayVFX("chargeVFX");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放震波特效")) PlayVFX("shockwaveVFX");
            if (GUILayout.Button("播放死亡爆炸")) PlayVFX("deathExplosionVFX");
            if (GUILayout.Button("播放燃烧特效")) PlayVFX("burnVFX");
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("未找到 UnitVFX 组件", MessageType.Warning);
        }

        // VFX 缩放映射
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── VFX 缩放映射 ───", _subHeaderStyle);
        EditorGUILayout.LabelField("公式: VFX Scale = Radius / BaseSize", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("被动", EditorStyles.miniLabel, GUILayout.Width(100));
        GUILayout.Label("逻辑半径", EditorStyles.miniLabel, GUILayout.Width(70));
        GUILayout.Label("VFX Base", EditorStyles.miniLabel, GUILayout.Width(65));
        GUILayout.Label("VFX Scale", EditorStyles.miniLabel, GUILayout.Width(65));
        EditorGUILayout.EndHorizontal();

        foreach (var (label, field, vfxBase) in VfxMapping)
        {
            float logicRadius = GetFloatField(field);
            if (logicRadius <= 0f) continue;

            float vfxScale = logicRadius / vfxBase;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            GUILayout.Label($"{logicRadius:F2}", GUILayout.Width(70));
            GUILayout.Label($"{vfxBase:F0}", GUILayout.Width(65));
            GUILayout.Label($"{vfxScale:F2}x", GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
        }

        // 特效预览区域
        DrawVFXPreview();
    }

    // ─── 特效预览 ───

    private Texture2D _vfxPreviewTexture;
    private string _vfxPreviewName;
    private float _vfxPreviewTimer;
    private const float VFX_PREVIEW_DURATION = 3f;

    private void PlayVFX(string fieldName)
    {
        GameObject vfxPrefab = null;
        if (_soVFX != null)
        {
            var prop = _soVFX.FindProperty(fieldName);
            if (prop != null)
                vfxPrefab = prop.objectReferenceValue as GameObject;
        }

        if (vfxPrefab != null)
        {
            // 获取特效的缩略图作为预览
            _vfxPreviewTexture = AssetPreview.GetAssetPreview(vfxPrefab);
            if (_vfxPreviewTexture == null)
                _vfxPreviewTexture = AssetPreview.GetMiniThumbnail(vfxPrefab);
            _vfxPreviewName = vfxPrefab.name;
            _vfxPreviewTimer = VFX_PREVIEW_DURATION;

            // 同时在 Scene View 中生成特效
            var pos = Vector3.zero;
            if (_selectedPrefab != null)
                pos = _selectedPrefab.transform.position;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab);
            instance.transform.position = pos;
            instance.hideFlags = HideFlags.HideAndDontSave;

            // 聚焦 Scene View
            SceneView.lastActiveSceneView?.LookAt(pos, SceneView.lastActiveSceneView.rotation, 10f);

            Debug.Log($"[综合调试] 预览特效: {vfxPrefab.name}");

            // 5秒后自动销毁
            DestroyImmediate(instance, true);

            // 刷新窗口显示预览
            Repaint();
        }
        else
        {
            Debug.LogWarning($"[综合调试] 未配置特效预制体: {fieldName}");
        }
    }

    private void DrawVFXPreview()
    {
        if (_vfxPreviewTexture == null || _vfxPreviewTimer <= 0f) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("─── 特效预览 ───", _subHeaderStyle);

        // 显示预览图片
        var rect = GUILayoutUtility.GetRect(150, 150, GUILayout.ExpandWidth(false));
        rect.x = (position.width - 150) / 2;
        GUI.DrawTexture(rect, _vfxPreviewTexture, ScaleMode.ScaleToFit);

        // 显示特效名称
        EditorGUILayout.LabelField(_vfxPreviewName, EditorStyles.centeredGreyMiniLabel);

        // 显示剩余时间
        float remaining = _vfxPreviewTimer;
        EditorGUILayout.LabelField($"预览中... {remaining:F1}s", EditorStyles.miniLabel);

        // 更新计时器
        _vfxPreviewTimer -= Time.deltaTime;
        if (_vfxPreviewTimer <= 0f)
        {
            _vfxPreviewTexture = null;
            _vfxPreviewName = null;
        }

        // 持续刷新以更新计时器
        Repaint();
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: BOSS 技能
    // ═══════════════════════════════════════════════════════════

    private void DrawBossSkillsTab()
    {
        if (_soSkillSystem == null && _runtimeSkillSystem == null)
        {
            EditorGUILayout.HelpBox("未找到 BossSkillSystem 组件（非 BOSS 单位无需配置）", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("─── BOSS 技能 ───", _subHeaderStyle);

        if (_soSkillSystem != null)
        {
            var skillsProp = _soSkillSystem.FindProperty("skills");
            if (skillsProp == null) return;

            // 技能数量
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"技能数量: {skillsProp.arraySize}", GUILayout.Width(100));
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                skillsProp.InsertArrayElementAtIndex(skillsProp.arraySize);
                _selectedSkillIndex = skillsProp.arraySize - 1;
            }
            if (GUILayout.Button("-", GUILayout.Width(30)) && skillsProp.arraySize > 0)
            {
                skillsProp.DeleteArrayElementAtIndex(skillsProp.arraySize - 1);
                if (_selectedSkillIndex >= skillsProp.arraySize)
                    _selectedSkillIndex = skillsProp.arraySize - 1;
            }
            EditorGUILayout.EndHorizontal();

            // 技能列表
            for (int i = 0; i < skillsProp.arraySize; i++)
            {
                var skillProp = skillsProp.GetArrayElementAtIndex(i);
                var nameProp = skillProp.FindPropertyRelative("skillName");
                string skillName = nameProp?.stringValue ?? $"技能 {i}";

                bool isSelected = (_selectedSkillIndex == i);
                var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

                if (GUILayout.Button($"{i + 1}. {skillName}", style))
                    _selectedSkillIndex = i;
            }

            // 选中的技能配置
            if (_selectedSkillIndex >= 0 && _selectedSkillIndex < skillsProp.arraySize)
            {
                EditorGUILayout.Space(8);
                DrawBossSkillConfig(skillsProp.GetArrayElementAtIndex(_selectedSkillIndex));
            }
        }
    }

    private void DrawBossSkillConfig(SerializedProperty skillProp)
    {
        EditorGUILayout.LabelField("─── 技能配置 ───", _subHeaderStyle);

        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("skillName"), new GUIContent("技能名称"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("trigger"), new GUIContent("触发条件"));

        var triggerProp = skillProp.FindPropertyRelative("trigger");
        if (triggerProp != null)
        {
            if (triggerProp.enumValueIndex == 0)
                EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("hpThreshold"), new GUIContent("HP阈值"));
            else if (triggerProp.enumValueIndex == 1)
                EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("cooldown"), new GUIContent("冷却时间"));
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("castDuration"), new GUIContent("施法时间"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("invulnerable"), new GUIContent("施法不可选"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("clearCC"), new GUIContent("清除控制"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("animTrigger"), new GUIContent("动画触发名"));

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("effectType"), new GUIContent("效果类型"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("effectValue"), new GUIContent("效果数值"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("effectRadius"), new GUIContent("效果半径"));

        var effectTypeProp = skillProp.FindPropertyRelative("effectType");
        if (effectTypeProp != null && effectTypeProp.enumValueIndex == 5)
        {
            EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("dashDistance"), new GUIContent("冲刺距离"));
            EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("dashSpeed"), new GUIContent("冲刺速度"));
        }

        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("effectDelay"), new GUIContent("效果延迟"));

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("vfxPrefab"), new GUIContent("特效预制体"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("vfxDuration"), new GUIContent("特效持续时间"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("sfxClip"), new GUIContent("音效"));
        EditorGUILayout.PropertyField(skillProp.FindPropertyRelative("sfxVolume"), new GUIContent("音效音量"));

        // 测试按钮
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("播放音效"))
        {
            var sfxProp = skillProp.FindPropertyRelative("sfxClip");
            var clip = sfxProp?.objectReferenceValue as AudioClip;
            if (clip != null)
            {
            UnityEditor.EditorUtility.DisplayDialog("播放音效", $"正在播放: {clip.name}", "确定");
                Debug.Log($"[综合调试] 播放技能音效: {clip.name}");
            }
        }
        if (GUILayout.Button("生成特效"))
        {
            var vfxProp = skillProp.FindPropertyRelative("vfxPrefab");
            var vfxPrefab = vfxProp?.objectReferenceValue as GameObject;
            if (vfxPrefab != null)
            {
                var pos = _selectedPrefab?.transform.position ?? Vector3.zero;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab);
                instance.transform.position = pos;
                instance.hideFlags = HideFlags.HideAndDontSave;
                Debug.Log($"[综合调试] 生成技能特效: {vfxPrefab.name}");
                DestroyImmediate(instance, true);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════════════
    //  Tab: 批量操作
    // ═══════════════════════════════════════════════════════════

    private void DrawBatchTab()
    {
        EditorGUILayout.LabelField("─── 批量操作 ───", _subHeaderStyle);

        EditorGUILayout.HelpBox(
            "将当前 Prefab 的配置应用到同类兵种。\n" +
            "选择所有目标 Prefab，然后点击应用按钮。",
            MessageType.Info);

        EditorGUILayout.Space(8);

        if (_selectedPrefab == null)
        {
            EditorGUILayout.HelpBox("请先选择一个源 Prefab", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"源 Prefab: {_selectedPrefab.name}");

        // 一键测试
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── 一键测试 ───", _subHeaderStyle);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("测试所有音效"))
        {
            Debug.Log("[综合调试] 测试所有音效...");
            PlayAudio("attackMeleeClips");
            PlayAudio("hitClip");
            PlayAudio("deathClip");
        }
        if (GUILayout.Button("测试所有特效"))
        {
            Debug.Log("[综合调试] 测试所有特效...");
            PlayVFX("spawnVFX");
            PlayVFX("attackVFX");
            PlayVFX("deathVFX");
        }
        EditorGUILayout.EndHorizontal();

        // 批量应用
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("─── 批量应用 ───", _subHeaderStyle);

        if (GUILayout.Button("应用到所有选中的 Prefab"))
        {
            ApplyToSelectedPrefabs();
        }
    }

    private void ApplyToSelectedPrefabs()
    {
        if (_selectedPrefab == null) return;

        var selection = Selection.gameObjects;
        int applied = 0;

        foreach (var target in selection)
        {
            if (target == _selectedPrefab) continue;

            var targetPassives = target.GetComponent<UnitPassives>();
            var sourcePassives = _selectedPrefab.GetComponent<UnitPassives>();
            if (targetPassives == null || sourcePassives == null) continue;

            // 复制被动技能参数
            CopyPassiveParameters(sourcePassives, targetPassives);
            applied++;
            Debug.Log($"[综合调试] 已应用到: {target.name}");
        }

        Debug.Log($"[综合调试] 批量应用完成，共 {applied} 个 Prefab");
    }

    private void CopyPassiveParameters(UnitPassives source, UnitPassives target)
    {
        // 使用反射复制所有 public 字段
        var fields = typeof(UnitPassives).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(GameObject)) continue; // 跳过预制体引用
            if (field.FieldType == typeof(AudioClip)) continue; // 跳过音效引用
            object value = field.GetValue(source);
            field.SetValue(target, value);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  辅助方法
    // ═══════════════════════════════════════════════════════════

    private float GetFloatField(string fieldName)
    {
        if (_soPassives != null)
        {
            var sp = _soPassives.FindProperty(fieldName);
            return sp?.floatValue ?? 0f;
        }
        if (_runtimePassives != null)
        {
            var fi = typeof(UnitPassives).GetField(fieldName);
            return fi != null ? (float)fi.GetValue(_runtimePassives) : 0f;
        }
        return 0f;
    }

    private void DrawPrefabPreview()
    {
        if (_selectedPrefab == null) return;

        var spriteRenderer = _selectedPrefab.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            var sprite = spriteRenderer.sprite;
            var rect = GUILayoutUtility.GetRect(120, 120, GUILayout.ExpandWidth(false));
            rect.x = (position.width * 0.55f - 120) / 2;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
            EditorGUILayout.LabelField(sprite.name, EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            var rect = GUILayoutUtility.GetRect(120, 120, GUILayout.ExpandWidth(false));
            rect.x = (position.width * 0.55f - 120) / 2;
            EditorGUI.DrawPreviewTexture(rect, AssetPreview.GetAssetPreview(_selectedPrefab));
            EditorGUILayout.LabelField(_selectedPrefab.name, EditorStyles.centeredGreyMiniLabel);
        }
    }
}
