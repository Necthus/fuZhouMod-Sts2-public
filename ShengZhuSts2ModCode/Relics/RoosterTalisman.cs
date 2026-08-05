using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 鸡符咒：凌空虚渡，允许在地图上无视路径限制前往下一层任意房间，共10次使用机会。
/// 鼠符咒共鸣：回合结束时若手牌不为空，可自选保留手牌到下回合。
/// </summary>
public class RoosterTalisman : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 凌空虚渡总使用次数上限。
    /// </summary>
    private const int MaxFreeTravelUses = 10;

    /// <summary>
    /// 剩余凌空虚渡次数（通过SavedProperty持久化到存档，SL后不会重置）。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int RemainingUses
    {
        get => _remainingUses;
        set => _remainingUses = value;
    }

    private int _remainingUses = MaxFreeTravelUses;

    /// <summary>
    /// 是否还能为全队提供凌空虚渡。补丁直接扫描全队遗物时会用到，避免熔毁遗物继续生效。
    /// </summary>
    internal bool CanAllowTeamFreeTravel => _remainingUses > 0 && !IsMelted;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示剩余次数计数器。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值：剩余凌空虚渡次数。
    /// </summary>
    public override int DisplayAmount => _remainingUses;

    /// <summary>
    /// 凌空虚渡核心逻辑：当剩余次数大于0时，允许自由选择下一层任意节点。
    /// 游戏框架会在地图界面调用此方法判断是否解锁路径限制。
    /// </summary>
    public override bool ShouldAllowFreeTravel()
    {
        return CanAllowTeamFreeTravel;
    }

    /// <summary>
    /// 进入房间后判断是否使用了凌空虚渡（即选择了非正常连接的节点），若是则消耗次数。
    /// 判断逻辑：对比上一个位置的MapPoint.Children是否包含当前位置，不包含则说明用了自由移动。
    /// </summary>
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (_remainingUses <= 0 || Owner?.RunState == null)
        {
            return Task.CompletedTask;
        }

        // 判断本次移动是否使用了凌空虚渡
        if (!WasFreeTravelUsed())
        {
            return Task.CompletedTask;
        }

        _remainingUses--;
        InvokeDisplayAmountChanged();

        if (_remainingUses > 0)
        {
            MainFile.Logger.Info($"【鸡符咒】凌空虚渡：消耗1次，剩余{_remainingUses}次。");
        }
        else
        {
            MainFile.Logger.Info("【鸡符咒】凌空虚渡：已用尽全部次数，不再允许自由移动。");
            Status = RelicStatus.Disabled;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断本次地图移动是否使用了凌空虚渡（选择了非正常路径连接的节点）。
    /// 通过对比上一个位置的Children是否包含当前位置来判断。
    /// </summary>
    private bool WasFreeTravelUsed()
    {
        IRunState runState = Owner!.RunState;

        // 需要通过RunState具体类访问VisitedMapCoords
        if (runState is not RunState concreteRunState)
        {
            return false;
        }

        IReadOnlyList<MapCoord> visitedCoords = concreteRunState.VisitedMapCoords;

        // 至少需要2个已访问坐标才能判断（上一个位置 + 当前位置）
        if (visitedCoords.Count < 2)
        {
            return false;
        }

        MapCoord previousCoord = visitedCoords[visitedCoords.Count - 2];
        MapCoord currentCoord = visitedCoords[visitedCoords.Count - 1];

        // 获取上一个位置的MapPoint
        MapPoint? previousPoint = runState.Map?.GetPoint(previousCoord);
        if (previousPoint == null)
        {
            return false;
        }

        // 检查当前位置是否在上一个位置的正常Children中
        bool isNormalPath = previousPoint.Children.Any(child => child.coord.Equals(currentCoord));

        return !isNormalPath;
    }

    /// <summary>
    /// 鼠符咒共鸣：回合结束前让玩家自选要保留到下回合的手牌。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != CombatSide.Player || !HasRatResonance() || Owner?.PlayerCombatState == null)
        {
            return;
        }

        var hand = Owner.PlayerCombatState.Hand.Cards;
        if (hand.Count == 0)
        {
            return;
        }

        Flash();

        // 让玩家自选要保留的手牌（可选0~全部）
        var prefs = new CardSelectorPrefs(
            new LocString("relics", "SHENGZHUSTS2MOD-ROOSTER_TALISMAN.retainPrompt"),
            0,
            hand.Count)
        {
            Cancelable = false
        };

        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromHand(
            choiceContext, Owner, prefs, null!, null!)).ToList();

        if (selectedCards.Count == 0)
        {
            return;
        }

        // 给选中的手牌添加本回合保留标记，让它们真正留到下回合。
        foreach (CardModel card in selectedCards)
        {
            card.GiveSingleTurnRetain();
        }

        MainFile.Logger.Info($"【鸡符咒】鼠符咒共鸣：玩家选择保留{selectedCards.Count}张手牌到下回合。");
    }
}
