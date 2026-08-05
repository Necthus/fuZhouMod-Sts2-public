using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 大法师老爹的正气反扑：展示动态生命折算出的力量来源，实际伤害加成由力量能力承担。
/// </summary>
public class GrandMageCounterPouncePower : ShengZhuSts2ModPower
{
    private int _extraHp;
    private int _playerCount = 1;

    /// <summary>
    /// 正气反扑属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 用计数器显示本场折算出的力量。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 文案动态变量：额外生命、玩家数、折算力量。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ExtraHp", 0),
        new DynamicVar("PlayerCount", 1),
        new DynamicVar("Strength", 0)
    ];

    /// <summary>
    /// 生效后按当前战斗人数刷新展示变量。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _playerCount = Math.Max(1, Owner?.CombatState?.Players.Count ?? 1);
        DynamicVars["ExtraHp"].BaseValue = _extraHp;
        DynamicVars["PlayerCount"].BaseValue = _playerCount;
        DynamicVars["Strength"].BaseValue = Amount;
        MainFile.Logger.Info($"【大法师老爹】【正气反扑】展示能力生效：额外生命={_extraHp}，玩家数={_playerCount}，折算力量={Amount}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置动态生命展示数据。
    /// </summary>
    /// <param name="extraHp">本场动态额外生命。</param>
    /// <param name="playerCount">战斗玩家数。</param>
    public void Configure(int extraHp, int playerCount)
    {
        _extraHp = Math.Max(0, extraHp);
        _playerCount = Math.Max(1, playerCount);
        DynamicVars["ExtraHp"].BaseValue = _extraHp;
        DynamicVars["PlayerCount"].BaseValue = _playerCount;
        DynamicVars["Strength"].BaseValue = Amount;
    }
}
