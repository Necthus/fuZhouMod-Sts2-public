using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

[Pool(typeof(ShengZhuSts2ModRelicPool))]
public abstract class ShengZhuSts2ModRelic : CustomRelicModel
{
    public override string PackedIconPath => ResolveRelicIconPath();
    protected override string PackedIconOutlinePath => ResolveTransparentRelicOutlinePath();
    protected override string BigIconPath => ResolveBigRelicIconPath();

    /// <summary>
    /// 符咒遗物商店价格：设为217，经过0.85~1.15随机浮动后最终价格在185~250金币区间。
    /// </summary>
    public override int MerchantCost => 217;

    /// <summary>
    /// 获取当前遗物应该展示的描述 LocString key。
    /// 持有鼠符咒时返回共鸣版 key，否则返回基础版 key。
    /// 供 TalismanDescriptionPatch 调用。
    /// </summary>
    /// <returns>描述文本的 LocString key（不含表名前缀）。</returns>
    public virtual string GetDynamicDescriptionKey()
    {
        string entry = Id.Entry;

        // 鼠符咒自身不做切换
        if (this is RatTalisman)
        {
            return entry + ".description";
        }

        // 有鼠符咒共鸣且存在共鸣描述时，使用共鸣版
        if (HasRatResonance())
        {
            string resonanceKey = entry + ".description_resonance";
            if (LocString.Exists("relics", resonanceKey))
            {
                return resonanceKey;
            }
        }

        return entry + ".description";
    }

    /// <summary>
    /// 给动态遗物描述补充变量；默认不做处理。
    /// </summary>
    /// <param name="description">准备展示的描述。</param>
    public virtual void CustomizeDescription(LocString description)
    {
    }

    /// <summary>
    /// 检测是否拥有鼠符咒共鸣（同时持有鼠符咒时激活额外效果）。
    /// </summary>
    /// <returns>玩家同时持有鼠符咒时返回 true。</returns>
    protected bool HasRatResonance()
    {
        return ShengZhuFormService.HasAwakenedRatForm(Owner);
    }

    /// <summary>
    /// 判断一次出牌是否由当前遗物持有者本人打出。
    /// 联机时出牌钩子会收到队友的出牌事件，符咒效果不能因此被队友触发或消耗。
    /// </summary>
    /// <param name="cardPlay">出牌信息。</param>
    /// <returns>出牌者是当前遗物持有者时返回 true。</returns>
    protected bool IsCardPlayedByOwner(CardPlay? cardPlay)
    {
        if (Owner == null || cardPlay?.Card?.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(cardPlay.Card.Owner, Owner) || cardPlay.Card.Owner.NetId == Owner.NetId;
    }

    /// <summary>
    /// 按符咒交互状态刷新遗物视觉：激发时高亮闪烁，冷却时置灰，普通时恢复正常。
    /// </summary>
    /// <param name="activated">是否处于激发状态。</param>
    /// <param name="cooldownTurnsRemaining">剩余冷却回合数。</param>
    protected void UpdateTalismanVisualState(bool activated, int cooldownTurnsRemaining)
    {
        RelicStatus targetStatus = cooldownTurnsRemaining > 0
            ? RelicStatus.Disabled
            : (activated ? RelicStatus.Active : RelicStatus.Normal);

        if (Status != targetStatus)
        {
            Status = targetStatus;
        }

        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// 解析遗物小图路径：优先使用标准命名，再兼容去下划线命名，最后回退默认图。
    /// </summary>
    /// <returns>可用的小图路径。</returns>
    private string ResolveRelicIconPath()
    {
        return ResolveRelicPath("images/relics", ".png", "relic.png");
    }

    /// <summary>
    /// 解析遗物大图路径：优先使用标准命名，再兼容去下划线命名，最后回退默认大图。
    /// </summary>
    /// <returns>可用的大图路径。</returns>
    private string ResolveBigRelicIconPath()
    {
        return ResolveRelicPath("images/relics/big", ".png", "relic.png");
    }

    /// <summary>
    /// 解析遗物透明描边路径：塔2小遗物栏会固定渲染描边层，使用透明图避免给自定义遗物套黑圈。
    /// </summary>
    /// <returns>透明描边图路径。</returns>
    private string ResolveTransparentRelicOutlinePath()
    {
        return Path.Join(MainFile.ResPath, "images/relics/tigertalisman_outline.png");
    }

    /// <summary>
    /// 按“标准名 -> 去下划线名 -> 默认名”顺序解析路径，兼容旧资源命名差异。
    /// </summary>
    /// <param name="folder">相对资源目录。</param>
    /// <param name="suffix">文件后缀（含前缀片段）。</param>
    /// <param name="fallbackFileName">兜底文件名。</param>
    /// <returns>存在的资源路径；若都不存在，则返回兜底路径。</returns>
    private string ResolveRelicPath(string folder, string suffix, string fallbackFileName)
    {
        string entry = Id.Entry.RemovePrefix().ToLowerInvariant();
        string compactEntry = entry.Replace("_", string.Empty);

        string standardPath = Path.Join(MainFile.ResPath, folder, $"{entry}{suffix}");
        if (ResourceLoader.Exists(standardPath))
        {
            return standardPath;
        }

        string compactPath = Path.Join(MainFile.ResPath, folder, $"{compactEntry}{suffix}");
        if (ResourceLoader.Exists(compactPath))
        {
            return compactPath;
        }

        return Path.Join(MainFile.ResPath, folder, fallbackFileName);
    }
}
