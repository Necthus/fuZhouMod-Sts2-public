using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 武士刀：让尼嘉-忍者团改为攻击所有敌人。
/// </summary>
public class Katana : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费金色能力牌。
    /// </summary>
    public Katana() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时为自身补上固有关键词。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    /// <summary>
    /// 出牌时获得武士刀能力，后续尼嘉-忍者团会攻击所有敌人。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.Apply<KatanaPower>(choiceContext, Owner.Creature, this, 1);
        MainFile.Logger.Info($"【联机同步】来源=武士刀，玩家={Owner.NetId}/{Owner.Character.Id.Entry}，动作=获得尼嘉-忍者团群伤能力，升级={IsUpgraded}。");
    }
}
