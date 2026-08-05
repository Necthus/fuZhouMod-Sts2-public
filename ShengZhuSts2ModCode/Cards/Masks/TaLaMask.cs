using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 塔拉面具：特殊面具，面具容量+2(3)，获得1层影噬，打出和回合开始时随机生成兵团牌。
/// 不走BaseMaskCard的统一流程，直接继承ShengZhuSts2ModCard。
/// </summary>
public class TaLaMask : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：2费能力牌，面具容量+2(升级+3)，升级后费用降为1。
    /// </summary>
    public TaLaMask() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Magic", 2, 1);
    }

    /// <summary>
    /// 升级时把基础费用从2降到1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时施加塔拉面具能力（增加面具容量）、立即随机获得1张兵团牌，并获得1层影噬。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var taLaPower = await CommonActions.ApplySelf<TaLaPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
        if (taLaPower != null)
        {
            if (IsUpgraded)
            {
                taLaPower.GenerateUpgraded = true;
            }

            int permanentCostReductionCount = MaskManager.ConsumeMaskCardPermanentCostReduction(this);
            await taLaPower.RecordMaskCast(IsUpgraded, permanentCostReductionCount);
            await taLaPower.AddRandomShadowKhanToHand(choiceContext, Owner, IsUpgraded);
            MainFile.Logger.Info($"【塔拉面具】打出完成：扩充栏位={(int)DynamicVars["Magic"].BaseValue}，升级版={IsUpgraded}，当前每回合随机兵团数={taLaPower.GetRandomShadowKhanCardsPerTurn()}");
        }

        await CommonActions.ApplySelf<DominionPower>(choiceContext, this, 1);
    }
}
