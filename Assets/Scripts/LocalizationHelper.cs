using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 本地化工具类，提供静态方法方便调用翻译
/// </summary>
public static class LocalizationHelper
{
    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="tableName">表名，默认为"GameText"</param>
    /// <param name="key">键名</param>
    /// <returns>本地化后的字符串</returns>
    public static string GetLocalizedString(string key, string tableName = "GameText")
    {
        var localizedString = new LocalizedString(tableName, key);
        var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
            localizedString.TableReference, 
            localizedString.TableEntryReference);
        return handle.WaitForCompletion();
    }
    
    /// <summary>
    /// 获取本地化字符串（带参数）
    /// </summary>
    /// <param name="tableName">表名，默认为"GameText"</param>
    /// <param name="key">键名</param>
    /// <param name="arguments">参数数组</param>
    /// <returns>本地化后的字符串</returns>
    public static string GetLocalizedString(string key, object[] arguments, string tableName = "GameText")
    {
        var localizedString = new LocalizedString(tableName, key);
        localizedString.Arguments = arguments;
        var handle = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
            localizedString.TableReference, 
            localizedString.TableEntryReference,arguments);
        return handle.WaitForCompletion();
    }
}


