using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

/// <summary>
/// 面具能力基类：回合开始按层数生成对应黑影兵团卡，并负责被挤出/被剥离时的返牌逻辑。
/// </summary>
public abstract class BaseMaskPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 记录该面具后续生成的黑影兵团卡是否为升级版。
    /// </summary>
    public bool GenerateUpgraded { get; set; }

    /// <summary>
    /// 面具能力属于增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 面具能力使用计数器样式显示层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 面具能力在管理器中的唯一键。
    /// </summary>
    public string MaskPowerKey => GetType().FullName ?? GetType().Name;

    /// <summary>
    /// 创建对应的黑影兵团卡。
    /// </summary>
    protected abstract CardModel CreateShadowKhanCard(Player player);

    /// <summary>
    /// 面具首次施加时注册到MaskManager，并由管理器按真实状态增量同步充能球。
    /// </summary>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner == null)
        {
            return;
        }

        var pendingChoiceContext = MaskManager.GetPendingMaskApplyContext(Owner);
        await MaskManager.OnMaskAppliedAsync(Owner, MaskPowerKey, this, pendingChoiceContext);
        MainFile.Logger.Info($"【面具充能球】刷新显示：{MaskPowerKey}，层数={Amount}");
    }

    /// <summary>
    /// 面具层数变化后，立即把最新层数同步到对应充能球，避免显示滞后到下次轨道刷新。
    /// </summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (Owner == null || !ReferenceEquals(power, this) || Amount <= 0)
        {
            return Task.CompletedTask;
        }

        var player = Owner.Player;
        if (player == null)
        {
            return Task.CompletedTask;
        }

        bool updated = MaskManager.TryUpdateMaskOrbAmount(player, MaskPowerKey, Amount, Id.Entry);
        MaskManager.UpdateMaskOrbs(player);
        if (updated)
        {
            MainFile.Logger.Info($"【面具充能球】叠层同步：{MaskPowerKey}，当前层数={Amount}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合开始时生成对应数量的黑影兵团卡；手牌已满则自动打出。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner || Owner.CombatState == null || Owner.IsDead)
        {
            return;
        }

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        Flash();

        for (int i = 0; i < Amount; i++)
        {
            var shadowKhanCard = CreateShadowKhanCard(player);
            if (GenerateUpgraded)
            {
                CardCmd.Upgrade(shadowKhanCard, CardPreviewStyle.None);
            }

            if (playerCombatState.Hand.Cards.Count >= 10)
            {
                BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, shadowKhanCard, "普通面具回合开始满手自动打出");
                await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Play, addedByPlayer: true);
                await CardCmd.AutoPlay(choiceContext, shadowKhanCard, null);
                continue;
            }

            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, shadowKhanCard, "普通面具回合开始加入手牌");
            await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Hand, addedByPlayer: true);
        }
    }

    /// <summary>
    /// 面具被挤出或被剥离时的返牌处理。
    /// returnedMaskCostReduction 为0表示普通挤出，为1表示剥离后返还的面具牌本场战斗费用额外-1。
    /// </summary>
    public async Task OnEvict(PlayerChoiceContext choiceContext, Player player, int returnedMaskCostReduction = 0)
    {
        MainFile.Logger.Info($"【面具能力】{MaskPowerKey} 被移除，层数={Amount}，返牌减费={returnedMaskCostReduction}");

        var records = MaskManager.GetMaskStackSnapshot(player, MaskPowerKey, Amount);
        var combatState = Owner?.CombatState;
        if (combatState == null)
        {
            return;
        }

        foreach (var record in records)
        {
            for (int i = 0; i < MaskManager.EvictSoldierRefundPerStack; i++)
            {
                var shadowKhanCard = CreateShadowKhanCard(player);
                if (record.Upgraded)
                {
                    CardCmd.Upgrade(shadowKhanCard, CardPreviewStyle.None);
                }

                await MaskManager.AddGeneratedShadowKhanCardWithFallback(choiceContext, player, shadowKhanCard);
            }
        }

        var returnedMaskCards = new List<CardModel>();
        foreach (var record in records)
        {
            var maskCard = MaskManager.CreateMaskCardByPowerKey(combatState, MaskPowerKey, player);
            if (maskCard == null)
            {
                continue;
            }

            if (record.Upgraded)
            {
                CardCmd.Upgrade(maskCard, CardPreviewStyle.None);
            }

            int totalReduction = Math.Max(0, record.PermanentCostReductionCount + returnedMaskCostReduction);
            MaskManager.ApplyPermanentPeelCostReduction(maskCard, MaskPowerKey, totalReduction);
            returnedMaskCards.Add(maskCard);
        }

        if (returnedMaskCards.Count > 0)
        {
            await MaskManager.AddGeneratedMaskCardsToDiscard(player, returnedMaskCards);
        }
    }
}
