using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 黑影敕令状态：回合开始时失去1力量、1敏捷、1HP。无法再获得力量。
/// </summary>
public class ShadowDecreePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 黑影敕令属于减益能力（对自身的代价）。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 黑影敕令不堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 回合开始时失去1力量、1敏捷、1HP。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner || Owner.IsDead)
        {
            return;
        }

        Flash();

        // 失去1力量
        await PowerCmd.Apply<StrengthPower>(Owner, -1, Owner, null);

        // 失去1敏捷
        await PowerCmd.Apply<DexterityPower>(Owner, -1, Owner, null);

        // 失去1HP
        await CreatureCmd.Damage(choiceContext, Owner, 1, ValueProp.Unpowered, Owner, null, null);
    }
}
