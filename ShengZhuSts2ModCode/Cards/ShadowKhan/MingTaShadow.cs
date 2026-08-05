using System.Reflection;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 明塔-噬影团：消耗手中1张攻击牌，造成基础伤害+影噬+历史消耗攻击牌伤害合计。
/// 若手中没有其他攻击牌，直接造成基础伤害+影噬+历史累计伤害。
/// </summary>
public class MingTaShadow : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费攻击，2(5)伤害。消耗。
    /// </summary>
    public MingTaShadow() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage("Damage", 2, static (card, target) => ((MingTaShadow)card).GetDamageBonus(target), default, upgrade: 3, bonusUpgrade: 0);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 噬影团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetPrintedBaseValue(2, 3);

    /// <summary>
    /// 出牌时从手牌中选择1张攻击牌消耗，然后造成基础伤害+影噬+历史消耗攻击牌伤害合计。
    /// 1代逻辑：消耗的攻击牌伤害在下次使用噬影团时才生效（先造成伤害，再累加本次消耗的伤害）。
    /// 若手中没有其他攻击牌，直接造成伤害（baseDamage + 历史累计）。
    /// 若只有1张攻击牌，自动选择消耗。
    /// 若有多张，让玩家选择1张消耗。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            var playerCombatState = Owner?.PlayerCombatState;
            if (playerCombatState == null)
            {
                return;
            }

            var attackCards = playerCombatState.Hand.Cards
                .Where(c => c.Type == CardType.Attack && !ReferenceEquals(c, this))
                .ToList();

            int consumedCardDamage = 0;

            if (attackCards.Count == 0)
            {
                MainFile.Logger.Info("【明塔噬影团】手中无其他攻击牌，直接造成伤害。");
            }
            else if (attackCards.Count == 1)
            {
                var card = attackCards[0];
                consumedCardDamage = GetCardFaceDamage(card);
                MainFile.Logger.Info($"【明塔噬影团】自动消耗攻击牌，本次记录伤害={consumedCardDamage}");
                await CardCmd.Exhaust(choiceContext, card);
            }
            else
            {
                CardModel? selected = ShadowKhanToken.IsManualExhaustMode(Owner)
                    ? await SelectManualAttackCard(choiceContext, attackCards)
                    : SelectRandomAttackCard(attackCards);

                if (selected != null)
                {
                    consumedCardDamage = GetCardFaceDamage(selected);
                    MainFile.Logger.Info($"【明塔噬影团】消耗攻击牌，本次记录伤害={consumedCardDamage}");
                    await CardCmd.Exhaust(choiceContext, selected);
                }
            }

            int baseAndDominionDamage = CalculateShadowKhanDamage();
            int totalDamage = baseAndDominionDamage + ShadowKhanHelper.GetMingTaTotalDamage();
            MainFile.Logger.Info($"【明塔噬影团】造成伤害={totalDamage}（基础+影噬={baseAndDominionDamage}，历史累计={ShadowKhanHelper.GetMingTaTotalDamage()}）");
            await ShadowKhanAttack(choiceContext, cardPlay.Target, totalDamage);

            if (consumedCardDamage > 0)
            {
                ShadowKhanHelper.AddMingTaDamage(consumedCardDamage);
                MainFile.Logger.Info($"【明塔噬影团】伤害累加完成，新累计={ShadowKhanHelper.GetMingTaTotalDamage()}");
            }
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 手动挡：从可消耗攻击牌中选择1张。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="attackCards">可消耗攻击牌列表。</param>
    /// <returns>玩家选择的攻击牌。</returns>
    private async Task<CardModel?> SelectManualAttackCard(PlayerChoiceContext choiceContext, IReadOnlyList<CardModel> attackCards)
    {
        var prefs = new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionScreenPrompt"), 1, 1);
        return (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            attackCards,
            Owner!,
            prefs)).FirstOrDefault();
    }

    /// <summary>
    /// 自动挡：从可消耗攻击牌中随机选择1张。
    /// </summary>
    /// <param name="attackCards">可消耗攻击牌列表。</param>
    /// <returns>随机选中的攻击牌。</returns>
    private CardModel? SelectRandomAttackCard(IReadOnlyList<CardModel> attackCards)
    {
        if (attackCards.Count == 0)
        {
            return null;
        }

        CardModel selected = attackCards[StableRandomHelper.NextInt(Owner, attackCards.Count)];
        MainFile.Logger.Info($"【明塔噬影团】黑影令牌自动挡随机消耗攻击牌：{selected.Id.Entry}");
        return selected;
    }

    /// <summary>
    /// 计算影噬加成 + 历史消耗攻击牌伤害合计。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        int totalDamage = CalculateShadowKhanDamage() + ShadowKhanHelper.GetMingTaTotalDamage();
        return CalculateDisplayedShadowKhanDamage(totalDamage, target) - PrintedBaseDamage;
    }

    /// <summary>
    /// 读取被消耗攻击牌印在卡面上的基础伤害，避免吃到虚弱、力量或遗物的临时修正。
    /// </summary>
    private static int GetCardFaceDamage(CardModel card)
    {
        if (card is BaseShadowKhanCard shadowKhanCard)
        {
            return Math.Max(0, shadowKhanCard.PrintedBaseDamage);
        }

        if (TryReadIntMember(card, new[] { "BaseDamage", "baseDamage", "PrintedBaseDamage", "RawBaseDamage" }, out int cardBaseDamage))
        {
            return Math.Max(0, cardBaseDamage);
        }

        if (!card.DynamicVars.ContainsKey("Damage"))
        {
            return 0;
        }

        object damageVar = card.DynamicVars["Damage"];
        if (TryReadIntMember(damageVar, new[] { "UnmodifiedBaseValue", "OriginalBaseValue", "PrintedBaseValue", "RawBaseValue" }, out int damageVarBase))
        {
            return Math.Max(0, damageVarBase);
        }

        return Math.Max(0, (int)card.DynamicVars["Damage"].BaseValue);
    }

    /// <summary>
    /// 尝试从对象的属性或字段中读取整数值。
    /// </summary>
    private static bool TryReadIntMember(object target, string[] candidateNames, out int value)
    {
        value = 0;
        Type type = target.GetType();

        foreach (string name in candidateNames)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(target) is int intValue)
            {
                value = intValue;
                return true;
            }

            if (property?.GetValue(target) is decimal decimalValue)
            {
                value = (int)decimalValue;
                return true;
            }

            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(target) is int fieldIntValue)
            {
                value = fieldIntValue;
                return true;
            }

            if (field?.GetValue(target) is decimal fieldDecimalValue)
            {
                value = (int)fieldDecimalValue;
                return true;
            }
        }

        return false;
    }
}
