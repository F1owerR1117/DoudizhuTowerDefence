using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DoudizhuTower.Config;

/// <summary>
/// 像素废土：Codex 内容补齐（新增条目 + 关键词 + 关联档案 + 描述修正）。
/// 数据来源：UnitPassives.cs 的被动开关、Boss 预制体实际挂载的被动、CardType 枚举。
/// 菜单 Tools/PixelWasteland/7
/// </summary>
public static class CodexContentBuilder
{
    private const string Root = "Assets/Scripts/UI/Codex/";
    private const string IconDir = "Assets/Art/PixelWasteland/Icons/";

    private class Def
    {
        public string id;
        public string name;
        public CodexCategory cat;
        public string desc;
        public string extra;
        public string[] kw;
        public string[] rel;
    }

    // ── 新增条目 ─────────────────────────────────────────────────
    private static readonly Def[] NewEntries =
    {
        // 牌型：枚举里 Straight6Plus 没有对应条目
        new Def { id = "cardtype-straight6", name = "六连以上", cat = CodexCategory.CardType,
            desc = "连续点数达到六张以上时单独归类的顺子变体。张数越多，一次铺开的单位越多，凑齐的代价也越高。",
            extra = "权重 6+　出兵 = 张数　需 6 张以上连续点数",
            kw = new[] { "六连", "长顺", "顺子" }, rel = new[] { "cardtype-straight", "cardtype-consecutive-pair" } },

        // BOSS：恶魔（FlyKill 预置体未完成，不建条目）
        new Def { id = "boss-demon", name = "恶魔", cat = CodexCategory.Boss,
            desc = "盘踞在高塔后段的恶魔型 BOSS。光环减速拖住推进，撕裂逐层放大伤害，溅射再把它扩到周围——靠近它的部队会被持续削弱。",
            extra = "被动：减速光环 / 撕裂 / 溅射攻击　建议：拉远距离、优先爆破",
            kw = new[] { "BOSS", "恶魔", "减速", "撕裂", "溅射" }, rel = new[] { "passive-slow-aura", "passive-tear" } },

        // 规则
        new Def { id = "rule-play-limit", name = "出牌上限", cat = CodexCategory.Rule,
            desc = "单次出牌的张数上限，决定一次能铺开多大规模的战线。王炸会占满整个上限，换来一次觉醒英雄的召唤。",
            extra = "上限按牌型判定　王炸占满上限",
            kw = new[] { "上限", "出牌", "王炸" }, rel = new[] { "cardtype-kingbomb", "rule-economy" } },
        new Def { id = "rule-economy", name = "金币经济", cat = CodexCategory.Rule,
            desc = "部署单位与升级建筑都要花金币。经济一旦断档，手里再好的牌也打不出去，所以前期铺经济往往比多打一手更划算。",
            extra = "金币来源：战斗产出　用途：部署 / 升级",
            kw = new[] { "金币", "经济", "产出" }, rel = new[] { "rule-play-limit" } },
        new Def { id = "rule-route", name = "路线", cat = CodexCategory.Rule,
            desc = "单位沿预定路线推进。建筑被摧毁或路线被改变时，单位需要重新寻路，这段时间往往就是防守方唯一的窗口。",
            extra = "路线改变会触发重新寻路",
            kw = new[] { "路线", "寻路", "推进" }, rel = new[] { "rule-economy" } },

        // ── 被动：来自 UnitPassives.cs 的 17 个开关 ──
        new Def { id = "passive-sniper", name = "点杀", cat = CodexCategory.Passive,
            desc = "可以额外攻击到自身攻击范围外、生命值低于一定比例的敌人。",
            extra = "· 范围内的敌人始终优先选择血量最低的" + "\n" + "· 越界只能锁定残血目标" + "\n" + "· 没有合适目标时回退普通索敌",
            kw = new[] { "点杀", "残血", "锁定" }, rel = new[] { "passive-cavalry", "passive-splash" } },
        new Def { id = "passive-swarm", name = "人海连击", cat = CodexCategory.Passive,
            desc = "周围友军越多，自身攻击力越高。",
            extra = "· 以自身为圆心统计友军数量" + "\n" + "· 攻击力随友军数量线性提升，没有上限" + "\n" + "· 不计自身与建筑，友军进出范围实时生效",
            kw = new[] { "人海", "连击", "集群" }, rel = new[] { "passive-charge", "passive-shield-wall" } },
        new Def { id = "passive-charge", name = "冲锋一击", cat = CodexCategory.Passive,
            desc = "出场时以及停止攻击一段时间后进入冲锋，下一次攻击造成额外伤害，命中即退出。",
            extra = "· 冲锋期间移动速度提升" + "\n" + "· 冲锋期间可单独设置自身高度、可被阻挡高度、可攻击目标高度" + "\n" + "· 退出冲锋时高度设置自动恢复",
            kw = new[] { "冲锋", "爆发", "移速" }, rel = new[] { "passive-swarm", "passive-stun" } },
        new Def { id = "passive-king-aura", name = "君王光环", cat = CodexCategory.Passive,
            desc = "周期性地把周围敌人沿远离自身的方向推开。",
            extra = "· 击退是平滑位移，不会瞬间瞬移" + "\n" + "· 可选择只影响自身能攻击到的高度",
            kw = new[] { "光环", "击退", "控制" }, rel = new[] { "passive-taunt", "passive-slow-aura" } },
        new Def { id = "passive-shield-wall", name = "盾墙线", cat = CodexCategory.Passive,
            desc = "为周围友军提供伤害减免，多个盾墙的减伤可叠加。",
            extra = "· 减伤按乘算叠加，没有上限" + "\n" + "· 对真实伤害无效" + "\n" + "· 不保护建筑，也不保护自己" + "\n" + "· 判定距离按碰撞体边缘计算",
            kw = new[] { "盾墙", "减伤", "阵型" }, rel = new[] { "passive-shield-absorb", "passive-swarm" } },
        new Def { id = "passive-taunt", name = "嘲讽", cat = CodexCategory.Passive,
            desc = "使周围能攻击到自己的敌人优先以自己为目标。",
            extra = "· 需要敌人同时在光环范围内、且自己在敌人攻击范围内" + "\n" + "· 多个嘲讽源同时存在时取最近的一个" + "\n" + "· 不会打断进行中的攻击，本次攻击结束后才切换",
            kw = new[] { "嘲讽", "拉仇恨", "保护" }, rel = new[] { "passive-shield-wall", "passive-king-aura" } },
        new Def { id = "passive-death-explosion", name = "死亡爆炸", cat = CodexCategory.Passive,
            desc = "死亡时对周围敌人造成范围伤害。",
            extra = "· 只伤害自己能攻击到的高度" + "\n" + "· 敌方建筑也会被波及",
            kw = new[] { "死亡", "爆炸", "范围" }, rel = new[] { "passive-burn", "passive-splash" } },
        new Def { id = "passive-shield-absorb", name = "护盾吸收", cat = CodexCategory.Passive,
            desc = "出场时获得一层可吸收伤害的护盾。",
            extra = "· 真实伤害会直接穿透护盾" + "\n" + "· 若一次攻击被护盾完全吸收，不会触发处决",
            kw = new[] { "护盾", "吸收", "生存" }, rel = new[] { "passive-shield-wall", "passive-death-explosion" } },
        new Def { id = "passive-slow-aura", name = "减速光环", cat = CodexCategory.Passive,
            desc = "持续降低周围敌人的移动速度。",
            extra = "· 敌人离开范围后仍会减速一段时间" + "\n" + "· 多个减速同时存在时只取最强的一个" + "\n" + "· 可选择只影响自身能攻击到的高度",
            kw = new[] { "减速", "光环", "控制" }, rel = new[] { "passive-king-aura", "passive-taunt" } },
        new Def { id = "passive-stun", name = "攻击眩晕", cat = CodexCategory.Passive,
            desc = "攻击命中时眩晕目标，并打断其正在进行的动作。",
            extra = "· 眩晕期间目标无法行动" + "\n" + "· 可设置每累计多少次攻击触发一次（多目标时一次攻击只算一次）",
            kw = new[] { "眩晕", "打断", "控制" }, rel = new[] { "passive-burst", "passive-charge" } },
        new Def { id = "passive-bleed", name = "流血", cat = CodexCategory.Passive,
            desc = "攻击会使目标流血，在持续时间内不断损失生命；不同单位造成的流血各自独立叠加。",
            extra = "· 同一来源重复命中，只叠加它自己那份的层数（有上限）" + "\n" + "· 不同来源的流血同时存在，每秒伤害相加" + "\n" + "· 目标受到伤害时，所有流血的持续时间都会被刷新" + "\n" + "· 流血自身的每秒伤害不触发刷新；只要持续受伤，流血就不会结束",
            kw = new[] { "流血", "持续伤害", "叠层" }, rel = new[] { "passive-tear", "passive-ignite" } },
        new Def { id = "passive-ignite", name = "燃烧", cat = CodexCategory.Passive,
            desc = "攻击会点燃目标，使其持续损失生命，火焰还会在同阵营单位间蔓延。",
            extra = "· 同一兵种的重复命中只叠加它自己那份的层数（有上限）" + "\n" + "· 不同兵种的燃烧各自独立，每秒伤害相加" + "\n" + "· 火焰在同阵营单位间蔓延，蔓延沿用来源兵种的数值" + "\n" + "· 被蔓延时刷新时间并叠层",
            kw = new[] { "燃烧", "点燃", "蔓延", "持续伤害" }, rel = new[] { "passive-burn", "passive-bleed" } },
        new Def { id = "passive-impair", name = "损伤", cat = CodexCategory.Passive,
            desc = "攻击会削弱命中目标的移动速度。",
            extra = "· 减速持续一段时间后自动恢复" + "\n" + "· 与减速光环同时存在时只取更强的一个",
            kw = new[] { "损伤", "减速", "削弱" }, rel = new[] { "passive-slow-aura", "passive-heavy-strike" } },
        new Def { id = "passive-heavy-strike", name = "重击", cat = CodexCategory.Passive,
            desc = "攻击会把命中的敌人击退，并打断其动作。",
            extra = "· 击退为平滑位移" + "\n" + "· 会打断目标正在进行的攻击",
            kw = new[] { "重击", "击退", "打断" }, rel = new[] { "passive-king-aura", "passive-impair" } },
        new Def { id = "passive-overheat", name = "过热", cat = CodexCategory.Passive,
            desc = "持续攻击会不断提升自身攻击力，停手后仍会保留一段时间。",
            extra = "· 连续攻击累计到整秒提升一档，攻击力加成有上限" + "\n" + "· 停手后保留一段时间，到期立即清零",
            kw = new[] { "过热", "攻击力", "持续输出" }, rel = new[] { "passive-swarm", "passive-charge" } },
        new Def { id = "passive-burst", name = "快速连击", cat = CodexCategory.Passive,
            desc = "连续攻击若干次后自我眩晕，进入冷却。",
            extra = "· 连击期间的出手频率可单独设置" + "\n" + "· 次数耗尽后立即进入自我眩晕",
            kw = new[] { "连击", "爆发", "脉冲" }, rel = new[] { "passive-stun", "passive-charge" } },
        new Def { id = "passive-tear", name = "撕裂", cat = CodexCategory.Passive,
            desc = "攻击会为目标叠加易伤，层数越多受到的伤害越高。",
            extra = "· 同一兵种的重复命中只叠加它自己那份的层数（有上限）" + "\n" + "· 不同兵种的易伤各自独立，效果相加" + "\n" + "· 反复命中会刷新持续时间",
            kw = new[] { "撕裂", "易伤", "叠加" }, rel = new[] { "passive-splash", "boss-demon" } },
        new Def { id = "passive-shockwave", name = "出场震波", cat = CodexCategory.Passive,
            desc = "出场时对周围敌人造成伤害并将其击退。",
            extra = "· 只影响自己能攻击到的高度" + "\n" + "· 击退距离可单独设置，关闭击退后只造成伤害",
            kw = new[] { "出场", "震波", "击退" }, rel = new[] { "passive-king-aura", "passive-death-explosion" } },
        new Def { id = "passive-burn", name = "死亡燃烧", cat = CodexCategory.Passive,
            desc = "死亡后原地留下持续一段时间的火海，灼烧经过的敌人。",
            extra = "· 只灼烧自己能攻击到的高度" + "\n" + "· 区域按每秒持续造成伤害",
            kw = new[] { "死亡", "燃烧", "封锁" }, rel = new[] { "passive-death-explosion", "rule-route" } },
        new Def { id = "passive-splash", name = "群体攻击", cat = CodexCategory.Passive,
            desc = "攻击附带范围伤害，可在「圆形波及」与「正面扇形」两种形态间选择。",
            extra = "· 圆形：以命中点为中心波及周围" + "\n" + "· 扇形：以自身朝向为基准、在攻击范围内横扫" + "\n" + "· 主目标默认不再吃这份额外范围伤害（可开启）",
            kw = new[] { "溅射", "群体", "范围", "扇形" }, rel = new[] { "passive-tear", "boss-demon" } },
        new Def { id = "passive-cavalry", name = "骑兵追远程", cat = CodexCategory.Passive,
            desc = "索敌时优先锁定攻击距离较远的敌人。",
            extra = "· 搜索范围比自身攻击范围更远" + "\n" + "· 优先选择远程单位，没有合适目标时退回最近的敌人",
            kw = new[] { "骑兵", "切后排", "索敌" }, rel = new[] { "passive-sniper", "passive-charge" } },
        new Def { id = "passive-summoner", name = "召唤师", cat = CodexCategory.Passive,
            desc = "定时召唤召唤物，击杀敌人时也会在其死亡位置召唤。",
            extra = "· 召唤物有同时存活上限" + "\n" + "· 定时召唤可设置每次生成的数量（受存活上限限制），多只时左右排开" + "\n" + "· 引导期间召唤师原地站定，在召唤帧生成" + "\n" + "· 眩晕会取消本次召唤；击杀召唤固定生成一只，召唤物击杀不再连锁",
            kw = new[] { "召唤", "增援", "上限" }, rel = new[] { "passive-swarm", "rule-play-limit" } },
        new Def { id = "passive-execute", name = "处决", cat = CodexCategory.Passive,
            desc = "造成伤害后，若目标生命值将低于阈值则直接击杀。",
            extra = "· 必须造成真实扣血才判定，伤害被护盾完全吸收时不触发" + "\n" + "· 对建筑无效",
            kw = new[] { "处决", "斩杀", "残血" }, rel = new[] { "passive-sniper", "passive-shield-absorb" } },
        new Def { id = "passive-heal", name = "治疗", cat = CodexCategory.Passive,
            desc = "把受伤友军作为攻击目标为其恢复生命，没有伤员时正常攻击敌人。",
            extra = "· 治疗走完整攻击流程，频率跟随攻击间隔" + "\n" + "· 优先治疗血量最低的友军，也可以治疗自己" + "\n" + "· 多目标单位会一次治疗所有选中目标" + "\n" + "· 治疗不会触发群体攻击、击晕等攻击类被动",
            kw = new[] { "治疗", "回复", "辅助" }, rel = new[] { "passive-shield-wall", "passive-shield-absorb" } },
        new Def { id = "passive-pounce", name = "飞扑", cat = CodexCategory.Passive,
            desc = "目标在攻击范围外但不远处时，跃到目标面前再开打。",
            extra = "· 落点刚好在攻击范围边缘，落地即可攻击" + "\n" + "· 飞行期间无视碰撞" + "\n" + "· 可选择飞行期间无敌、或是否会被眩晕打断",
            kw = new[] { "飞扑", "跃击", "位移", "突进" }, rel = new[] { "passive-charge", "passive-sniper" } },
        new Def { id = "passive-leader", name = "领袖", cat = CodexCategory.Passive,
            desc = "为范围内的友军提升攻击力、攻击速度与移动速度。",
            extra = "· 以自身为圆心，只影响同阵营单位" + "\n" + "· 默认不含自己（可开启）" + "\n" + "· 多名领袖的光环可以叠加" + "\n" + "· 友军离开范围或领袖阵亡时加成立即消失",
            kw = new[] { "领袖", "光环", "增伤", "攻速", "移速" }, rel = new[] { "passive-swarm", "passive-shield-wall" } },
    };

