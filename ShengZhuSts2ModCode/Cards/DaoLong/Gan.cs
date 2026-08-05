using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 甘：刀龙黑暗杀手攻击牌，拥有会随使用次数成长的暴击率。
/// </summary>
public class Gan : ShengZhuSts2ModCard
{
    /// <summary>
    /// 基础暴击率。
    /// </summary>
    private const int BaseCritChance = 30;

    /// <summary>
    /// 升级后额外暴击率。
    /// </summary>
    private const int UpgradeCritChance = 10;

    /// <summary>
    /// 每层甘能力提供的暴击率。
    /// </summary>
    private const int CritGrowthPerStack = 10;

    /// <summary>
    /// 构造甘卡牌数值。
    /// </summary>
    public Gan() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithDamage(8, 4);
    }

    /// <summary>
    /// 出牌时按当前暴击率判定，暴击则造成 150% 伤害，并让后续甘暴击率提升。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || cardPlay.Target == null)
        {
            return;
        }

        int critChance = CalculateCritChance();
        int roll = StableRandomHelper.NextIntInclusive(Owner, 1, 100);
        bool isCrit = roll <= critChance;
        decimal damage = DynamicVars.Damage.BaseValue;
        if (isCrit)
        {
            damage = Math.Floor(damage * 1.5m);
        }

        MainFile.Logger.Info($"【甘】暴击判定：当前暴击率={critChance}%，随机数={roll}，是否暴击={isCrit}，伤害={damage}");
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, damage, ValueProp.Move, Owner.Creature, this);
        await PowerCmd.Apply<GanPower>(Owner.Creature, 1, Owner.Creature, this);
    }

    /// <summary>
    /// 给描述文本补充当前暴击率变量。
    /// </summary>
    /// <param name="description">卡牌描述本地化对象。</param>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("CritChance", CalculateCritChance());
    }

    /// <summary>
    /// 计算当前甘暴击率，上限 100%。
    /// </summary>
    /// <returns>当前暴击率百分比。</returns>
    private int CalculateCritChance()
    {
        // 卡牌库展示的是规范模型，不能读取 Owner；这里只展示基础暴击率。
        int powerStacks = IsCanonical ? 0 : Owner?.Creature?.GetPower<GanPower>()?.Amount ?? 0;
        int upgradeBonus = IsUpgraded ? UpgradeCritChance : 0;
        return Math.Min(100, BaseCritChance + upgradeBonus + powerStacks * CritGrowthPerStack);
    }
}
