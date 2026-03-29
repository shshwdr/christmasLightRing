/// <summary>
/// 关卡 CSV 中 boss 列使用的标识；部分 boss 共用同一套玩法但用不同名字区分。
/// </summary>
public static class BossLevelIds
{
    public const string Horribleman = "horribleman";
    /// <summary>与 horribleman 玩法相同，关卡里填写的独立 boss 名（大小写不敏感）。</summary>
    public const string HorriblemanNew = "horriblemannew";

    public static bool IsHorriblemanStyleBoss(string bossIdentifier)
    {
        if (string.IsNullOrEmpty(bossIdentifier)) return false;
        string b = bossIdentifier.ToLowerInvariant();
        return b == Horribleman || b == HorriblemanNew;
    }
}
