using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 符咒遗物共享池补丁：在玩家遗物抓取袋填充后，将符咒遗物注入非圣主角色的遗物袋中。
/// 游戏本体的 RelicGrabBag.Populate 只从 SharedRelicPool + 角色专属池取遗物，
/// 不会自动包含其他 Mod 的共享池，因此需要手动注入。
/// </summary>
[HarmonyPatch]
public static class TalismanSharedPoolPatch
{
    /// <summary>
    /// RelicGrabBag 内部存储遗物的字典字段。
    /// </summary>
    private static readonly FieldInfo? DequesField =
        typeof(RelicGrabBag).GetField("_deques", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 所有十二符咒遗物类型。
    /// </summary>
    private static readonly Type[] AllTalismanTypes =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 动态指定要 Patch 的方法：RelicGrabBag.Populate(Player, Rng)。
    /// 使用 TargetMethod 方式避免编译时类型解析问题。
    /// </summary>
    public static MethodBase TargetMethod()
    {
        MethodInfo? method = typeof(RelicGrabBag).GetMethod(
            "Populate",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(Player), typeof(Rng)],
            null);

        if (method != null)
        {
            MainFile.Logger.Info("【符咒共享池】成功找到 RelicGrabBag.Populate(Player, Rng) 方法进行 Patch。");
        }
        else
        {
            MainFile.Logger.Info("【符咒共享池】未找到 RelicGrabBag.Populate(Player, Rng) 方法！Patch 将不生效。");
        }

        return method!;
    }

    /// <summary>
    /// 后置补丁：遗物袋填充完成后，若玩家不是圣主角色，则将符咒遗物注入其遗物袋。
    /// 圣主角色的遗物袋已经包含符咒（因为符咒在圣主专属池中），无需重复注入。
    /// </summary>
    public static void Postfix(RelicGrabBag __instance, Player player, Rng rng)
    {
        if (DequesField == null)
        {
            MainFile.Logger.Info("【符咒共享池】无法获取 RelicGrabBag._deques 字段，符咒注入失败。");
            return;
        }

        var deques = DequesField.GetValue(__instance) as Dictionary<RelicRarity, List<RelicModel>>;
        if (deques == null)
        {
            MainFile.Logger.Info("【符咒共享池】_deques 为空，符咒注入失败。");
            return;
        }

        RemoveShadowKhanTokenFromGrabBag(deques, player);

        // 圣主角色的池已经包含符咒，不需要额外注入
        if (player.Character is ShengZhu)
        {
            MainFile.Logger.Info("【符咒共享池】圣主角色，跳过注入。");
            return;
        }

        // 获取 Rare 稀有度的遗物列表
        if (!deques.TryGetValue(RelicRarity.Rare, out List<RelicModel>? rareList))
        {
            rareList = new List<RelicModel>();
            deques[RelicRarity.Rare] = rareList;
        }

        // 将所有符咒遗物注入到 Rare 列表中
        int injectedCount = 0;
        foreach (Type talismanType in AllTalismanTypes)
        {
            RelicModel? talisman = ModelDb.AllRelics.FirstOrDefault(r => r.GetType() == talismanType);
            if (talisman == null)
            {
                continue;
            }

            // 避免重复注入（正常不会重复，但做防御性检查）
            if (rareList.Any(r => r.Id == talisman.Id))
            {
                continue;
            }

            rareList.Add(talisman);
            injectedCount++;
        }

        // 注入后重新打乱 Rare 列表顺序
        if (injectedCount > 0)
        {
            rareList.UnstableShuffle(rng);
            MainFile.Logger.Info($"【符咒共享池】已向 {player.Character.GetType().Name} 的遗物袋注入 {injectedCount} 个符咒遗物。");
        }
    }

    /// <summary>
    /// 防御性移除黑影令牌，保证它只通过圣主初始遗物获得，不进入奖励、商店、宝箱或事件随机遗物来源。
    /// </summary>
    /// <param name="deques">遗物抓取袋内部列表。</param>
    /// <param name="player">当前玩家。</param>
    private static void RemoveShadowKhanTokenFromGrabBag(Dictionary<RelicRarity, List<RelicModel>> deques, Player player)
    {
        int removedCount = 0;
        foreach (List<RelicModel> relics in deques.Values)
        {
            removedCount += relics.RemoveAll(relic => relic is ShadowKhanToken);
        }

        if (removedCount > 0)
        {
            MainFile.Logger.Info($"【黑影令牌】已从 {player.Character.GetType().Name} 的遗物抓取袋移除 {removedCount} 个黑影令牌，避免随机获得。");
        }
    }
}
