using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 黑手帮·特鲁：伤害和格挡 = 最大生命值 × (8 + 主牌组黑手帮卡数 × 倍率)%。
/// 打出时获得格挡、造成伤害、施加 2 层黑手。
/// </summary>
public class BlackHandTohru : BlackHandGangCard
{
    /// <summary>
    /// 基础百分比（8%）。
    /// </summary>
    private const int BasePercentage = 8;

    // 构造卡牌数值：2 费，稀有攻击，指定单体敌人。
    public BlackHandTohru() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        // 使用 CalculatedDamage 让伤害数值在手牌中动态显示
        WithCalculatedDamage(0, static (card, _) => CalculateValue(card), upgrade: 0, bonusUpgrade: 0);
        // 使用 CalculatedBlock（自定义名称）让格挡数值在手牌中动态显示
        WithCalculatedBlock("Block", 0, static (card, _) => CalculateBlockValue(card), ValueProp.Move, upgrade: 0, bonusUpgrade: 0);
        // 当前百分比（基础8 + 黑手帮卡数 × 倍率），用于描述中展示公式
        WithCalculatedVar("Percent", BasePercentage, static (card, _) => CalculatePercentBonus(card), upgrade: 0, bonusUpgrade: 0);
        WithVar("BlackHandApply", 2);
        // 每张黑手帮牌贡献的百分比：未升级 1%，升级后 2%
        WithVar("PerCard", 1, 1);
    }

    /// <summary>
    /// 计算动态数值：最大生命值 × (基础百分比 + 主牌组黑手帮卡数 × 倍率) / 100。
    /// </summary>
    /// <param name="card">当前卡牌实例。</param>
    /// <returns>计算后的伤害/格挡数值。</returns>
    private static decimal CalculateValue(CardModel card)
    {
        if (card is not BlackHandTohru tohru)
        {
            return 0;
        }

        // 战斗中用 Creature.MaxHp，非战斗时用 Character.StartingHp 作为近似值
        int maxHp = tohru.Owner?.Creature?.MaxHp
                    ?? tohru.Owner?.Character?.StartingHp
                    ?? 0;
        int multiplier = tohru.IsUpgraded ? 2 : 1;
        int percentage = BasePercentage + tohru.CountBlackHandInMasterDeck() * multiplier;
        return maxHp * percentage / 100;
    }

    /// <summary>
    /// 计算动态格挡值：只计算特鲁自己的基础动态格挡，敏捷和虎符咒由普通格挡链统一处理。
    /// </summary>
    /// <param name="card">当前卡牌实例。</param>
    /// <returns>计算后的格挡数值。</returns>
    private static decimal CalculateBlockValue(CardModel card)
    {
        return CalculateValue(card);
    }

    /// <summary>
    /// 计算百分比加成部分（黑手帮卡数 × 倍率），用于描述中展示公式。
    /// </summary>
    /// <param name="card">当前卡牌实例。</param>
    /// <returns>百分比加成值。</returns>
    private static decimal CalculatePercentBonus(CardModel card)
    {
        if (card is not BlackHandTohru tohru)
        {
            return 0;
        }

        int multiplier = tohru.IsUpgraded ? 2 : 1;
        return tohru.CountBlackHandInMasterDeck() * multiplier;
    }

    // 出牌时先获得格挡，再造成伤害，最后施加黑手。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        // 使用框架的标准格挡和攻击流程，数值由 CalculatedDamage/CalculatedBlock 提供
        await CommonActions.CardBlock(this, cardPlay);
        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        // 施加 2 层黑手
        await ApplyBlackHand(choiceContext, cardPlay.Target, (int)DynamicVars["BlackHandApply"].BaseValue);

        await ResolveBountyRewards(choiceContext);
    }
}
