using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 合作遗物：持有后战斗奖励额外出现一张龙小组卡牌，并允许龙小组卡牌进入七十二变候选。
/// </summary>
public class CollaborationRelic : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 合作是一代事件专属遗物，二代用 None 表示不进普通遗物池。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.None;

    /// <summary>
    /// 在卡牌奖励生成早期阶段追加龙小组卡牌，确保后续遗物效果（亮片、蛋类等）能正常作用。
    /// 使用 TryModifyCardRewardOptions 而非 Postfix，让龙小组卡参与游戏的 Hook 处理流程。
    /// </summary>
    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> cardRewards, CardCreationOptions options)
    {
        // 只处理战斗奖励且是遗物持有者本人
        if (player != Owner || options.Source != CardCreationSource.Encounter)
        {
            return false;
        }

        // 获取已有卡牌类型，避免重复
        List<Type> existingTypes = cardRewards.Select(result => result.Card.GetType()).ToList();

        // 筛选出未出现的龙小组卡牌
        List<CardModel> candidates = ModelDb.AllCards
            .Where(TeamJackieCardHelper.IsTeamJackieCard)
            .Where(card => !existingTypes.Contains(card.GetType()))
            .ToList();

        // 如果所有龙小组卡都已出现，则重新从全部候选
        if (candidates.Count == 0)
        {
            candidates = ModelDb.AllCards
                .Where(TeamJackieCardHelper.IsTeamJackieCard)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            MainFile.Logger.Info("【合作】未找到可追加的龙小组卡牌。");
            return false;
        }

        // 使用稳定随机选择一张龙小组卡
        CardModel prototype = StableRandomHelper.PickByStableHash(
            player,
            candidates,
            "CollaborationRelic.TeamJackieCard",
            card => card.Id.Entry,
            cardRewards.Count.ToString()) ?? candidates[0];

        CardModel teamJackieCard = player.RunState.CreateCard(prototype, player);
        cardRewards.Add(new CardCreationResult(teamJackieCard));

        MainFile.Logger.Info($"【合作】战斗卡牌奖励追加龙小组卡牌：{teamJackieCard.Id.Entry}。");
        return true;
    }
}
