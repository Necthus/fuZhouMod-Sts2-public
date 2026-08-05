using System.Reflection;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 伊卡-武士团：造成伤害，可选消耗1张手牌获得额外效果。
/// 攻击牌→造成其伤害的一半；技能牌→抽2张；
/// 能力牌→随机施加1个负面效果（虚弱2/易伤2/力量-2 三选一）；
/// 诅咒牌→眩晕目标；状态牌→施加 1+当前回合数 中毒。
/// </summary>
public class YiKaSamurai : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费攻击，6(9)伤害。消耗。
    /// </summary>
    public YiKaSamurai() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage("Damage", 6, static (card, target) => ((YiKaSamurai)card).GetDamageBonus(target), default, upgrade: 3, bonusUpgrade: 0);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 武士团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetPrintedBaseValue(6, 3);

    /// <summary>
    /// 出牌时造成伤害，然后可选消耗1张手牌获得额外效果。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            await ShadowKhanAttack(choiceContext, cardPlay.Target);

            if (ShadowKhanToken.IsYiKaBlankMode(Owner))
            {
                MainFile.Logger.Info("【伊卡-武士团】黑影令牌2挡：伊卡作为白板打出，不烧牌。");
                return;
            }

            var playerCombatState = Owner?.PlayerCombatState;
            if (playerCombatState == null)
            {
                return;
            }

            var handCards = playerCombatState.Hand.Cards
                .Where(c => !ReferenceEquals(c, this))
                .ToList();

            if (handCards.Count == 0)
            {
                return;
            }

            CardModel? selected = ShadowKhanToken.IsManualExhaustMode(Owner)
                ? await SelectManualExhaustCard(choiceContext)
                : SelectRandomExhaustCard(handCards);

            if (selected == null)
            {
                return;
            }

            await CardCmd.Exhaust(choiceContext, selected);
            await ResolveExhaustedCardEffect(choiceContext, cardPlay.Target, selected);
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 手动挡：从手牌中选择1张牌消耗，可取消。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <returns>玩家选择的手牌；取消时返回null。</returns>
    private async Task<CardModel?> SelectManualExhaustCard(PlayerChoiceContext choiceContext)
    {
        // 使用FromHand在手牌区域直接选择，支持点击另一张牌直接切换选中
        var prefs = new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionScreenPrompt"), 0, 1);
        return (await CardSelectCmd.FromHand(
            choiceContext,
            Owner!,
            prefs,
            null,
            this)).FirstOrDefault();
    }

    /// <summary>
    /// 自动挡：从当前可消耗手牌中随机选择1张。
    /// </summary>
    /// <param name="handCards">可消耗手牌列表。</param>
    /// <returns>随机选中的手牌。</returns>
    private CardModel? SelectRandomExhaustCard(IReadOnlyList<CardModel> handCards)
    {
        if (handCards.Count == 0)
        {
            return null;
        }

        CardModel selected = handCards[StableRandomHelper.NextInt(Owner, handCards.Count)];
        MainFile.Logger.Info($"【伊卡-武士团】黑影令牌自动挡随机消耗：{selected.Id.Entry}");
        return selected;
    }

    /// <summary>
    /// 按被消耗卡牌的类型结算伊卡追加效果。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">出牌目标。</param>
    /// <param name="selected">被消耗的手牌。</param>
    /// <returns>异步任务。</returns>
    private async Task ResolveExhaustedCardEffect(PlayerChoiceContext choiceContext, Creature target, CardModel selected)
    {
        switch (selected.Type)
        {
            case CardType.Attack:
                int cardDamage = selected.DynamicVars.ContainsKey("Damage") ? (int)selected.DynamicVars["Damage"].BaseValue : 0;
                int halfDamage = cardDamage / 2;
                if (halfDamage > 0)
                {
                    await ShadowKhanAttack(choiceContext, target, halfDamage);
                }
                break;

            case CardType.Skill:
                await CardPileCmd.Draw(choiceContext, 2, Owner!);
                break;

            case CardType.Power:
                await ApplyRandomDebuff(choiceContext, target);
                break;

            case CardType.Curse:
            case CardType.Quest:
                await CreatureCmd.Stun(target, null);
                break;

            case CardType.Status:
                int poisonAmount = 1 + GetCurrentTurnNumber();
                await CommonActions.Apply<PoisonPower>(choiceContext, target, this, poisonAmount);
                break;

            default:
                // 保底：遇到未知类型的卡牌，获得1点最大生命值
                MainFile.Logger.Info($"【伊卡-武士团】消耗了未知类型的卡牌：类型={selected.Type}，名称={selected.Id.Entry}，ID={selected.Id}");
                await CreatureCmd.GainMaxHp(Owner!.Creature!, 1);
                break;
        }
    }

    /// <summary>
    /// 随机施加1个负面效果：虚弱2/易伤2/力量-2 三选一。
    /// </summary>
    private async Task ApplyRandomDebuff(PlayerChoiceContext choiceContext, Creature target)
    {
        int choice = StableRandomHelper.NextInt(Owner, 3);

        switch (choice)
        {
            case 0:
                await CommonActions.Apply<WeakPower>(choiceContext, target, this, 2);
                break;
            case 1:
                await CommonActions.Apply<VulnerablePower>(choiceContext, target, this, 2);
                break;
            case 2:
                await PowerCmd.Apply<StrengthPower>(target, -2, Owner!.Creature, this);
                break;
        }
    }

    /// <summary>
    /// 获取当前回合数（简化实现）。
    /// </summary>
    private int GetCurrentTurnNumber()
    {
        var combatState = Owner?.Creature?.CombatState;
        if (combatState == null) return 1;

        try
        {
            var prop = combatState.GetType().GetProperty("TurnNumber")
                       ?? combatState.GetType().GetProperty("TurnCount")
                       ?? combatState.GetType().GetProperty("Turn");
            if (prop != null)
            {
                var value = prop.GetValue(combatState);
                if (value is int intVal) return intVal;
            }
        }
        catch
        {
            // 忽略反射异常
        }

        return 1;
    }

    /// <summary>
    /// 计算手牌展示用的武士团伤害。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        return GetDisplayedShadowKhanDamageBonus(PrintedBaseDamage, 0, target);
    }
}
