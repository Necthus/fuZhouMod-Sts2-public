using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 无尽黑暗：集齐10种面具后觉醒获得的特殊能力牌。
/// 0费，远古卡，打出时面具容量最大化并施加所有10种面具效果各1层。
/// 基础版和升级版均固有。
/// 升级后所有面具标记为升级版（生成升级版兵团牌）。
/// </summary>
public class EndlessDarkness : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：0费远古能力牌，基础版和升级版均固有。
    /// </summary>
    public EndlessDarkness() : base(0, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
        WithKeywords(CardKeyword.Innate);
    }

    /// <summary>
    /// 升级时保留固有词条，生成升级版兵团牌的逻辑在出牌时处理。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 出牌时：面具容量最大化，施加塔拉+9种普通面具各1层。
    /// 升级版：所有面具标记为升级版。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null || !Owner.Creature.IsAlive)
        {
            return;
        }
        MaskManager.BeginDeferredMaskOrbRefresh(Owner?.Creature);
        try
        {
            // 面具容量最大化至10
            await MaskManager.MaximizeMaskCapacityAsync(Owner);
            MainFile.Logger.Info("【无尽黑暗】面具容量已最大化至10。");

            // 施加塔拉面具能力+1层
            var taLaPower = await PowerCmd.Apply<TaLaPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
            if (taLaPower != null)
            {
                if (IsUpgraded)
                {
                    taLaPower.GenerateUpgraded = true;
                }

                await taLaPower.RecordMaskCast(IsUpgraded, 0);
                MainFile.Logger.Info($"【无尽黑暗】施加塔拉面具+1层，升级版={IsUpgraded}");
            }

            // 施加9种普通面具各1层
            await ApplyMaskPower<NiJiaPower>(choiceContext);
            await ApplyMaskPower<LaZuoPower>(choiceContext);
            await ApplyMaskPower<SaMoPower>(choiceContext);
            await ApplyMaskPower<BaTePower>(choiceContext);
            await ApplyMaskPower<KaBoPower>(choiceContext);
            await ApplyMaskPower<LeiSuPower>(choiceContext);
            await ApplyMaskPower<ManNiPower>(choiceContext);
            await ApplyMaskPower<MingTaPower>(choiceContext);
            await ApplyMaskPower<YiKaPower>(choiceContext);

            // 施加1层影噬
            await PowerCmd.Apply<DominionPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);

            // 施加无尽黑暗持续能力：回合开始时按消耗堆黑影兵团数量获得影噬
            await PowerCmd.Apply<EndlessDarknessPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);

            MainFile.Logger.Info("【无尽黑暗】所有面具施加完成。");
        }
        finally
        {
            await MaskManager.EndDeferredMaskOrbRefreshAsync(Owner);
        }
    }

    /// <summary>
    /// 施加指定面具能力+1层，并记录堆叠信息。
    /// 升级版时标记面具为升级版。
    /// </summary>
    private async Task ApplyMaskPower<T>(PlayerChoiceContext choiceContext) where T : BaseMaskPower
    {
        MaskManager.SetPendingMaskApplyContext(Owner?.Creature, choiceContext);
        BaseMaskPower? power;
        try
        {
            power = await PowerCmd.Apply<T>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        }
        finally
        {
            MaskManager.ClearPendingMaskApplyContext(Owner?.Creature);
        }

        if (power == null)
        {
            return;
        }

        if (IsUpgraded)
        {
            power.GenerateUpgraded = true;
        }

        MaskManager.RecordMaskStacks(Owner, power.MaskPowerKey, 1, IsUpgraded, 0);
    }
}
