using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹 Boss 本体：负责基础生命、行动状态机和后续战斗钩子入口。
/// </summary>
public class GrandMageDad : CustomMonsterModel
{
    private const int BaseHp = 500;

    /// <summary>
    /// 大法师老爹单人基础最小生命；多人缩放交给塔2原生逻辑处理。
    /// </summary>
    public override int MinInitialHp => BaseHp;

    /// <summary>
    /// 大法师老爹单人基础最大生命；第一版固定生命，后续再接动态生命来源。
    /// </summary>
    public override int MaxInitialHp => BaseHp;

    /// <summary>
    /// 生成老爹第一版固定行动循环。
    /// </summary>
    /// <returns>老爹行动状态机。</returns>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        return GrandMageDadMoveService.CreateStateMachine(this);
    }

    /// <summary>
    /// 创建大法师老爹自定义视觉，使用一代迁移来的待机序列帧。
    /// </summary>
    /// <returns>大法师老爹战斗视觉节点。</returns>
    public override NCreatureVisuals? CreateCustomVisuals()
    {
        return GrandMageDadVisualService.CreateVisuals();
    }

    /// <summary>
    /// 老爹进房后立刻挂载核心能力，避免要等到第一回合出手才出现 Buff。
    /// </summary>
    /// <returns>异步任务。</returns>
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await GrandMageDadMoveService.EnsureOpeningPowers(this);
    }
}
