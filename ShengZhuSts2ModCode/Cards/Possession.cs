using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 夺舍：本回合未使用攻击牌时才能打出，眩晕目标并夺取指定增益与护甲。
/// </summary>
public class Possession : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：2费蓝色技能，消耗，施加2(3)易伤虚弱和1(2)摧残。
    /// </summary>
    public Possession() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithVar("Debuff", 2, 1);
        WithVar("Mangle", 1, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 本回合已经打出攻击牌时不可使用。
    /// </summary>
    protected override bool IsPlayable => !AttackCardPlayHelper.HasAttackPlayedThisTurn(this);

    /// <summary>
    /// 出牌时禁止本回合继续使用攻击牌，眩晕并夺取目标能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        Creature target = cardPlay.Target;
        await PowerCmd.Apply<PossessionNoAttackPower>(Owner.Creature, 1, Owner.Creature, this);
        await CreatureCmd.Stun(target);
        await StealPower<StrengthPower>(target);
        await StealPower<ArtifactPower>(target);
        await StealPower<RitualPower>(target);
        await StealPower<PlatingPower>(target);
        await StealBlock(target, cardPlay);

        int debuffAmount = (int)DynamicVars["Debuff"].BaseValue;
        int mangleAmount = (int)DynamicVars["Mangle"].BaseValue;
        await PowerCmd.Apply<VulnerablePower>(target, debuffAmount, Owner.Creature, this);
        await PowerCmd.Apply<WeakPower>(target, debuffAmount, Owner.Creature, this);
        await PowerCmd.Apply<DebilitatePower>(target, mangleAmount, Owner.Creature, this);
        MainFile.Logger.Info($"【夺舍】结算完成：目标={target.Name}，易伤/虚弱={debuffAmount}，摧残={mangleAmount}。");
    }

    /// <summary>
    /// 从目标身上夺取指定正层数能力。
    /// </summary>
    /// <typeparam name="TPower">要夺取的能力类型。</typeparam>
    /// <param name="target">目标敌人。</param>
    private async Task StealPower<TPower>(Creature target)
        where TPower : MegaCrit.Sts2.Core.Models.PowerModel
    {
        TPower? power = target.GetPower<TPower>();
        int amount = Math.Max(0, power?.Amount ?? 0);
        if (power == null || amount <= 0 || Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.ModifyAmount(power, -amount, Owner.Creature, this);
        await PowerCmd.Apply<TPower>(Owner.Creature, amount, Owner.Creature, this);
        MainFile.Logger.Info($"【夺舍】夺取能力：{power.Id.Entry}，层数={amount}。");
    }

    /// <summary>
    /// 夺取目标当前护甲，并把等量护甲给自己。
    /// </summary>
    /// <param name="target">目标敌人。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    private async Task StealBlock(Creature target, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int blockAmount = Math.Max(0, target.Block);
        if (blockAmount <= 0)
        {
            return;
        }

        await CreatureCmd.LoseBlock(target, blockAmount);
        await CreatureCmd.GainBlock(Owner.Creature, blockAmount, default, cardPlay);
        MainFile.Logger.Info($"【夺舍】夺取目标护甲：{blockAmount}。");
    }
}
