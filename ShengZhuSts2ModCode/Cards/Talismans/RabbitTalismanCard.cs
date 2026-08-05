using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 卯兔：3费稀有技能牌。保留手牌结束回合，跳过敌人的下一个回合，开启新回合。消耗。升级后费用变2。
/// 实现方式：保留所有手牌 + 结束玩家回合 + 在敌方回合开始结算后把敌人当前行动替换为空行动。
/// </summary>
public class RabbitTalismanCard : TalismanCard
{
    /// <summary>
    /// 卯兔基础费用。
    /// </summary>
    private const int BaseCost = 3;

    /// <summary>
    /// 卯兔用于临时吃掉怪物行动的空行动ID。
    /// 战斗历史补丁会识别该ID，避免把空行动写入历史记录。
    /// </summary>
    public const string SkipMoveId = EnemyIntentControlHelper.RabbitSkipMoveId;

    /// <summary>
    /// 构造卡牌数值：3费，技能牌，稀有，消耗。
    /// </summary>
    public RabbitTalismanCard() : base(BaseCost, CardType.Skill, CardRarity.Rare, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时费用从3降到2。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 通过费用计算钩子临时修正卯兔费用，避免本地 UI 可打出检查把费用写进联机状态。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || !HasTalismanRelic<RabbitTalisman>())
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return modifiedCost != originalCost;
    }

    /// <summary>
    /// 出牌时：保留所有手牌到下回合，结束当前玩家回合，并在敌方回合开始后跳过敌人实际行动。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (playerCombatState == null || Owner?.Creature == null)
        {
            return;
        }

        var handCards = playerCombatState.Hand.Cards.ToList();
        foreach (CardModel card in handCards)
        {
            card.GiveSingleTurnRetain();
        }

        MainFile.Logger.Info($"【卯兔】保留{handCards.Count}张手牌到下回合。");

        // 结束玩家回合，并把跳过敌人行动的逻辑安排到敌方回合开始结算之后执行。
        var player = Owner;
        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        RabbitTurnSkipContext.Begin(player, combatState);
        RabbitEnemyTurnSkipService.Register(player, combatState);
        PlayerCmd.EndTurn(player, canBackOut: false);

        MainFile.Logger.Info($"【卯兔】已结束玩家回合，并登记敌方回合开始后跳过所有敌人的当前行动：玩家={player.NetId}。");
        await Task.CompletedTask;
    }

}
