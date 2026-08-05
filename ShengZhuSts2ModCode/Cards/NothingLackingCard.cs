using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 我什么都不缺了：集齐十二符咒后自动获得的彩蛋卡片。
/// 0费远古能力牌，仅通过十二符咒觉醒获得。
/// 基础版：固有。每打出1张牌抽1张牌，每回合开始恢复已损失生命30%并伤害随机敌人，获得4点能量、4层力量、4层敏捷、4层影噬，能量上限提高2点。
/// 升级版：包含基础版效果，获得数值提高到6，并且每回合开始力量、敏捷、影噬各成长1点。
/// </summary>
public class NothingLackingCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 能量上限提高量（基础版和升级版相同）。
    /// </summary>
    private const int EnergyCapIncrease = 2;

    /// <summary>
    /// 构造卡牌数值：0费远古能力牌，基础版和升级版均固有。
    /// Magic变量：基础4，升级+2（即升级后为6），用于能量/力量/敏捷/影噬的数值。
    /// </summary>
    public NothingLackingCard() : base(0, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
        WithVar("Magic", 4, 2);
        WithKeywords(CardKeyword.Innate);
    }

    /// <summary>
    /// 升级时保留固有词条，数值升级由Magic变量负责。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 出牌时：施加NothingLackingPower + 获得能量 + 获得力量 + 获得敏捷 + 获得影噬 + 能量上限提高。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int stats = (int)DynamicVars["Magic"].BaseValue;

        // 施加"每出牌抽1张 + 每回合恢复/伤害 + 能量上限提高"能力
        NothingLackingPower? nothingLackingPower = await CommonActions.ApplySelf<NothingLackingPower>(choiceContext, this, EnergyCapIncrease);
        if (nothingLackingPower != null)
        {
            nothingLackingPower.StatsGrowthPerTurn = Math.Max(nothingLackingPower.StatsGrowthPerTurn, IsUpgraded ? 1 : 0);
        }

        // 获得能量（数值与力量/敏捷/影噬相同）
        await PlayerCmd.GainEnergy(stats, Owner);

        // 获得力量
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, stats, Owner.Creature, this);

        // 获得敏捷
        await PowerCmd.Apply<DexterityPower>(Owner.Creature, stats, Owner.Creature, this);

        // 获得影噬
        await CommonActions.ApplySelf<DominionPower>(choiceContext, this, stats);

        MainFile.Logger.Info($"【我什么都不缺了】打出成功：能量+{stats}，力量+{stats}，敏捷+{stats}，影噬+{stats}，能量上限+{EnergyCapIncrease}，升级={IsUpgraded}");
    }
}
