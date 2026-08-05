using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 小玉偷符咒：随机获得一张动物符咒牌到手牌，本回合费用为0。
/// </summary>
public class JadeStealsTalisman : TeamJackieCard
{
    /// <summary>
    /// 可随机生成的十三张动物符咒牌。
    /// </summary>
    private static readonly Type[] TalismanCardTypes =
    [
        typeof(RatTalismanCard),
        typeof(OxTalismanCard),
        typeof(TigerTalismanAttackCard),
        typeof(TigerTalismanSkillCard),
        typeof(RabbitTalismanCard),
        typeof(DragonTalismanCard),
        typeof(SnakeTalismanCard),
        typeof(HorseTalismanCard),
        typeof(GoatTalismanCard),
        typeof(MonkeyTalismanCard),
        typeof(RoosterTalismanCard),
        typeof(DogTalismanCard),
        typeof(PigTalismanCard)
    ];

    /// <summary>
    /// 构造卡牌数值：1费技能，升级降为0费，消耗。
    /// </summary>
    public JadeStealsTalisman() : base(1, CardType.Skill, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时费用从1降为0。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时随机生成一张符咒牌加入手牌，并把本回合费用设为0。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner?.Creature?.CombatState;
        if (Owner == null || combatState == null)
        {
            MainFile.Logger.Info("【小玉偷符咒】未处于战斗状态，无法生成动物符咒牌。");
            return;
        }

        Type chosenType = TalismanCardTypes[StableRandomHelper.NextInt(Owner, TalismanCardTypes.Length)];
        CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == chosenType);
        if (prototype == null)
        {
            return;
        }

        CardModel generated = combatState.CreateCard(prototype, Owner);
        generated.EnergyCost.SetThisTurn(0, reduceOnly: true);
        await CardPileCmdHelper.AddGeneratedCardToCombat(generated, PileType.Hand, true);
    }
}
