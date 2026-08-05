using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 给皇帝的供奉：治疗生命并参考本体炼制药水获得随机药水。
/// </summary>
public class OfferingToEmperor : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：1费蓝色技能，治疗5(8)点生命。
    /// </summary>
    public OfferingToEmperor() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Heal", 5, 3);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时治疗并获得一瓶战斗随机药水。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int healAmount = (int)DynamicVars["Heal"].BaseValue;
        await CreatureCmd.Heal(Owner.Creature, healAmount);

        PotionModel potion = PotionFactory.CreateRandomPotionInCombat(Owner, Owner.RunState.Rng.CombatPotionGeneration).ToMutable();
        PotionProcureResult result = await PotionCmd.TryToProcure(potion, Owner);
        if (result.success)
        {
            MainFile.Logger.Info($"【给皇帝的供奉】治疗={healAmount}，获得药水={potion.Id.Entry}。");
        }
        else
        {
            MainFile.Logger.Info($"【给皇帝的供奉】治疗={healAmount}，药水获取失败：{potion.Id.Entry}，原因={result.failureReason}。");
        }
    }
}
