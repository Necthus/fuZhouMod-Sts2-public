namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹行动 ID 集中管理，避免状态机和本地化 key 写散。
/// </summary>
internal static class GrandMageDadMoveIds
{
    /// <summary>
    /// 雷符连打：多段攻击。
    /// </summary>
    public const string ThunderCombo = "THUNDER_COMBO";

    /// <summary>
    /// 镇邪法印：攻击并施加虚弱、易伤。
    /// </summary>
    public const string SuppressionSeal = "SUPPRESSION_SEAL";

    /// <summary>
    /// 符咒剥夺追击：第一版只做攻击，后续接封存符咒。
    /// </summary>
    public const string TalismanPursuit = "TALISMAN_PURSUIT";

    /// <summary>
    /// 八卦连轰：多段攻击。
    /// </summary>
    public const string BaguaBarrage = "BAGUA_BARRAGE";

    /// <summary>
    /// 终极封印吟唱：获得大量格挡和人工制品。
    /// </summary>
    public const string UltimateSealChant = "ULTIMATE_SEAL_CHANT";

    /// <summary>
    /// 终极封印处决：第一版做高额全体攻击。
    /// </summary>
    public const string UltimateSealExecute = "ULTIMATE_SEAL_EXECUTE";

    /// <summary>
    /// 副意图：无。
    /// </summary>
    public const string SecondaryNone = "SEC_NONE";

    /// <summary>
    /// 副意图：训诫，向弃牌堆塞入成龙诅咒。
    /// </summary>
    public const string SecondaryCurseJackie = "SEC_CURSE_JACKIE";

    /// <summary>
    /// 副意图：护体，后续阶段接完整防御效果。
    /// </summary>
    public const string SecondaryDefendArtifact = "SEC_DEFEND_ARTIFACT";

    /// <summary>
    /// 副意图：杂念入侵，向弃牌堆塞入随机老爹诅咒。
    /// </summary>
    public const string SecondaryRandomCurse = "SEC_RANDOM_CURSE";

    /// <summary>
    /// 副意图：符咒剥夺，后续阶段接封存符咒。
    /// </summary>
    public const string SecondarySealTalisman = "SEC_SEAL_TALISMAN";

    /// <summary>
    /// 副意图：咒阵扩散，向弃牌堆塞入两张不同随机老爹诅咒。
    /// </summary>
    public const string SecondaryDoubleCurse = "SEC_DOUBLE_CURSE";
}