    // ── 现有条目的关键词补充（按 Id）─────────────────────────────
    private static readonly Dictionary<string, string[]> KeywordsById = new Dictionary<string, string[]>
    {
        { "cardtype-single", new[] { "单张", "一张", "基础" } },
        { "cardtype-pair", new[] { "对子", "两张", "双线" } },
        { "cardtype-triple", new[] { "三张", "三不带" } },
        { "cardtype-triple-one", new[] { "三带一", "带牌" } },
        { "cardtype-triple-pair", new[] { "三带二", "带对" } },
        { "cardtype-straight", new[] { "顺子", "连续", "五连" } },
        { "cardtype-consecutive-pair", new[] { "连对", "对子连" } },
        { "cardtype-plane", new[] { "飞机", "空中", "越障" } },
        { "cardtype-four-two", new[] { "四带二", "带牌" } },
        { "cardtype-bomb", new[] { "炸弹", "爆破", "压制" } },
        { "cardtype-kingbomb", new[] { "王炸", "双王", "最高" } },
        { "boss-knight", new[] { "BOSS", "骑士", "护盾", "减速", "撕裂" } },
        { "rule-shield", new[] { "护盾", "反制", "规则" } },
        { "rule-pass-domain", new[] { "要不起", "领域", "规则" } },
    };

