using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 亥猪：2费稀有攻击牌。造成6(升级12)点伤害。移除目标白名单Buff并给予6(升级12)层易伤。消耗。
/// </summary>
public class PigTalismanCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：2费，攻击牌，稀有，消耗。6点伤害(升级+6)，6层易伤(升级+6)。
    /// </summary>
    public PigTalismanCard() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithDamage(6, 6);
        WithVar("Magic", 6, 6);
    }

    /// <summary>
    /// 出牌时造成伤害、移除目标白名单能力，并给予易伤。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        List<Creature> targets = GetTargets(cardPlay);
        if (targets.Count == 0)
        {
            return;
        }

        if (HasTalismanRelic<PigTalisman>())
        {
            await CreatureCmd.Damage(choiceContext, targets, DynamicVars.Damage.BaseValue, ValueProp.Move, Owner.Creature, this);
            foreach (Creature target in targets)
            {
                await ApplyPigDebuffEffects(choiceContext, target);
            }

            MainFile.Logger.Info($"【亥猪】猪符咒共鸣：已对{targets.Count}个敌人结算亥猪效果。");
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        await ApplyPigDebuffEffects(choiceContext, targets[0]);
    }

    /// <summary>
    /// 获取亥猪目标；持有猪符咒遗物时改为所有存活敌人。
    /// </summary>
    private List<Creature> GetTargets(CardPlay cardPlay)
    {
        if (HasTalismanRelic<PigTalisman>() && Owner?.Creature?.CombatState != null)
        {
            return Owner.Creature.CombatState
                .GetOpponentsOf(Owner.Creature)
                .Where(c => c.IsAlive && !c.IsDead)
                .ToList();
        }

        return cardPlay.Target == null ? [] : [cardPlay.Target];
    }

    /// <summary>
    /// 对指定目标执行亥猪的移除白名单Buff和易伤效果。
    /// </summary>
    private async Task ApplyPigDebuffEffects(PlayerChoiceContext choiceContext, Creature target)
    {
        await RemoveWhitelistedBuffs(target);

        int vulnerableAmount = (int)DynamicVars["Magic"].BaseValue;
        await CommonActions.Apply<VulnerablePower>(choiceContext, target, this, vulnerableAmount);
        MainFile.Logger.Info($"【亥猪】给予目标{vulnerableAmount}层易伤。");
    }

    /// <summary>
    /// 移除目标身上白名单中的Buff：力量、人工制品、荆棘、无实体、覆甲、天罚、振翅、硬化外壳、翱翔、难以杀灭。
    /// </summary>
    private async Task RemoveWhitelistedBuffs(Creature target)
    {
        // 白名单：只移除这些指定Buff，不移除本mod的飞行FlightAfuPower。
        HashSet<Type> whitelist =
        [
            typeof(StrengthPower),
            typeof(ArtifactPower),
            typeof(ThornsPower),
            typeof(IntangiblePower),
            typeof(PlatingPower),
            typeof(DoomPower),
            typeof(DemisePower),
            typeof(NemesisPower),
            typeof(FlutterPower),
            typeof(HardenedShellPower),
            typeof(SoarPower),
            typeof(HardToKillPower)
        ];

        // 收集要移除的Power（避免遍历时修改集合）
        var powersToRemove = target.Powers
            .Where(p => whitelist.Contains(p.GetType()))
            .ToList();

        foreach (var power in powersToRemove)
        {
            await PowerCmd.Remove(power);
            MainFile.Logger.Info($"【亥猪】移除目标Buff：{power.Id.Entry}。");
        }
    }
}
