using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 大法师老爹的吟唱护盾：只负责标记护盾值和展示，后续处决逻辑由怪物行为控制。
/// </summary>
public class GrandMageChantShieldPower : ShengZhuSts2ModPower
{
    private bool _isRemoving;

    /// <summary>
    /// 吟唱护盾属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示护盾剩余值。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 初次获得能力时记日志。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshEncounterState(true);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【吟唱护盾】生效：护盾值={Amount}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 老爹受到伤害后检查格挡是否已经被打穿，被打穿则终止吟唱。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">受击目标。</param>
    /// <param name="result">伤害结果。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_isRemoving || Owner == null || target != Owner || Owner.Block > 0)
        {
            return;
        }

        _isRemoving = true;
        try
        {
            RefreshEncounterState(false);
            ShengZhuLogHelper.VerboseCombatInfo(() => "【大法师老爹】【吟唱护盾】护盾已被打破，终极封印处决将失效。");
            await PowerCmd.Remove(this);
        }
        finally
        {
            _isRemoving = false;
        }
    }

    /// <summary>
    /// 能力被移除时清理遭遇里的吟唱状态。
    /// </summary>
    /// <param name="oldOwner">移除前的持有者。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterRemoved(Creature oldOwner)
    {
        RefreshEncounterState(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 同步遭遇里的吟唱状态，供处决行动和存档读取。
    /// </summary>
    /// <param name="isChanting">是否仍在吟唱。</param>
    private void RefreshEncounterState(bool isChanting)
    {
        if (Owner?.CombatState?.Encounter is not GrandMageDadEncounter encounter)
        {
            return;
        }

        encounter.State.IsChanting = isChanting;
        encounter.State.ChantShieldAmount = isChanting ? Math.Max(Amount, Owner.Block) : 0;
    }
}