    // ── 现有条目的关联补充（按 Id）───────────────────────────────
    private static readonly Dictionary<string, string[]> RelatedById = new Dictionary<string, string[]>
    {
        { "cardtype-single", new[] { "cardtype-pair", "cardtype-triple" } },
        { "cardtype-pair", new[] { "cardtype-triple", "cardtype-consecutive-pair" } },
        { "cardtype-triple", new[] { "cardtype-triple-one", "cardtype-triple-pair" } },
        { "cardtype-triple-one", new[] { "cardtype-triple", "cardtype-triple-pair" } },
        { "cardtype-triple-pair", new[] { "cardtype-triple", "cardtype-plane" } },
        { "cardtype-straight", new[] { "cardtype-straight6", "cardtype-consecutive-pair" } },
        { "cardtype-consecutive-pair", new[] { "cardtype-straight", "cardtype-pair" } },
        { "cardtype-plane", new[] { "cardtype-triple-pair", "passive-splash" } },
        { "cardtype-four-two", new[] { "cardtype-bomb", "cardtype-triple-one" } },
        { "cardtype-bomb", new[] { "cardtype-kingbomb", "passive-splash" } },
        { "cardtype-kingbomb", new[] { "cardtype-bomb", "rule-play-limit" } },
        { "boss-knight", new[] { "passive-shield-absorb", "passive-tear" } },
    };

