using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

/// <summary>
/// 丑牛能力：敌人的力量和敏捷无法高于你。
/// 尽量实时检查并压制敌人的力量和敏捷。
/// </summary>
public class OxTalismanCardPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 持有牛符咒遗物时，敌人力量上限相对玩家力量降低的数值。
    /// </summary>
    private const int OxRelicStrengthCapOffset = 3;

    /// <summary>
    /// 丑牛属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 回合开始时检查并压制所有敌人的力量和敏捷。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner)
        {
            return;
        }

        await EnforceAllEnemies();
    }

    /// <summary>
    /// 每当一张牌被打出后也检查一次（覆盖敌人在回合中获得增益的情况）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await EnforceAllEnemies();
    }

    /// <summary>
    /// 任意力量/敏捷能力变化后立即检查，尽量对齐1代每帧压制效果。
    /// </summary>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is not StrengthPower && power is not DexterityPower)
        {
            return;
        }

        await EnforceAllEnemies();
    }

    /// <summary>
    /// 压制所有存活敌人的力量和敏捷。
    /// </summary>
    private async Task EnforceAllEnemies()
    {
        if (Owner == null || Owner.CombatState == null)
        {
            return;
        }

        var opponents = Owner.CombatState.GetOpponentsOf(Owner)
            .Where(c => c.IsAlive && !c.IsDead);

        foreach (Creature enemy in opponents)
        {
            await EnforceStatCap(enemy);
        }
    }

    /// <summary>
    /// 压制指定敌人的力量和敏捷，使其不超过玩家。
    /// </summary>
    private async Task EnforceStatCap(Creature enemy)
    {
        if (Owner == null)
        {
            return;
        }

        // 获取玩家力量
        int playerStr = GetStrengthAmount(Owner);
        int strengthCap = GetStrengthCap(playerStr);
        int enemyStr = GetStrengthAmount(enemy);

        if (enemyStr > strengthCap)
        {
            int diff = strengthCap - enemyStr;
            await PowerCmd.Apply<StrengthPower>(enemy, diff, Owner, null);
            Flash();
            MainFile.Logger.Info($"【丑牛】压制敌人力量：{enemyStr} → {strengthCap}。");
        }

        // 获取玩家敏捷
        int playerDex = GetDexterityAmount(Owner);
        int enemyDex = GetDexterityAmount(enemy);

        if (enemyDex > playerDex)
        {
            int diff = playerDex - enemyDex;
            await PowerCmd.Apply<DexterityPower>(enemy, diff, Owner, null);
            Flash();
            MainFile.Logger.Info($"【丑牛】压制敌人敏捷：{enemyDex} → {playerDex}。");
        }
    }

    /// <summary>
    /// 获取指定生物的力量层数。
    /// </summary>
    private static int GetStrengthAmount(Creature creature)
    {
        var power = creature.GetPower<StrengthPower>();
        return power?.Amount ?? 0;
    }

    /// <summary>
    /// 获取指定生物的敏捷层数。
    /// </summary>
    private static int GetDexterityAmount(Creature creature)
    {
        var power = creature.GetPower<DexterityPower>();
        return power?.Amount ?? 0;
    }

    /// <summary>
    /// 计算当前力量上限；持有牛符咒遗物时，敌人力量最多只能到玩家力量-3。
    /// </summary>
    private int GetStrengthCap(int playerStrength)
    {
        return Owner?.Player?.GetRelic<OxTalisman>() != null
            ? playerStrength - OxRelicStrengthCapOffset
            : playerStrength;
    }
}
