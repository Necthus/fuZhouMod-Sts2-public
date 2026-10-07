using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using System.Runtime.CompilerServices;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Demons;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Enchantments;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 潘库魔气类型：对应八大恶魔的八种魔气。
/// </summary>
public enum PanKuDemonQiKind
{
    Heaven,
    Earth,
    Wind,
    Thunder,
    Water,
    Fire,
    Mountain,
    Moon
}

/// <summary>
/// 潘库宝盒辅助类：集中处理八魔气映射、附魔目标、地图标记和精英强化。
/// </summary>
public static class PanKuDemonQiHelper
{
    /// <summary>
    /// 多人模式下，第一名玩家之外的每名玩家额外提供的恶魔异常伤害上限比例。
    /// </summary>
    private const decimal AdditionalPlayerDamageCapRatio = 0.75m;

    /// <summary>
    /// 基础火冰异常每满 9 层进化一次。
    /// </summary>
    private const int BaseStatusEvolutionThreshold = 9;

    /// <summary>
    /// 每次进化会生成 3 层上级火冰异常。
    /// </summary>
    private const int EvolvedStatusAmountPerThreshold = 3;

    /// <summary>
    /// 已经强化过的潘库恶魔门战斗键，避免联机多个宝盒对同一场恶魔门精英重复强化。
    /// </summary>
    private static readonly HashSet<string> BuffedPanKuEliteKeys = [];

    /// <summary>
    /// 八种魔气的固定顺序，与一代 UI 文案顺序一致。
    /// </summary>
    public static readonly PanKuDemonQiKind[] AllKinds =
    [
        PanKuDemonQiKind.Heaven,
        PanKuDemonQiKind.Earth,
        PanKuDemonQiKind.Wind,
        PanKuDemonQiKind.Thunder,
        PanKuDemonQiKind.Water,
        PanKuDemonQiKind.Fire,
        PanKuDemonQiKind.Mountain,
        PanKuDemonQiKind.Moon
    ];

    /// <summary>
    /// 判断卡牌是否是八魔气恶魔卡。
    /// </summary>
    /// <param name="card">待判断卡牌。</param>
    /// <returns>是恶魔卡时返回 true。</returns>
    public static bool IsDemonQiCard(CardModel card)
    {
        return TryGetKindFromCard(card, out _);
    }

    /// <summary>
    /// 从卡牌实例识别对应魔气。
    /// </summary>
    /// <param name="card">待识别卡牌。</param>
    /// <param name="kind">识别出的魔气。</param>
    /// <returns>识别成功时返回 true。</returns>
    public static bool TryGetKindFromCard(CardModel card, out PanKuDemonQiKind kind)
    {
        kind = card switch
        {
            CardHeavenDemonQi => PanKuDemonQiKind.Heaven,
            CardEarthDemonQi => PanKuDemonQiKind.Earth,
            CardWindDemonQi => PanKuDemonQiKind.Wind,
            CardThunderDemonQi => PanKuDemonQiKind.Thunder,
            CardWaterDemonQi => PanKuDemonQiKind.Water,
            CardFireDemonQi => PanKuDemonQiKind.Fire,
            CardMountainDemonQi => PanKuDemonQiKind.Mountain,
            CardMoonDemonQi => PanKuDemonQiKind.Moon,
            _ => default
        };

        return IsDemonQiCardType(card.GetType());
    }

    /// <summary>
    /// 判断卡牌类型是否属于八魔气恶魔卡。
    /// </summary>
    /// <param name="type">卡牌类型。</param>
    /// <returns>是恶魔卡类型时返回 true。</returns>
    private static bool IsDemonQiCardType(Type type)
    {
        return type == typeof(CardHeavenDemonQi)
            || type == typeof(CardEarthDemonQi)
            || type == typeof(CardWindDemonQi)
            || type == typeof(CardThunderDemonQi)
            || type == typeof(CardWaterDemonQi)
            || type == typeof(CardFireDemonQi)
            || type == typeof(CardMountainDemonQi)
            || type == typeof(CardMoonDemonQi);
    }