    // 数据库路径（索引 = CodexCategory 枚举值）
    private static readonly string[] DbPaths =
    {
        Root + "CardValues/CardValues.asset",
        Root + "CardType/CardType.asset",
        Root + "BOSS/BOSS.asset",
        Root + "Building/Building.asset",
        Root + "Passive/Passive.asset",
        Root + "Rules/Rules.asset",
    };

    // 现有条目描述修正（王国骑士原本写的被动与实际预制体不符）
    private static readonly Dictionary<string, string[]> DescPatches = new Dictionary<string, string[]>
    {
        { "boss-knight", new[]
            {
                "重甲骑士型 BOSS，出场自带护盾，靠近的部队会被减速并逐层叠加易伤。必须先把护盾打穿，伤害才真正落到血量上。",
                "被动：护盾吸收 / 减速光环 / 撕裂　建议：先破盾再集火",
            } },
    };

    private static void AddToDb(CodexEntry entry)
    {
        int idx = (int)entry.Category;
        if (idx < 0 || idx >= DbPaths.Length) return;
        CodexDatabase db = AssetDatabase.LoadAssetAtPath<CodexDatabase>(DbPaths[idx]);
        if (db == null) return;
        if (db.Entries == null) db.Entries = new List<CodexEntry>();
        if (!db.Entries.Contains(entry))
        {
            db.Entries.Add(entry);
            EditorUtility.SetDirty(db);
        }
    }

