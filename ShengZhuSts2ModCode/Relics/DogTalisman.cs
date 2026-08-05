using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 狗符咒：每场战斗限1次，受到致命伤害时生命值保留在25%而非死亡；若本场未触发免死，战斗结束后获得最大生命。
/// </summary>
public class DogTalisman : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 未触发免死并赢得战斗后获得的最大生命值。
    /// </summary>
    private const int VictoryMaxHpGain = 1;

    /// <summary>
    /// 免死触发后获得的护甲值。
    /// </summary>
    private const int DeathSaveBlock = 999;

    /// <summary>
    /// 本场战斗是否已触发过。
    /// </summary>
    private bool _usedThisCombat;

    /// <summary>
    /// 标记免死后需要在伤害结算完毕时补发护甲。
    /// </summary>
    private bool _needsDeathSaveBlock;

    /// <summary>
    /// 本场战斗是否存在戌狗卡牌 Buff 的未触发最大生命奖励候选。
    /// </summary>
    private bool _dogCardPowerRewardCandidate;

    /// <summary>
    /// 本场战斗戌狗卡牌 Buff 是否已经触发免死。
    /// </summary>
    private bool _dogCardPowerTriggered;

    /// <summary>
    /// 本场战斗戌狗卡牌 Buff 的未触发最大生命奖励是否已经发放。
    /// </summary>
    private bool _dogCardPowerRewardGranted;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 狗符咒不显示计数器，可用时用激活态闪烁，使用后用置灰态提示。
    /// </summary>
    public override bool ShowCounter => false;

    /// <summary>
    /// 计数器显示值。狗符咒不显示计数器，固定返回0。
    /// </summary>
    public override int DisplayAmount => 0;

    /// <summary>
    /// 战斗开始时重置触发标记。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        _needsDeathSaveBlock = false;
        _dogCardPowerRewardCandidate = false;
        _dogCardPowerTriggered = false;
        _dogCardPowerRewardGranted = false;
        UpdateTalismanVisualState(true, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 登记本场存在戌狗卡牌 Buff 的未触发最大生命奖励候选，用于战斗胜利时兜底结算。
    /// </summary>
    /// <param name="reason">登记原因。</param>
    internal void RegisterDogCardPowerRewardCandidate(string reason)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (!_dogCardPowerRewardCandidate)
        {
            MainFile.Logger.Info($"【联机同步】【戌狗】登记卡牌Buff未触发奖励候选：玩家={Owner.NetId}，原因={reason}。");
        }

        _dogCardPowerRewardCandidate = true;
    }

    /// <summary>
    /// 标记戌狗卡牌 Buff 已经触发免死，战后不再发放该 Buff 的未触发奖励。
    /// </summary>
    internal void MarkDogCardPowerTriggered()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (!_dogCardPowerTriggered)
        {
            MainFile.Logger.Info($"【联机同步】【戌狗】卡牌Buff已触发免死，取消未触发奖励：玩家={Owner.NetId}。");
        }

        _dogCardPowerTriggered = true;
    }

    /// <summary>
    /// 尝试发放戌狗卡牌 Buff 未触发时的最大生命奖励，避免 Power 战斗结束钩子漏跑。
    /// </summary>
    /// <param name="reason">发放原因。</param>
    /// <returns>实际发放奖励时返回 true。</returns>
    internal async Task<bool> TryGrantDogCardPowerUntriggeredMaxHpReward(string reason)
    {
        if (!_dogCardPowerRewardCandidate || _dogCardPowerTriggered || _dogCardPowerRewardGranted || Owner?.Creature == null)
        {
            return false;
        }

        if (Owner.Creature.CurrentHp <= 0)
        {
            MainFile.Logger.Info($"【联机同步】【戌狗】卡牌Buff战后最大生命奖励跳过：玩家={Owner.NetId}，原因=玩家当前生命值不大于0，阶段={reason}。");
            return false;
        }

        _dogCardPowerRewardGranted = true;
        Flash();
        await CreatureCmd.GainMaxHp(Owner.Creature, VictoryMaxHpGain);
        MainFile.Logger.Info($"【联机同步】【戌狗】卡牌Buff未触发，发放最大生命奖励：玩家={Owner.NetId}，原因={reason}，最大生命+{VictoryMaxHpGain}。");
        return true;
    }

    /// <summary>
    /// 修改受到的伤害：若伤害会致死且本场未触发过，则将生命值保留在25%。
    /// </summary>
    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_usedThisCombat || Owner?.Creature == null || target != Owner.Creature)
        {
            return amount;
        }

        // 判断是否致命伤害
        decimal currentHp = target.CurrentHp;
        if (amount >= currentHp && currentHp > 0)
        {
            _usedThisCombat = true;
            _needsDeathSaveBlock = true;
            UpdateTalismanVisualState(false, 1);
            Flash();

            // 计算保留血量：鼠符咒共鸣时35%，否则25%
            decimal maxHp = target.MaxHp;
            decimal preservePercent = HasRatResonance() ? 0.35m : 0.25m;
            decimal preserveHp = Math.Max(1m, Math.Floor(maxHp * preservePercent));

            // 对齐1代：本次致命伤害归零，并立刻把血量抬到保留值，避免多段攻击后续段数继续打死。
            target.SetCurrentHpInternal(preserveHp);
            MainFile.Logger.Info($"【狗符咒】免死触发：当前HP={currentHp}，最大HP={maxHp}，保留比例={preservePercent:P0}，保留HP={preserveHp}");
            return 0m;
        }

        return amount;
    }

    /// <summary>
    /// 狗符咒免死触发后，通过命令补发999护甲，保证联机动作顺序可同步。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!_needsDeathSaveBlock || Owner?.Creature == null || target != Owner.Creature || !target.IsAlive)
        {
            return;
        }

        _needsDeathSaveBlock = false;
        await CreatureCmd.GainBlock(target, DeathSaveBlock, ValueProp.Unpowered, null);
        MainFile.Logger.Info($"【狗符咒】免死后获得护甲：玩家={Owner.NetId}，护甲+{DeathSaveBlock}。");
    }

    /// <summary>
    /// 战斗结束时，若本场没有使用免死效果，则获得1点最大生命值。
    /// </summary>
    /// <param name="room">结束的战斗房间。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (!_usedThisCombat)
        {
            if (Owner.Creature.CurrentHp <= 0)
            {
                MainFile.Logger.Info($"【狗符咒】战后最大生命奖励跳过：玩家={Owner.NetId}，原因=玩家当前生命值不大于0。");
            }
            else
            {
                Flash();
                await CreatureCmd.GainMaxHp(Owner.Creature, VictoryMaxHpGain);
                MainFile.Logger.Info($"【狗符咒】战斗结束未触发免死，获得最大生命：玩家={Owner.NetId}，最大生命+{VictoryMaxHpGain}。");
            }
        }

        await TryGrantDogCardPowerUntriggeredMaxHpReward("战斗胜利兜底");
    }
}
