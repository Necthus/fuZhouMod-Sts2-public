using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 我什么都不缺了能力：每打出1张牌抽1张牌，每回合开始时按已损失生命恢复并伤害随机敌人，每回合结束时施加EnergyNextTurnPower实现能量上限提高。
/// Amount 层数 = 能量上限提高的点数。
/// </summary>
public class NothingLackingPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 升级版每回合开始时额外成长的力量、敏捷、影噬层数；基础版为0。
    /// </summary>
    public int StatsGrowthPerTurn { get; set; }

    /// <summary>
    /// 增益类型。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可堆叠（层数=能量上限提高量）。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 根据是否拥有升级版成长效果，切换能力悬浮说明。
    /// </summary>
    protected override string SmartDescriptionLocKey => StatsGrowthPerTurn > 0
        ? Id.Entry + ".smartDescription_upgraded"
        : base.SmartDescriptionLocKey;

    /// <summary>
    /// 每回合开始时，恢复已损失生命值的30%，并对随机敌人造成等额伤害；升级版额外成长力量、敏捷、影噬。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || Owner.IsDead || player.Creature != Owner)
        {
            return;
        }

        await HealLostHpAndDamageRandomEnemy(choiceContext);
        await GrowUpgradedStats();
    }

    /// <summary>
    /// 每打出1张牌后，抽1张牌。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Player? ownerPlayer = Owner?.Player;
        if (ownerPlayer == null || !IsOwnerCard(cardPlay.Card))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1, ownerPlayer);
    }

    /// <summary>
    /// 每回合结束时，施加EnergyNextTurnPower使下回合额外获得能量（模拟能量上限提高）。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<EnergyNextTurnPower>(Owner, Amount, Owner, null);
    }

    /// <summary>
    /// 按玩家已损失生命值的30%回血，并对随机存活敌人造成等额伤害。
    /// </summary>
    private async Task HealLostHpAndDamageRandomEnemy(PlayerChoiceContext choiceContext)
    {
        if (Owner == null)
        {
            return;
        }

        int lostHp = Math.Max(0, Owner.MaxHp - Owner.CurrentHp);
        int healAndDamage = (int)(lostHp * 0.3m);
        if (healAndDamage <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner, healAndDamage);

        Creature? target = GetRandomLivingEnemy();
        if (target == null)
        {
            MainFile.Logger.Info($"【我什么都不缺了】回合开始触发：已损失生命={lostHp}，恢复={healAndDamage}，但没有可伤害的敌人。");
            return;
        }

        await CreatureCmd.Damage(choiceContext, target, healAndDamage, ValueProp.Unpowered, Owner, null);
        MainFile.Logger.Info($"【我什么都不缺了】回合开始触发：已损失生命={lostHp}，恢复={healAndDamage}，对{target.Name}造成伤害={healAndDamage}。");
    }

    /// <summary>
    /// 升级版每回合开始时，力量、敏捷、影噬各成长指定层数。
    /// </summary>
    private async Task GrowUpgradedStats()
    {
        if (Owner == null || StatsGrowthPerTurn <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(Owner, StatsGrowthPerTurn, Owner, null);
        await PowerCmd.Apply<DexterityPower>(Owner, StatsGrowthPerTurn, Owner, null);
        await PowerCmd.Apply<DominionPower>(Owner, StatsGrowthPerTurn, Owner, null);
        MainFile.Logger.Info($"【我什么都不缺了】升级版成长：力量+{StatsGrowthPerTurn}，敏捷+{StatsGrowthPerTurn}，影噬+{StatsGrowthPerTurn}。");
    }

    /// <summary>
    /// 从当前战斗中随机取一名存活敌人。
    /// </summary>
    private Creature? GetRandomLivingEnemy()
    {
        if (Owner?.CombatState == null)
        {
            return null;
        }

        List<Creature> enemies = Owner.CombatState.HittableEnemies
            .Where(creature => creature.IsAlive && !creature.IsDead)
            .ToList();

        return enemies.Count == 0 ? null : enemies[StableRandomHelper.NextInt(Owner.Player, enemies.Count)];
    }

    /// <summary>
    /// 判断卡牌是否由当前能力持有者本人打出，避免联机时队友出牌触发本能力抽牌。
    /// </summary>
    private bool IsOwnerCard(CardModel? card)
    {
        if (Owner?.Player == null || card?.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(card.Owner, Owner.Player) || card.Owner.NetId == Owner.Player.NetId;
    }
}
