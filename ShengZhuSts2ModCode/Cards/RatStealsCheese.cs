using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 老鼠偷奶酪：0费普通技能牌，获得1点能量（升级后2点），将1张晕眩加入弃牌堆。
/// </summary>
public class RatStealsCheese : AhFuCard
{
    // 构造卡牌数值：0 费，获得 1 点能量（升级+1），副作用是加入 1 张晕眩到弃牌堆。
    public RatStealsCheese() : base(0, CardType.Skill, CardRarity.Common, TargetType.None)
    {
        WithVar("Magic", 1, 1);
    }

    // 出牌时获得能量，并将一张晕眩状态牌加入弃牌堆。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得能量
        int energyGain = (int)DynamicVars["Magic"].BaseValue;
        await PlayerCmd.GainEnergy(energyGain, Owner);

        // 将 1 张晕眩（Dazed）加入弃牌堆
        await CardPileCmdHelper.AddToCombatAndPreview<MegaCrit.Sts2.Core.Models.Cards.Dazed>(Owner.Creature, PileType.Discard, 1, true, CardPilePosition.Top);

        await ResolveBountyRewards(choiceContext);
    }
}
