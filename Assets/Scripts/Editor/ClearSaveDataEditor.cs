using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器脚本：用于在Unity编辑器中清除存档数据（测试用）
/// </summary>
public class ClearSaveDataEditor
{
    private const string SAVE_FILE_NAME = "gamedata.json";
    
    /// <summary>
    /// 获取存档文件路径
    /// </summary>
    private static string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
    }
    
    /// <summary>
    /// 清除存档文件（编辑器菜单项）
    /// </summary>
    [MenuItem("Tools/Clear Save Data")]
    public static void ClearSaveData()
    {
        // 使用DataManager的静态方法删除存档文件
        DataManager.DeleteSaveFile();
        
        string saveFilePath = GetSaveFilePath();
        if (File.Exists(saveFilePath))
        {
            EditorUtility.DisplayDialog("清除存档失败", "存档文件仍然存在，请检查控制台日志。", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("清除存档", "存档文件已成功清除！", "确定");
        }
    }
    
    /// <summary>
    /// 显示存档文件路径（编辑器菜单项）
    /// </summary>
    [MenuItem("Tools/Show Save Data Path")]
    public static void ShowSaveDataPath()
    {
        string saveFilePath = GetSaveFilePath();
        bool exists = File.Exists(saveFilePath);
        
        string message = $"存档文件路径:\n{saveFilePath}\n\n文件存在: {(exists ? "是" : "否")}";
        
        if (exists)
        {
            FileInfo fileInfo = new FileInfo(saveFilePath);
            message += $"\n文件大小: {fileInfo.Length} 字节";
            message += $"\n最后修改时间: {fileInfo.LastWriteTime}";
        }
        
        Debug.Log(message);
        EditorUtility.DisplayDialog("存档文件路径", message, "确定");
    }
}

