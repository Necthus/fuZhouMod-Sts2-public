using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 拘魂：2(1)费技能。选择消耗任意张手牌，
/// 每消耗奇数张获得 1 点力量，每消耗偶数张获得 1 点敏捷。消耗。
/// 与 1 代 Soultrap + SoultrapAction 行为一致：
/// 力量数 = (消耗张数 + 1) / 2，敏捷数 = 消耗张数 / 2。
/// </summary>
public class Soultrap : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：2 费技能，非普通稀有度，无目标。消耗。升级降费用 1。
    /// </summary>
    public Soultrap() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时把基础费用从 2 降到 1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时让玩家从手牌中选择任意张消耗，按消耗数量交替结算力量与敏捷。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (playerCombatState == null || Owner?.Creature == null)
        {
            return;
        }

        // 排除拘魂自身后判断手牌是否为空
        var selectableCards = playerCombatState.Hand.Cards
            .Where(c => !ReferenceEquals(c, this))
            .ToList();
        if (selectableCards.Count == 0)
        {
            MainFile.Logger.Info("【拘魂】手牌为空（除自身外），直接结束。");
            return;
        }

        // 允许选 0 ~ 全部，参考 YiKaSamurai 的 FromHand 直选写法
        var prefs = new CardSelectorPrefs(
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            0,
            selectableCards.Count);
        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner!,
            prefs,
            null,
            this)).ToList();

        int count = selected.Count;
        if (count == 0)
        {
            MainFile.Logger.Info("【拘魂】玩家未选择任何手牌消耗。");
            return;
        }

        // 逐张消耗
        foreach (CardModel card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 力量与敏捷的计算与 1 代一致：奇数张+1力量，偶数张+1敏捷
        int strengthGain = (count + 1) / 2;
        int dexterityGain = count / 2;

        if (strengthGain > 0)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, strengthGain, Owner.Creature, this);
        }

        if (dexterityGain > 0)
        {
            await PowerCmd.Apply<DexterityPower>(Owner.Creature, dexterityGain, Owner.Creature, this);
        }

        MainFile.Logger.Info($"【拘魂】消耗张数={count}，获得力量={strengthGain}，获得敏捷={dexterityGain}，升级={IsUpgraded}");
    }
}