    private static string FolderFor(CodexCategory cat)    {
        switch (cat)
        {
            case CodexCategory.CardValue: return Root + "CardValues";
            case CodexCategory.CardType: return Root + "CardType";
            case CodexCategory.Boss: return Root + "BOSS";
            case CodexCategory.Building: return Root + "Building";
            case CodexCategory.Passive: return Root + "Passive";
            default: return Root + "Rules";
        }
    }

    private static string IconFor(CodexCategory cat)
    {
        switch (cat)
        {
            case CodexCategory.CardValue: return "card";
            case CodexCategory.CardType: return "ladder";
            case CodexCategory.Boss: return "skull";
            case CodexCategory.Building: return "tower";
            case CodexCategory.Passive: return "chip";
            default: return "doc";
        }
    }

    [MenuItem("Tools/PixelWasteland/7 - Build Codex Content")]
    public static void BuildContent()
    {
        int created = 0, updated = 0;

        foreach (Def d in NewEntries)
        {
            string folder = FolderFor(d.cat);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Root.TrimEnd('/'), Path.GetFileName(folder));

            string path = folder + "/" + d.name + ".asset";
            CodexEntry e = AssetDatabase.LoadAssetAtPath<CodexEntry>(path);
            bool isNew = e == null;
            if (isNew)
            {
                e = ScriptableObject.CreateInstance<CodexEntry>();
                AssetDatabase.CreateAsset(e, path);
                created++;
            }
            else updated++;

            e.Id = d.id;
            e.DisplayName = d.name;
            e.Category = d.cat;
            // 已存在条目以人工整理/《被动技能总览》为准：重建时只补空字段，不覆盖已有描述
            if (isNew || string.IsNullOrEmpty(e.Description)) e.Description = d.desc;
            if (isNew || string.IsNullOrEmpty(e.ExtraInfo)) e.ExtraInfo = d.extra;
            e.Keywords = d.kw;
            e.RelatedEntries = d.rel;
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + IconFor(d.cat) + ".png");
            if (icon != null) e.Icon = icon;
            EditorUtility.SetDirty(e);
            AddToDb(e);
        }

        // 现有条目：补关键词 / 关联
        string[] guids = AssetDatabase.FindAssets("t:CodexEntry", new[] { Root.TrimEnd('/') });
        int kwFixed = 0, relFixed = 0;
        foreach (string g in guids)
        {
            CodexEntry e = AssetDatabase.LoadAssetAtPath<CodexEntry>(AssetDatabase.GUIDToAssetPath(g));
            if (e == null) continue;

            string[] kw;
            if (KeywordsById.TryGetValue(e.Id, out kw)) { e.Keywords = kw; kwFixed++; }
            else if (e.Keywords == null || e.Keywords.Length == 0)
            {
                e.Keywords = new[] { e.DisplayName, e.Category.ToString() };
                kwFixed++;
            }

            string[] rel;
            if (RelatedById.TryGetValue(e.Id, out rel)) { e.RelatedEntries = rel; relFixed++; }

            string[] patch;
            if (DescPatches.TryGetValue(e.Id, out patch))
            {
                e.Description = patch[0];
                e.ExtraInfo = patch[1];
            }

            AddToDb(e);
            EditorUtility.SetDirty(e);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[PixelWasteland] 内容补齐：新增 {0} 条，更新 {1} 条，补关键词 {2} 条，补关联 {3} 条",
            created, updated, kwFixed, relFixed));
    }
}
