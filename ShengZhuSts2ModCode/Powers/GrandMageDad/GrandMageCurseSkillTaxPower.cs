using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 布莱克：当前手牌里未支付的布莱克会让下一张技能牌费用增加。
/// </summary>
public class GrandMageCurseSkillTaxPower : GrandMageCurseTaxPower
{
    /// <summary>
    /// 只影响技能牌。
    /// </summary>
    protected override CardType AffectedType => CardType.Skill;

    /// <summary>
    /// 本 Buff 对应的诅咒类型。
    /// </summary>
    protected override Type SourceCurseType => typeof(CurseBlack);

    /// <summary>
    /// 来源名称，用于日志。
    /// </summary>
    protected override string SourceName => "布莱克";

    /// <summary>
    /// 施加或刷新布莱克费用提示。
    /// </summary>
    /// <param name="target">玩家生物。</param>
    /// <param name="sourceCard">来源诅咒牌。</param>
    /// <returns>异步任务。</returns>
    public static Task ApplyOrRefresh(Creature target, CardModel sourceCard)
    {
        return ApplyOrRefresh<GrandMageCurseSkillTaxPower>(target, sourceCard);
    }
}
