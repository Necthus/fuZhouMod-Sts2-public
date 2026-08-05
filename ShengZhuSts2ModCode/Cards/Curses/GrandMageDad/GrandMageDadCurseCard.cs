using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

/// <summary>
/// 大法师老爹专属诅咒牌基类：统一诅咒类型、稀有度和不可打出规则。
/// </summary>
public abstract class GrandMageDadCurseCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 当前诅咒是否仍在等待支付代价。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool IsTaxPending { get; private set; }

    /// <summary>
    /// 当前诅咒是否已经支付过或失效，用于卡面展示。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool IsTaxResolved { get; private set; }

    /// <summary>
    /// 构造老爹诅咒牌，使用塔2原生诅咒的 -1 费用显示。
    /// </summary>
    protected GrandMageDadCurseCard()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    /// <summary>
    /// 老爹诅咒不能主动打出，只通过抽到和回合结束触发负面效果。
    /// </summary>
    protected override bool IsPlayable => false;

    /// <summary>
    /// 将诅咒标记为刚进入手牌且尚未支付代价。
    /// </summary>
    /// <param name="source">触发来源。</param>
    protected void MarkTaxPending(string source)
    {
        IsTaxPending = true;
        IsTaxResolved = false;
        GrandMageCurseTaxService.RefreshPowers(Owner);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹诅咒】诅咒税待支付：玩家={Owner?.NetId.ToString() ?? "无"}，卡牌={Id.Entry}，来源={source}。");
    }

    /// <summary>
    /// 将诅咒标记为代价已支付。
    /// </summary>
    /// <param name="reason">支付原因。</param>
    public void MarkTaxPaid(string reason)
    {
        ResolveTax("已支付", reason, true);
    }

    /// <summary>
    /// 将诅咒标记为代价已支付，并由调用方统一刷新费用显示。
    /// </summary>
    /// <param name="reason">支付原因。</param>
    public void MarkTaxPaidWithoutRefresh(string reason)
    {
        ResolveTax("已支付", reason, false);
    }

    /// <summary>
    /// 如果诅咒未支付就离开手牌，则让该诅咒税失效。
    /// </summary>
    /// <param name="reason">失效原因。</param>
    protected void ExpirePendingTax(string reason)
    {
        if (!IsTaxPending)
        {
            return;
        }

        ResolveTax("已失效", reason, true);
    }

    /// <summary>
    /// 卡牌换牌堆时，若离开手牌则失效未支付代价。
    /// </summary>
    /// <param name="card">移动的卡牌。</param>
    /// <param name="oldPileType">旧牌堆。</param>
    /// <param name="source">来源模型。</param>
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (!ReferenceEquals(card, this) || oldPileType != PileType.Hand)
        {
            return Task.CompletedTask;
        }

        ExpirePendingTax("离开手牌");
        GrandMageCurseTaxService.RefreshPowers(Owner);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 诅咒牌被强制打出时不做任何事，避免自动打出场景报错。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 统一结算诅咒税状态。
    /// </summary>
    /// <param name="stateName">状态名称。</param>
    /// <param name="reason">原因。</param>
    /// <param name="refreshPowers">是否立即刷新费用显示。</param>
    private void ResolveTax(string stateName, string reason, bool refreshPowers)
    {
        if (!IsTaxPending && IsTaxResolved)
        {
            return;
        }

        IsTaxPending = false;
        IsTaxResolved = true;
        if (refreshPowers)
        {
            GrandMageCurseTaxService.RefreshPowers(Owner);
        }

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹诅咒】诅咒税{stateName}：玩家={Owner?.NetId.ToString() ?? "无"}，卡牌={Id.Entry}，原因={reason}。");
    }

    /// <summary>
    /// 给卡面描述补充当前代价状态文本。
    /// </summary>
    /// <param name="description">卡牌描述本地化对象。</param>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("TaxState", BuildTaxStateText());
    }

    /// <summary>
    /// 根据当前语言生成玩家可读的代价状态文本。
    /// </summary>
    /// <returns>代价状态文本。</returns>
    private string BuildTaxStateText()
    {
        bool useChinese = KeywordRegistry.LoadedLanguage == "zhs";
        return IsTaxPending
            ? useChinese ? "未支付" : "unpaid"
            : useChinese ? "已支付" : "paid";
    }
}