    /// <summary>
    /// 创建指定魔气对应的恶魔卡。
    /// </summary>
    /// <param name="player">卡牌持有者。</param>
    /// <param name="kind">魔气类型。</param>
    /// <returns>可加入牌组的恶魔卡实例。</returns>
    public static CardModel CreateDemonCard(Player player, PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Heaven => player.RunState.CreateCard<CardHeavenDemonQi>(player),
            PanKuDemonQiKind.Earth => player.RunState.CreateCard<CardEarthDemonQi>(player),
            PanKuDemonQiKind.Wind => player.RunState.CreateCard<CardWindDemonQi>(player),
            PanKuDemonQiKind.Thunder => player.RunState.CreateCard<CardThunderDemonQi>(player),
            PanKuDemonQiKind.Water => player.RunState.CreateCard<CardWaterDemonQi>(player),
            PanKuDemonQiKind.Fire => player.RunState.CreateCard<CardFireDemonQi>(player),
            PanKuDemonQiKind.Mountain => player.RunState.CreateCard<CardMountainDemonQi>(player),
            PanKuDemonQiKind.Moon => player.RunState.CreateCard<CardMoonDemonQi>(player),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    /// <summary>
    /// 获取魔气短名，用于日志和潘库宝盒描述。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>中文短名。</returns>
    public static string GetShortName(PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Heaven => "天",
            PanKuDemonQiKind.Earth => "地",
            PanKuDemonQiKind.Wind => "风",
            PanKuDemonQiKind.Thunder => "雷",
            PanKuDemonQiKind.Water => "水",
            PanKuDemonQiKind.Fire => "火",
            PanKuDemonQiKind.Mountain => "山",
            PanKuDemonQiKind.Moon => "月",
            _ => "?"
        };
    }

    /// <summary>
    /// 获取当前语言下的魔气名称，用于玩家能看到的界面文本。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>当前语言下的魔气名称。</returns>
    public static string GetDisplayName(PanKuDemonQiKind kind)
    {
        return GetPanKuQiLocString(kind, "name").GetRawText();
    }

    /// <summary>
    /// 获取当前语言下的附魔效果说明。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>当前语言下的附魔效果说明。</returns>
    public static string GetEnchantmentEffectText(PanKuDemonQiKind kind)
    {
        return GetPanKuQiLocString(kind, "effect").GetRawText();
    }

    /// <summary>
    /// 获取当前语言下的可附魔类型说明。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>当前语言下的可附魔类型说明。</returns>
    public static string GetEnchantTargetTypeText(PanKuDemonQiKind kind)
    {
        return GetPanKuQiLocString(kind, "target").GetRawText();
    }

    /// <summary>
    /// 获取魔气附魔预览卡标题。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>魔气预览标题。</returns>
    public static LocString GetPreviewTitleLocString(PanKuDemonQiKind kind)
    {
        LocString title = new("cards", "SHENGZHUSTS2MOD-PAN_KU_QI.previewTitle");
        title.Add("Qi", GetDisplayName(kind));
        return title;
    }

    /// <summary>
    /// 获取魔气对应的图标路径，用于附魔图标和潘库宝盒魔气预览卡图。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>魔气图标资源路径。</returns>
    public static string GetIconPath(PanKuDemonQiKind kind)
    {
        return $"res://ShengZhuSts2Mod/images/orbs/masks/{GetIconFileName(kind)}.png";
    }

