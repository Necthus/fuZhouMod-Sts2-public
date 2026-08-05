using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 黑影护卫能力：尼嘉-忍者团造成伤害后，为拥有者获得等量护甲。
/// </summary>
public class ShadowGuardPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 是否把被格挡吸收的伤害也转化为护甲。
    /// </summary>
    private bool _includeBlockedDamage;

    /// <summary>
    /// 黑影护卫属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示叠加层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 根据是否为升级版切换能力说明。
    /// </summary>
    protected override string SmartDescriptionLocKey => _includeBlockedDamage
        ? Id.Entry + ".smartDescription_upgraded"
        : base.SmartDescriptionLocKey;

    /// <summary>
    /// 升级版黑影护卫开启被格挡伤害计入。
    /// </summary>
    /// <param name="includeBlockedDamage">是否启用升级效果。</param>
    public void EnableBlockedDamageIfNeeded(bool includeBlockedDamage)
    {
        _includeBlockedDamage = _includeBlockedDamage || includeBlockedDamage;
    }

    /// <summary>
    /// 监听尼嘉-忍者团造成的伤害并转化为护甲。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="result">伤害结果。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="target">伤害目标。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (Owner == null || dealer != Owner || cardSource is not NiJiaNinja || target == Owner)
        {
            return;
        }

        int blockGain = result.UnblockedDamage;
        if (_includeBlockedDamage)
        {
            blockGain += result.BlockedDamage;
        }

        blockGain *= Math.Max(1, Amount);
        if (blockGain <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.GainBlock(Owner, blockGain, default, null);
        MainFile.Logger.Info($"【黑影护卫】忍者团伤害转护甲：生命伤害={result.UnblockedDamage}，被格挡={result.BlockedDamage}，获得护甲={blockGain}，升级效果={_includeBlockedDamage}。");
    }
}
