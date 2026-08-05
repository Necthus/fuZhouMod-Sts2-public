using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 面具牌基类：打出时施加对应面具Power，并立即生成一张对应黑影兵团牌到手牌。
/// 所有面具牌继承此类，只需实现 ApplyMaskPower 和 CreateShadowKhanCard 即可。
/// 集成 MaskManager 进行容量管理。
/// </summary>
public abstract class BaseMaskCard(int cost, CardRarity rarity, TargetType target = TargetType.Self) :
    ShengZhuSts2ModCard(cost, CardType.Power, rarity, target)
{
    /// <summary>
    /// 出牌时施加面具能力并生成一张兵团牌。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MaskManager.SetPendingMaskApplyContext(Owner?.Creature, choiceContext);
        BaseMaskPower? power;
        try
        {
            // 施加面具能力
            power = await ApplyMaskPower(choiceContext);
        }
        finally
        {
            MaskManager.ClearPendingMaskApplyContext(Owner?.Creature);
        }

        // 标记升级状态
        if (power != null && IsUpgraded)
        {
            power.GenerateUpgraded = true;
        }

        // 记录面具堆叠信息（用于挤出时返还正确的面具牌）
        if (power != null)
        {
            int magicValue = DynamicVars.ContainsKey("Magic") ? (int)DynamicVars["Magic"].BaseValue : 1;
            int permanentCostReductionCount = MaskManager.ConsumeMaskCardPermanentCostReduction(this);
            MaskManager.RecordMaskStacks(Owner, power.MaskPowerKey, magicValue, IsUpgraded, permanentCostReductionCount);
        }

        // 立即生成一张兵团牌到手牌
        await GenerateShadowKhanToHand(choiceContext);
    }

    /// <summary>
    /// 施加对应的面具能力，子类实现。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <returns>施加的面具能力实例。</returns>
    protected abstract Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext);

    /// <summary>
    /// 创建对应的黑影兵团牌实例，子类实现。
    /// </summary>
    /// <returns>新创建的兵团牌。</returns>
    protected abstract CardModel CreateShadowKhanCard();

    /// <summary>
    /// 生成一张兵团牌到手牌（满手时自动打出）。
    /// </summary>
    private async Task GenerateShadowKhanToHand(PlayerChoiceContext choiceContext)
    {
        var combatState = Owner?.Creature?.CombatState;
        var playerCombatState = Owner?.PlayerCombatState;
        if (combatState == null || playerCombatState == null)
        {
            return;
        }

        var shadowKhanCard = CreateShadowKhanCard();
        if (IsUpgraded)
        {
            CardCmd.Upgrade(shadowKhanCard, CardPreviewStyle.None);
        }

        if (playerCombatState.Hand.Cards.Count >= 10)
        {
            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(Owner, shadowKhanCard, "打出面具满手自动打出");
            await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Play, addedByPlayer: true);
            await CardCmd.AutoPlay(choiceContext, shadowKhanCard, null);
        }
        else
        {
            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(Owner, shadowKhanCard, "打出面具加入手牌");
            await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Hand, addedByPlayer: true);
        }
    }
}
