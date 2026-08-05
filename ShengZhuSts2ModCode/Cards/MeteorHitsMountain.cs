using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 流星连打山：1费罕见攻击牌，造成2点伤害打3次。若阿福连招激活，下次攻击伤害+4。
/// </summary>
public class MeteorHitsMountain : AhFuCard
{
    // 构造卡牌数值：1 费，造成 2 点伤害（升级+1），打 3 次；连招激活时获得活力（下次攻击+4，升级+2）。
    public MeteorHitsMountain() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(2, 1);
        WithVar("Magic", 3);
        WithVar("Bonus", 4, 2);
    }

    // 出牌时对目标连续攻击，若阿福连招激活则获得活力（VigorPower）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        int hitCount = (int)DynamicVars["Magic"].BaseValue;

        // 连续攻击指定次数
        for (int i = 0; i < hitCount; i++)
        {
            await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        }

        // 阿福连招激活：获得活力（VigorPower），下次攻击伤害增加
        if (IsAfuComboActive())
        {
            int vigorAmount = (int)DynamicVars["Bonus"].BaseValue;
            // TODO: 若 VigorPower 命名空间有变化，需要调整此处
            await PowerCmd.Apply<VigorPower>(Owner.Creature, vigorAmount, Owner.Creature, this);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
