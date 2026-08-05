using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 恶魔法典：让黑影兵团牌重新吃力量、敏捷、易伤、虚弱、脆弱等正常修正，并增加面具容量。
/// </summary>
public class DemonCodex : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：2费金色能力，升级后1费。
    /// </summary>
    public DemonCodex() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时费用从2降到1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得恶魔法典能力，并增加 2 点本场面具容量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DemonCodexPower>(Owner.Creature, 1, Owner.Creature, this);
        MaskManager.AddExtraMaskCapacity(Owner, 2);
        await MaskManager.EnsureMaskCapacitySlotsAsync(Owner);
        MainFile.Logger.Info("【恶魔法典】黑影兵团开始参与力量、敏捷和常规攻防减益修正，面具容量+2。");
    }
}