    /// <summary>
    /// 获取魔气对应的图标文件名。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>不带扩展名的图标文件名。</returns>
    private static string GetIconFileName(PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Heaven => "HeavenDemonQi",
            PanKuDemonQiKind.Earth => "EarthDemonQi",
            PanKuDemonQiKind.Wind => "WindDemonQi",
            PanKuDemonQiKind.Thunder => "ThunderDemonQi",
            PanKuDemonQiKind.Water => "WaterDemonQi",
            PanKuDemonQiKind.Fire => "FireDemonQi",
            PanKuDemonQiKind.Mountain => "MountainDemonQi",
            PanKuDemonQiKind.Moon => "MoonDemonQi",
            _ => "HeavenDemonQi"
        };
    }

    /// <summary>
    /// 组装潘库魔气本地化文本。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <param name="field">本地化字段。</param>
    /// <returns>魔气本地化文本。</returns>
    private static LocString GetPanKuQiLocString(PanKuDemonQiKind kind, string field)
    {
        return new LocString("cards", $"SHENGZHUSTS2MOD-PAN_KU_QI.{GetKindLocKey(kind)}.{field}");
    }

    /// <summary>
    /// 获取魔气本地化 key 后缀。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>本地化 key 后缀。</returns>
    private static string GetKindLocKey(PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Heaven => "heaven",
            PanKuDemonQiKind.Earth => "earth",
            PanKuDemonQiKind.Wind => "wind",
            PanKuDemonQiKind.Thunder => "thunder",
            PanKuDemonQiKind.Water => "water",
            PanKuDemonQiKind.Fire => "fire",
            PanKuDemonQiKind.Mountain => "mountain",
            PanKuDemonQiKind.Moon => "moon",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 获取魔气对应的八卦符号，用于潘库宝盒地图恶魔门标记。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>八卦符号。</returns>
    public static string GetBaguaSymbol(PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Fire => "☲",
            PanKuDemonQiKind.Thunder => "☳",
            PanKuDemonQiKind.Mountain => "☶",
            PanKuDemonQiKind.Wind => "☴",
            PanKuDemonQiKind.Heaven => "☰",
            PanKuDemonQiKind.Moon => "☵",
            PanKuDemonQiKind.Earth => "☷",
            PanKuDemonQiKind.Water => "☱",
            _ => "?"
        };
    }

    /// <summary>
    /// 统计玩家当前已拥有的魔气来源：恶魔卡、已附魔卡、寄宿魔气都会计入。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="panKuBox">潘库宝盒。</param>
    /// <returns>已拥有魔气集合。</returns>
    public static HashSet<PanKuDemonQiKind> CollectOwnedKinds(Player? player, PanKuBox? panKuBox)
    {
        HashSet<PanKuDemonQiKind> owned = [];
        if (player?.Deck?.Cards != null)
        {
            foreach (CardModel card in player.Deck.Cards)
            {
                if (TryGetKindFromCard(card, out PanKuDemonQiKind cardKind))
                {
                    owned.Add(cardKind);
                }

                if (TryGetKindFromEnchantment(card.Enchantment, out PanKuDemonQiKind enchantKind))
                {
                    owned.Add(enchantKind);
                }
            }
        }

        if (panKuBox != null)
        {
            foreach (PanKuDemonQiKind storedKind in panKuBox.GetStoredKinds())
            {
                owned.Add(storedKind);
            }
        }

        return owned;
    }

    /// <summary>
    /// 从附魔实例识别对应魔气。
    /// </summary>
    /// <param name="enchantment">待识别附魔。</param>
    /// <param name="kind">识别出的魔气。</param>
    /// <returns>识别成功时返回 true。</returns>
    public static bool TryGetKindFromEnchantment(EnchantmentModel? enchantment, out PanKuDemonQiKind kind)
    {
        kind = enchantment switch
        {
            HeavenDemonQiEnchantment => PanKuDemonQiKind.Heaven,
            EarthDemonQiEnchantment => PanKuDemonQiKind.Earth,
            WindDemonQiEnchantment => PanKuDemonQiKind.Wind,
            ThunderDemonQiEnchantment => PanKuDemonQiKind.Thunder,
            WaterDemonQiEnchantment => PanKuDemonQiKind.Water,
            FireDemonQiEnchantment => PanKuDemonQiKind.Fire,
            MountainDemonQiEnchantment => PanKuDemonQiKind.Mountain,
            MoonDemonQiEnchantment => PanKuDemonQiKind.Moon,
            _ => default
        };

        return enchantment is AbstractDemonQiEnchantment;
    }

    /// <summary>
    /// 判断指定卡牌能否被指定魔气附魔。
    /// </summary>
    /// <param name="card">候选卡牌。</param>
    /// <param name="kind">魔气类型。</param>
    /// <returns>可附魔时返回 true。</returns>
    public static bool CanEnchant(CardModel card, PanKuDemonQiKind kind)
    {
        return kind switch
        {
            PanKuDemonQiKind.Heaven => ModelDb.Enchantment<HeavenDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Earth => ModelDb.Enchantment<EarthDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Wind => ModelDb.Enchantment<WindDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Thunder => ModelDb.Enchantment<ThunderDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Water => ModelDb.Enchantment<WaterDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Fire => ModelDb.Enchantment<FireDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Mountain => ModelDb.Enchantment<MountainDemonQiEnchantment>().CanEnchant(card),
            PanKuDemonQiKind.Moon => ModelDb.Enchantment<MoonDemonQiEnchantment>().CanEnchant(card),
            _ => false
        };
    }

    /// <summary>
    /// 给指定卡牌施加魔气附魔。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <param name="kind">魔气类型。</param>
    public static void Enchant(CardModel card, PanKuDemonQiKind kind)
    {
        switch (kind)
        {
            case PanKuDemonQiKind.Heaven:
                CardCmd.Enchant<HeavenDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Earth:
                CardCmd.Enchant<EarthDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Wind:
                CardCmd.Enchant<WindDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Thunder:
                CardCmd.Enchant<ThunderDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Water:
                CardCmd.Enchant<WaterDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Fire:
                CardCmd.Enchant<FireDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Mountain:
                CardCmd.Enchant<MountainDemonQiEnchantment>(card, 1);
                break;
            case PanKuDemonQiKind.Moon:
                CardCmd.Enchant<MoonDemonQiEnchantment>(card, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>
    /// 获取当前牌组里可被指定魔气附魔的卡牌。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="kind">魔气类型。</param>
    /// <returns>可附魔目标。</returns>
    public static List<CardModel> GetEnchantTargets(Player player, PanKuDemonQiKind kind)
    {
        return PileType.Deck.GetPile(player).Cards
            .Where(card => CanEnchant(card, kind))
            .ToList();
    }

    /// <summary>
    /// 将火异常施加给目标，若已有潮湿则合成为高温蒸汽。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task ApplyBurning(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        SoakedPower? soaked = target.GetPower<SoakedPower>();
        if (soaked != null)
        {
            int oldSoakedAmount = soaked.Amount;
            int steamAmount = Math.Min(amount, oldSoakedAmount);
            int remainingBurning = amount - steamAmount;
            await ReduceOrRemovePower(soaked, steamAmount, source, cardSource);
            await PowerCmd.Apply<SteamPower>(target, steamAmount, source, cardSource);
            MainFile.Logger.Info($"【恶魔异常合成】灼烧遇潮湿：新增灼烧={amount}，已有潮湿={oldSoakedAmount}，合成高温蒸汽={steamAmount}，剩余灼烧={remainingBurning}。");

            if (remainingBurning <= 0)
            {
                return;
            }

            await ApplyBurningAndEvolve(target, remainingBurning, source, cardSource);
            return;
        }

        await ApplyBurningAndEvolve(target, amount, source, cardSource);
    }

    /// <summary>
    /// 将水异常施加给目标，若已有灼烧则合成为高温蒸汽。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task ApplySoaked(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        BurningPower? burning = target.GetPower<BurningPower>();
        if (burning != null)
        {
            int oldBurningAmount = burning.Amount;
            int steamAmount = Math.Min(amount, oldBurningAmount);
            int remainingSoaked = amount - steamAmount;
            await ReduceOrRemovePower(burning, steamAmount, source, cardSource);
            await PowerCmd.Apply<SteamPower>(target, steamAmount, source, cardSource);
            MainFile.Logger.Info($"【恶魔异常合成】潮湿遇灼烧：新增潮湿={amount}，已有灼烧={oldBurningAmount}，合成高温蒸汽={steamAmount}，剩余潮湿={remainingSoaked}。");

            if (remainingSoaked <= 0)
            {
                return;
            }

            await ApplySoakedAndFuse(target, remainingSoaked, source, cardSource);
            return;
        }

        if (target.GetPower<FrostbitePower>() != null || target.GetPower<FrostHellPower>() != null)
        {
            await ApplyFrostbiteAndEvolve(target, amount, source, cardSource);
            return;
        }

        await ApplySoakedAndFuse(target, amount, source, cardSource);
    }

    /// <summary>
    /// 将地缚施加给目标，若已有潮湿则合成为沉重泥沼。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task ApplyEarthBind(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        if (target == null || !target.IsAlive || amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<EarthBindPower>(target, amount, source, cardSource);
        await TryFuseEarthBindWithSoaked(target, source, cardSource);
    }

    /// <summary>
    /// 尝试把目标身上的地缚和潮湿合成为沉重泥沼；潮湿按一代规则保留。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task TryFuseEarthBindWithSoaked(Creature target, Creature? source, CardModel? cardSource)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        EarthBindPower? earthBind = target.GetPower<EarthBindPower>();
        SoakedPower? soaked = target.GetPower<SoakedPower>();
        if (earthBind == null || soaked == null)
        {
            return;
        }

        Creature applier = source ?? earthBind.Applier ?? soaked.Applier ?? target;
        await PowerCmd.Remove(earthBind);
        await PowerCmd.Apply<HeavyMirePower>(target, 1, applier, cardSource);
        MainFile.Logger.Info($"【恶魔异常合成】地缚遇潮湿：目标={target.Name}，移除地缚，保留潮湿，生成沉重泥沼。");
    }

    /// <summary>
    /// 把目标身上的潮湿转化为同层数冻伤，用于还原一代“潮湿 + 风势 -> 冻伤”的融合。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task ConvertSoakedToFrostbite(Creature target, Creature source, CardModel? cardSource)
    {
        SoakedPower? soaked = target.GetPower<SoakedPower>();
        if (soaked == null || soaked.Amount <= 0)
        {
            return;
        }

        int soakedAmount = soaked.Amount;
        await PowerCmd.Remove(soaked);
        await ApplyFrostbiteAndEvolve(target, soakedAmount, source, cardSource);
        MainFile.Logger.Info($"【恶魔异常合成】潮湿遇风势：目标={target.Name}，潮湿={soakedAmount}，转化为同层数冻伤。");
    }

    /// <summary>
    /// 施加灼烧并按一代规则把每 9 层灼烧进化为 3 层灼心。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">新增灼烧层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static async Task ApplyBurningAndEvolve(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<BurningPower>(target, amount, source, cardSource);
        await EvolveBaseStatus<BurningPower, BurningHeartPower>(target, source, cardSource, "灼烧", "灼心");
    }

    /// <summary>
    /// 施加冻伤并按一代规则把每 9 层冻伤进化为 3 层冰狱。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">新增冻伤层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static async Task ApplyFrostbiteAndEvolve(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<FrostbitePower>(target, amount, source, cardSource);
        await EvolveBaseStatus<FrostbitePower, FrostHellPower>(target, source, cardSource, "冻伤", "冰狱");
    }

    /// <summary>
    /// 施加潮湿，并在目标已有风势时立刻转化为冻伤。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="amount">新增潮湿层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static async Task ApplySoakedAndFuse(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<SoakedPower>(target, amount, source, cardSource);
        if (target.GetPower<WindCatalystPower>() != null)
        {
            await ConvertSoakedToFrostbite(target, source, cardSource);
        }
    }

    /// <summary>
    /// 基础火冰异常每满 9 层转化为 3 层上级异常，余数保留。
    /// </summary>
    /// <typeparam name="TBasePower">基础异常类型。</typeparam>
    /// <typeparam name="TEvolvedPower">上级异常类型。</typeparam>
    /// <param name="target">目标。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="baseName">基础异常中文名。</param>
    /// <param name="evolvedName">上级异常中文名。</param>
    private static async Task EvolveBaseStatus<TBasePower, TEvolvedPower>(Creature target, Creature source, CardModel? cardSource, string baseName, string evolvedName)
        where TBasePower : PowerModel
        where TEvolvedPower : PowerModel, new()
    {
        TBasePower? basePower = target.GetPower<TBasePower>();
        if (basePower == null || basePower.Amount < BaseStatusEvolutionThreshold)
        {
            return;
        }

        int oldAmount = basePower.Amount;
        int evolveCount = oldAmount / BaseStatusEvolutionThreshold;
        int remaining = oldAmount % BaseStatusEvolutionThreshold;
        int evolvedAmount = evolveCount * EvolvedStatusAmountPerThreshold;
        await PowerCmd.Apply<TEvolvedPower>(target, evolvedAmount, source, cardSource);

        if (remaining <= 0)
        {
            await PowerCmd.Remove(basePower);
        }
        else
        {
            await PowerCmd.ModifyAmount(basePower, remaining - oldAmount, source, cardSource);
        }

        MainFile.Logger.Info($"【恶魔异常进化】{baseName}进化为{evolvedName}：目标={target.Name}，原层数={oldAmount}，进化层数={evolvedAmount}，剩余{baseName}={remaining}。");
    }

    /// <summary>
    /// 按魔气异常统一伤害规则造成生命百分比伤害。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="target">目标。</param>
    /// <param name="source">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="divisor">生命除数。</param>
    /// <param name="minDamage">最低伤害。</param>
    /// <param name="maxDamage">最高伤害。</param>
    public static async Task DealDemonQiPercentDamage(PlayerChoiceContext choiceContext, Creature target, Creature? source, CardModel? cardSource, int divisor, int minDamage, int maxDamage)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        int damage = CalculateDemonQiPercentDamage(target, source, cardSource, divisor, minDamage, maxDamage, out int rawDamage, out int playerCount, out int scaledMaxDamage, out bool ignoreDamageCap);

        MainFile.Logger.Info($"【恶魔异常伤害】目标={target.Name}，原始伤害={rawDamage}，最低={minDamage}，单人上限={maxDamage}，玩家数={playerCount}，额外玩家系数={AdditionalPlayerDamageCapRatio:0.##}，多人上限={scaledMaxDamage}，队伍解除上限={ignoreDamageCap}，最终伤害={damage}。");
        await CreatureCmd.Damage(choiceContext, target, damage, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable | MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, source, cardSource, null);
    }

    /// <summary>
    /// 计算恶魔异常下一次按生命比例造成的伤害，只用于预览或真实结算前取值，不修改任何战斗状态。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="source">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="divisor">生命除数。</param>
    /// <param name="minDamage">最低伤害。</param>
    /// <param name="maxDamage">单人最高伤害。</param>
    /// <returns>按当前血量、人数和解除限制状态算出的最终伤害。</returns>
    public static int CalculateDemonQiPercentDamage(Creature target, Creature? source, CardModel? cardSource, int divisor, int minDamage, int maxDamage)
    {
        return CalculateDemonQiPercentDamage(target, source, cardSource, divisor, minDamage, maxDamage, out _, out _, out _, out _);
    }

    /// <summary>
    /// 计算恶魔异常伤害，并把日志和描述需要的中间值一并返回。
    /// </summary>
    /// <param name="target">目标。</param>
    /// <param name="source">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="divisor">生命除数。</param>
    /// <param name="minDamage">最低伤害。</param>
    /// <param name="maxDamage">单人最高伤害。</param>
    /// <param name="rawDamage">未套最低值和上限前的原始伤害。</param>
    /// <param name="playerCount">当前战斗玩家人数。</param>
    /// <param name="scaledMaxDamage">多人缩放后的最高伤害。</param>
    /// <param name="ignoreDamageCap">当前是否跳过最高伤害上限。</param>
    /// <returns>最终伤害。</returns>
    private static int CalculateDemonQiPercentDamage(Creature target, Creature? source, CardModel? cardSource, int divisor, int minDamage, int maxDamage, out int rawDamage, out int playerCount, out int scaledMaxDamage, out bool ignoreDamageCap)
    {
        rawDamage = 0;
        playerCount = GetCombatPlayerCount(target, source, cardSource);
        scaledMaxDamage = CalculateMultiplayerDamageCap(maxDamage, playerCount);
        bool hasGrandMageBlessing = target.HasPower<GrandMageBlessingPower>();
        if (hasGrandMageBlessing)
        {
            scaledMaxDamage = Math.Min(scaledMaxDamage, GrandMageBlessingPower.DemonQiSingleDamageCap);
        }

        ignoreDamageCap = !hasGrandMageBlessing && ShouldIgnoreDemonQiDamageCap(target, source, cardSource);

        if (target == null || target.CurrentHp <= 0 || divisor <= 0)
        {
            return 0;
        }

        rawDamage = target.CurrentHp / divisor;
        int damage = Math.Max(rawDamage, minDamage);
        if (!ignoreDamageCap)
        {
            damage = Math.Min(damage, scaledMaxDamage);
        }

        return Math.Max(0, damage);
    }

    /// <summary>
    /// 将目标正数力量和敏捷变为等量负数。
    /// </summary>
    /// <param name="target">需要反转数值的目标。</param>
    /// <param name="source">效果来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task InvertPositiveStrengthAndDexterity(Creature target, Creature? source, CardModel? cardSource)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        StrengthPower? strength = target.GetPower<StrengthPower>();
        if (strength != null && strength.Amount > 0)
        {
            await PowerCmd.ModifyAmount(strength, -strength.Amount * 2, source, cardSource);
        }

        DexterityPower? dexterity = target.GetPower<DexterityPower>();
        if (dexterity != null && dexterity.Amount > 0)
        {
            await PowerCmd.ModifyAmount(dexterity, -dexterity.Amount * 2, source, cardSource);
        }
    }

    /// <summary>
    /// 按指定数量扣除能力层数，扣完时直接移除能力。
    /// </summary>
    /// <param name="power">待扣除的能力。</param>
    /// <param name="amount">扣除层数。</param>
    /// <param name="source">来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static async Task ReduceOrRemovePower(PowerModel power, int amount, Creature source, CardModel? cardSource)
    {
        if (power.Amount <= amount)
        {
            await PowerCmd.Remove(power);
            return;
        }

        await PowerCmd.ModifyAmount(power, -amount, source, cardSource);
    }

    /// <summary>
    /// 计算多人模式下的恶魔异常伤害上限。
    /// </summary>
    /// <param name="baseMaxDamage">单人伤害上限。</param>
    /// <param name="playerCount">当前战斗玩家人数。</param>
    /// <returns>多人缩放后的伤害上限。</returns>
    public static int CalculateMultiplayerDamageCap(int baseMaxDamage, int playerCount)
    {
        decimal multiplier = 1m + (Math.Max(1, playerCount) - 1) * AdditionalPlayerDamageCapRatio;
        return Math.Max(baseMaxDamage, (int)Math.Floor(baseMaxDamage * multiplier));
    }

    /// <summary>
    /// 获取当前战斗玩家人数，拿不到战斗信息时按单人处理。
    /// </summary>
    /// <param name="target">伤害目标。</param>
    /// <param name="source">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>玩家人数。</returns>
    public static int GetCombatPlayerCount(Creature target, Creature? source, CardModel? cardSource)
    {
        int? combatPlayerCount = target.CombatState?.Players.Count
            ?? source?.CombatState?.Players.Count
            ?? cardSource?.Owner?.RunState?.Players.Count;
        return Math.Max(1, combatPlayerCount ?? 1);
    }

    /// <summary>
    /// 判断当前恶魔异常伤害是否应跳过最高伤害上限。
    /// </summary>
    /// <param name="target">伤害目标。</param>
    /// <param name="source">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>队伍中任意玩家持有月之解除限制时返回 true。</returns>
    private static bool ShouldIgnoreDemonQiDamageCap(Creature target, Creature? source, CardModel? cardSource)
    {
        IEnumerable<Player>? players = target.CombatState?.Players
            ?? source?.CombatState?.Players
            ?? cardSource?.Owner?.RunState?.Players;
        return players?.Any(player => player?.Creature?.GetPower<MoonUncapPower>() != null) == true;
    }

    /// <summary>
    /// 把潘库宝盒记录的坐标重新挂回地图任务标记。
    /// </summary>
    /// <param name="box">潘库宝盒。</param>
    /// <param name="map">当前地图。</param>
    public static void RestoreMapQuestMarks(PanKuBox box, ActMap map)
    {
        foreach (MapPoint point in map.GetAllMapPoints())
        {
            if (!box.HasMarkedCoord(point.coord))
            {
                continue;
            }

            if (point.PointType == MapPointType.Monster)
            {
                point.PointType = MapPointType.Elite;
            }

            if (point.PointType != MapPointType.Elite)
            {
                MainFile.Logger.Info($"【潘库宝盒】跳过非法地狱之门恢复：列={point.coord.col}，行={point.coord.row}，节点类型={point.PointType}。");
                continue;
            }

            if (!point.Quests.OfType<PanKuBox>().Any(existing => ReferenceEquals(existing, box)))
            {
                point.AddQuest(box);
            }
        }
    }

    /// <summary>
    /// 给潘库恶魔门战斗里的出场精英添加与抢夺符咒事件一致的强化。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <param name="player">当前玩家。</param>
    public static async Task ApplyPanKuEliteBuff(CombatRoom combatRoom, Player player)
    {
        if (combatRoom?.CombatState == null || player?.Creature == null)
        {
            MainFile.Logger.Info("【潘库宝盒】恶魔门精英强化失败：战斗房间或玩家为空。");
            return;
        }

        string buffKey = GetPanKuEliteBuffKey(combatRoom, player);
        if (!BuffedPanKuEliteKeys.Add(buffKey))
        {
            MainFile.Logger.Info($"【潘库宝盒】恶魔门精英已强化过，跳过重复强化：键={buffKey}。");
            return;
        }

        int ascensionLevel = Math.Max(0, player.RunState.AscensionLevel);
        int playerCount = Math.Max(1, player.RunState.Players.Count);
        int totalHpBonus = RobTalismanEventHelper.RobTalismanEliteBaseHpBonus
            + ascensionLevel * RobTalismanEventHelper.RobTalismanEliteAscensionHpBonus
            + playerCount * RobTalismanEventHelper.RobTalismanElitePlayerHpBonus;
        List<Creature> targets = combatRoom.CombatState.Enemies
            .Where(enemy => ShouldBuffPanKuElite(enemy, combatRoom))
            .ToList();

        MainFile.Logger.Info($"【潘库宝盒】恶魔门精英强化计算：公式=20 + 进阶数({ascensionLevel}) * 5 + 人数({playerCount}) * 20，总生命增加={totalHpBonus}，总力量增加={RobTalismanEventHelper.RobTalismanEliteStrengthBonus}，目标数量={targets.Count}。");
        for (int i = 0; i < targets.Count; i++)
        {
            Creature target = targets[i];
            int hpBonus = RobTalismanEventHelper.CalculateEvenlyDistributedEliteBonus(totalHpBonus, targets.Count, i);
            int strengthBonus = RobTalismanEventHelper.CalculateEvenlyDistributedEliteBonus(RobTalismanEventHelper.RobTalismanEliteStrengthBonus, targets.Count, i);
            int oldMaxHp = target.MaxHp;
            int oldCurrentHp = target.CurrentHp;
            if (hpBonus > 0)
            {
                await CreatureCmd.GainMaxHp(target, hpBonus);
            }
            if (strengthBonus > 0)
            {
                await PowerCmd.Apply<StrengthPower>(target, strengthBonus, player.Creature, null);
            }
            PanKuElitePower? power = await PowerCmd.Apply<PanKuElitePower>(target, hpBonus, player.Creature, null);
            power?.SetFormulaDetails(ascensionLevel, playerCount, strengthBonus, totalHpBonus);
            MainFile.Logger.Info($"【潘库宝盒】已强化恶魔门精英：目标={target.Name}，序号={i + 1}/{targets.Count}，最大生命={oldMaxHp}+{hpBonus}=>{target.MaxHp}，当前生命={oldCurrentHp}+{hpBonus}=>{target.CurrentHp}，力量增加={strengthBonus}。");
        }
    }

    /// <summary>
    /// 生成潘库恶魔门精英强化键，用同一存档、同一幕、同一地图坐标识别同一场恶魔门战斗。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <param name="player">当前玩家。</param>
    /// <returns>强化去重键。</returns>
    private static string GetPanKuEliteBuffKey(CombatRoom combatRoom, Player player)
    {
        MapCoord? coord = player.RunState.CurrentMapCoord;
        string coordKey = coord == null ? $"Room:{combatRoom.GetHashCode()}" : $"{coord.Value.col}:{coord.Value.row}";
        return $"{RuntimeHelpers.GetHashCode(player.RunState):X8}:{player.RunState.Rng.StringSeed}:{player.RunState.CurrentActIndex}:{coordKey}";
    }

    /// <summary>
    /// 判断候选敌人是否应该获得潘库恶魔门强化；逻辑与抢夺符咒事件保持一致。
    /// </summary>
    /// <param name="enemy">候选敌人。</param>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>应该强化时返回 true。</returns>
    private static bool ShouldBuffPanKuElite(Creature enemy, CombatRoom combatRoom)
    {
        if (enemy == null || !enemy.IsMonster || enemy.Monster == null)
        {
            MainFile.Logger.Info("【潘库宝盒】跳过恶魔门强化候选：目标为空或不是怪物。");
            return false;
        }

        if (!enemy.IsAlive)
        {
            MainFile.Logger.Info($"【潘库宝盒】跳过恶魔门强化候选：目标={enemy.Name}，原因=目标已死亡。");
            return false;
        }

        if (!enemy.CanReceivePowers)
        {
            MainFile.Logger.Info($"【潘库宝盒】跳过恶魔门强化候选：目标={enemy.Name}，原因=无法接收能力。");
            return false;
        }

        if (IsMinionLike(enemy))
        {
            MainFile.Logger.Info($"【潘库宝盒】跳过恶魔门强化候选：目标={enemy.Name}，原因=疑似爪牙或召唤物，怪物={enemy.Monster.Id.Entry}，槽位={enemy.SlotName ?? "空"}。");
            return false;
        }

        if (!combatRoom.Encounter.HaveMonstersBeenGenerated || !combatRoom.Encounter.MonstersWithSlots.Any(monsterWithSlot => monsterWithSlot.Item1 == enemy.Monster))
        {
            MainFile.Logger.Info($"【潘库宝盒】跳过恶魔门强化候选：目标={enemy.Name}，原因=不是本场初始出场怪。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 通过怪物 Id 和槽位名保守判断是否是爪牙或召唤物。
    /// </summary>
    /// <param name="enemy">候选敌人。</param>
    /// <returns>疑似爪牙或召唤物时返回 true。</returns>
    private static bool IsMinionLike(Creature enemy)
    {
        string monsterId = enemy.Monster?.Id.Entry ?? string.Empty;
        string slotName = enemy.SlotName ?? string.Empty;
        return ContainsMinionKeyword(monsterId) || ContainsMinionKeyword(slotName);
    }

    /// <summary>
    /// 判断文本是否包含常见爪牙关键词。
    /// </summary>
    /// <param name="text">待检查文本。</param>
    /// <returns>包含关键词时返回 true。</returns>
    private static bool ContainsMinionKeyword(string text)
    {
        return text.Contains("minion", StringComparison.OrdinalIgnoreCase)
            || text.Contains("summon", StringComparison.OrdinalIgnoreCase)
            || text.Contains("spawn", StringComparison.OrdinalIgnoreCase)
            || text.Contains("helper", StringComparison.OrdinalIgnoreCase);
    }
}
