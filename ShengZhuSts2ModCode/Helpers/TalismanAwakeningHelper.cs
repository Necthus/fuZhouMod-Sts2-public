using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 十二符咒觉醒辅助类：检测玩家是否集齐十二符咒，若集齐则自动获得【我什么都不缺了】卡牌。
/// </summary>
public static class TalismanAwakeningHelper
{
    /// <summary>
    /// 觉醒音效资源路径。
    /// </summary>
    private static readonly string AwakeningAudioPath = Path.Join(MainFile.ResPath, "audio", "NothingLackingCard.mp3");

    /// <summary>
    /// 玩家级持久化标记：记录十二符咒集齐奖励是否已经发放。不能用牌组是否存在卡牌代替，因为卡牌可能被删除、融合或由其他来源提前获得。
    /// </summary>
    private static readonly SavedSpireField<Player, bool> TwelveTalismanAwakeningRewardGranted = new(() => false, "ShengZhuTwelveTalismanAwakeningRewardGranted");

    /// <summary>
    /// 所有十二符咒遗物类型列表。
    /// </summary>
    private static readonly Type[] AllTalismanTypes =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 正在触发十二符咒觉醒的玩家，避免同一玩家重复发放卡牌。
    /// </summary>
    private static readonly HashSet<Player> AwakeningPlayers = [];

    /// <summary>
    /// 初始化十二符咒觉醒的存档字段，确保 BaseLib 在保存字段扫描前完成注册。
    /// </summary>
    public static void InitializeSavedFields()
    {
        _ = TwelveTalismanAwakeningRewardGranted;
    }

    /// <summary>
    /// 检查玩家是否集齐十二符咒，若集齐且十二符咒奖励尚未发放则触发觉醒。
    /// 性能优化：先做快速前置检查，避免不必要的遍历。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    public static void CheckAndTriggerAwakening(Player? player)
    {
        if (player == null)
        {
            return;
        }

        // 防重入
        if (AwakeningPlayers.Contains(player))
        {
            return;
        }

        if (HasGrantedAwakeningReward(player))
        {
            return;
        }

        var deckCards = player.Deck?.Cards;
        if (deckCards == null)
        {
            MainFile.Logger.Info($"【十二符咒觉醒】检查跳过：玩家={FormatPlayer(player)}，原因=主牌组不可读取。");
            return;
        }

        // 检查是否集齐12个符咒
        if (!HasAllTwelveTalismans(player))
        {
            return;
        }

        // 触发觉醒
        TriggerAwakening(player);
    }

    /// <summary>
    /// 判断玩家是否拥有全部十二符咒。
    /// </summary>
    private static bool HasAllTwelveTalismans(Player player)
    {
        foreach (Type talismanType in AllTalismanTypes)
        {
            if (!player.Relics.Any(r => r.GetType() == talismanType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 触发觉醒：将【我什么都不缺了】加入玩家牌组，并播放音效。
    /// </summary>
    private static void TriggerAwakening(Player player)
    {
        if (!AwakeningPlayers.Add(player))
        {
            return;
        }

        try
        {
            MainFile.Logger.Info("【十二符咒觉醒】条件达成！十二枚符咒已悉数归位，正在发放【我什么都不缺了】...");

            // 创建卡牌并加入牌组，具体牌组修改统一交给服务处理。
            var nothingLackingCard = (CardModel)ModelDb.Card<NothingLackingCard>().ToMutable();
            DeckMutationService.AddCardToDeck(player, nothingLackingCard, true, "十二符咒觉醒");
            if (!IsCardInDeck(player, nothingLackingCard))
            {
                MainFile.Logger.Info($"【十二符咒觉醒】觉醒奖励发放失败，未写入已发放标记：玩家={FormatPlayer(player)}，卡牌={nothingLackingCard.Id.Entry}。");
                return;
            }

            MarkAwakeningRewardGranted(player);

            // 播放觉醒音效
            PlayAwakeningSound();

            MainFile.Logger.Info("【十二符咒觉醒】觉醒完成，【我什么都不缺了】已加入牌组。");
        }
        finally
        {
            AwakeningPlayers.Remove(player);
        }
    }

    /// <summary>
    /// 判断指定卡牌是否已经进入玩家主牌组，避免加牌失败时误写奖励已发放标记。
    /// </summary>
    private static bool IsCardInDeck(Player player, CardModel card)
    {
        return player.Deck?.Cards?.Contains(card) == true;
    }

    /// <summary>
    /// 判断玩家是否已经领取过十二符咒集齐奖励。
    /// </summary>
    private static bool HasGrantedAwakeningReward(Player player)
    {
        return TwelveTalismanAwakeningRewardGranted.Get(player) == true;
    }

    /// <summary>
    /// 标记玩家已经领取过十二符咒集齐奖励，并写入玩家级持久状态。
    /// </summary>
    private static void MarkAwakeningRewardGranted(Player player)
    {
        TwelveTalismanAwakeningRewardGranted.Set(player, true);
        MainFile.Logger.Info($"【联机同步】来源=十二符咒觉醒，玩家={FormatPlayer(player)}，动作=写入觉醒奖励已发放标记，结果=已发放。");
    }

    /// <summary>
    /// 格式化玩家信息，方便联机日志对比。
    /// </summary>
    private static string FormatPlayer(Player? player)
    {
        return player == null ? "无玩家" : $"{player.NetId}/{player.Character.Id.Entry}";
    }

    /// <summary>
    /// 播放觉醒音效。
    /// </summary>
    private static void PlayAwakeningSound()
    {
        if (!ResourceLoader.Exists(AwakeningAudioPath))
        {
            MainFile.Logger.Info($"【十二符咒觉醒】音效文件不存在，跳过播放：{AwakeningAudioPath}");
            return;
        }

        var audioStream = ResourceLoader.Load<AudioStream>(AwakeningAudioPath);
        if (audioStream == null)
        {
            MainFile.Logger.Info("【十二符咒觉醒】音效加载失败，跳过播放。");
            return;
        }

        var audioPlayer = new AudioStreamPlayer();
        audioPlayer.Stream = audioStream;
        audioPlayer.Bus = "SFX";

        // 添加到场景树并播放
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        sceneTree?.Root?.AddChild(audioPlayer);
        audioPlayer.Play();

        // 播放完毕后自动释放
        audioPlayer.Finished += () => audioPlayer.QueueFree();

        MainFile.Logger.Info("【十二符咒觉醒】觉醒音效已播放。");
    }
}
