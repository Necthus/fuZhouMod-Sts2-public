using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 苦涩的抉择：黑手帮爆发技能，立即获得资源，下回合全部卡临时加费。
/// </summary>
public class BlackHandBitterChoice : BlackHandGangCard
{
    /// <summary>
    /// 打出此牌需要失去的生命值。
    /// </summary>
    private const int HpLoss = 3;

    /// <summary>
    /// 构造卡牌数值：1费蓝色技能，消耗，失去3点生命，获得3(4)能量和抽牌，临时力量敏捷2(3)。
    /// </summary>
    public BlackHandBitterChoice() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("HpLoss", HpLoss);
        WithVar("Magic", 3, 1);
        WithVar("Stats", 2, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时先失去生命，再获得能量、抽牌、临时力量敏捷，并安排下回合费用惩罚。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int resourceAmount = (int)DynamicVars["Magic"].BaseValue;
        int statsAmount = (int)DynamicVars["Stats"].BaseValue;
        int hpLoss = (int)DynamicVars["HpLoss"].BaseValue;
        await CreatureCmd.Damage(choiceContext, Owner.Creature, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, cardPlay);
        if (!Owner.Creature.IsAlive || Owner.Creature.IsDead)
        {
            MainFile.Logger.Info($"【苦涩的抉择】支付生命代价后玩家死亡，停止后续效果：失去生命={hpLoss}。");
            return;
        }

        await PlayerCmd.GainEnergy(resourceAmount, Owner);
        await CardPileCmd.Draw(choiceContext, resourceAmount, Owner);
        await PowerCmd.Apply<LameGooseTemporaryStrengthPower>(Owner.Creature, statsAmount, Owner.Creature, this);
        await PowerCmd.Apply<TemporaryDexterityPower>(Owner.Creature, statsAmount, Owner.Creature, this);
        await PowerCmd.Apply<BitterChoiceNextTurnCostPower>(Owner.Creature, 1, Owner.Creature, this);
        await ResolveBountyRewards(choiceContext);
        MainFile.Logger.Info($"【苦涩的抉择】失去生命={hpLoss}，获得能量/抽牌={resourceAmount}，临时力量敏捷={statsAmount}，下回合费用+1。");
    }
}
