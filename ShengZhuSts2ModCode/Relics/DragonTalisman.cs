using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 龙符咒：每打出10张牌，对所有敌人造成15点伤害。
/// </summary>
public class DragonTalisman : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 每次触发时造成的基础伤害。
    /// </summary>
    private const int TriggerDamage = 15;

    /// <summary>
    /// 本场战斗累计打出的牌数。
    /// </summary>
    private int _cardsPlayedCount;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示计数器：当前打出牌数（对10取模）。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值。
    /// </summary>
    public override int DisplayAmount => _cardsPlayedCount % 10;

    /// <summary>
    /// 战斗开始时重置计数。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _cardsPlayedCount = 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每次出牌后计数+1，满10张时对全体敌人造成15点伤害。
    /// 鼠符咒共鸣：伤害额外加上当前力量值。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        _cardsPlayedCount++;
        InvokeDisplayAmountChanged();

        if (_cardsPlayedCount % 10 == 0)
        {
            Flash();
            // 基础伤害，鼠符咒共鸣时额外加力量值
            decimal baseDamage = TriggerDamage;
            if (HasRatResonance() && Owner?.Creature != null)
            {
                var strengthPower = Owner.Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
                if (strengthPower != null && strengthPower.Amount > 0)
                {
                    baseDamage += strengthPower.Amount;
                }
            }

            // 对所有敌人造成伤害
            IReadOnlyList<Creature> opponents = Owner?.Creature?.CombatState?.HittableEnemies?.ToList()
                ?? (IReadOnlyList<Creature>)Array.Empty<Creature>();

            foreach (Creature enemy in opponents)
            {
                await CreatureCmd.Damage(choiceContext, enemy, baseDamage, ValueProp.Unpowered, Owner?.Creature, null);
            }
        }
    }
}
