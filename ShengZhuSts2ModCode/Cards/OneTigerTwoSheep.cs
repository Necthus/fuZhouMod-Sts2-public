using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 一虎杀两羊：1费普通攻击牌，造成5点伤害打2次。若本回合打出过阿福牌则额外打1次。击杀敌人获得力量。
/// </summary>
public class OneTigerTwoSheep : AhFuCard
{
    // 构造卡牌数值：1 费，造成 5 点伤害（升级+2），打 2 次；击杀获得 1 点力量（升级+1）。
    public OneTigerTwoSheep() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithVar("Magic", 1, 1);
    }

    // 出牌时对目标连续攻击2次；若本回合打出过阿福牌则额外攻击1次；若目标被击杀则获得力量。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        bool afuComboActive = IsAfuComboActive();
        int hitCount = afuComboActive ? 3 : 2;
        MainFile.Logger.Info($"【一虎杀两羊】结算攻击次数：阿福连招={afuComboActive}，攻击次数={hitCount}。");

        // 使用原生多段攻击语义，避免被识别成重新打出卡牌。
        await CommonActions.CardAttack(this, cardPlay, hitCount).Execute(choiceContext);

        // 击杀判定：目标死亡则获得力量
        if (cardPlay.Target.IsDead)
        {
            int strengthGain = (int)DynamicVars["Magic"].BaseValue;
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, strengthGain, Owner.Creature, this);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
