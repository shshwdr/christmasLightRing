using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Linq;

/// <summary>
/// 为 Unity Localization 添加带 BOM 的 CSV 导出功能
/// 在 StringTableCollection 的上下文菜单中添加 "CSV (With BOM)..." 选项
/// </summary>
public static class LocalizationCSVExportWithBOM
{
    /// <summary>
    /// 在 StringTableCollection 的上下文菜单中添加导出选项
    /// </summary>
    [MenuItem("Assets/Export String Table Collection/CSV (With BOM)...", false, 1000)]
    public static void ExportStringTableCollectionToCSVWithBOM()
    {
        // 获取选中的对象
        Object[] selectedObjects = Selection.objects;
        
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个 StringTableCollection 资源", "确定");
            return;
        }

        foreach (Object obj in selectedObjects)
        {
            StringTableCollection collection = obj as StringTableCollection;
            if (collection == null)
            {
                // 尝试从路径加载
                string path = AssetDatabase.GetAssetPath(obj);
                collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            }

            if (collection != null)
            {
                ExportCollectionToCSVWithBOM(collection);
            }
        }
    }

    /// <summary>
    /// 验证菜单项是否可用
    /// </summary>
    [MenuItem("Assets/Export String Table Collection/CSV (With BOM)...", true)]
    public static bool ValidateExportStringTableCollectionToCSVWithBOM()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
            return false;

        foreach (Object obj in selectedObjects)
        {
            if (obj is StringTableCollection)
                return true;

            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<StringTableCollection>(path) != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 导出 StringTableCollection 到 CSV 文件（带 BOM）
    /// </summary>
    private static void ExportCollectionToCSVWithBOM(StringTableCollection collection)
    {
        // 获取保存路径
        string defaultFileName = collection.name + ".csv";
        string path = EditorUtility.SaveFilePanel("导出 CSV (With BOM)", "Assets/Localization", defaultFileName, "csv");

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            // 使用 Unity Localization 的导出功能，但使用带 BOM 的编码
            ExportToCSVWithBOM(collection, path);
            
            EditorUtility.DisplayDialog("导出成功", $"已成功导出到:\n{path}\n\n文件已包含 UTF-8 BOM", "确定");
            Debug.Log($"LocalizationCSVExportWithBOM: 已导出 {collection.name} 到 {path} (带 BOM)");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出时发生错误:\n{e.Message}", "确定");
            Debug.LogError($"LocalizationCSVExportWithBOM: 导出失败 - {e.Message}");
        }
    }

    /// <summary>
    /// 导出到 CSV 文件（带 BOM）
    /// </summary>
    private static void ExportToCSVWithBOM(StringTableCollection collection, string filePath)
    {
        SharedTableData sharedData = collection.SharedData;
        if (sharedData == null)
        {
            throw new System.Exception("SharedTableData 为空");
        }

        // 获取所有语言表
        var tables = collection.StringTables;
        if (tables == null || tables.Count == 0)
        {
            throw new System.Exception("没有找到语言表");
        }

        // 构建 CSV 内容
        StringBuilder csv = new StringBuilder();

        // 写入表头（不包含 Comments 列）
        csv.Append("Key,Id");
        foreach (var table in tables)
        {
            if (table != null)
            {
                string localeName = table.LocaleIdentifier.ToString();
                csv.Append($",{localeName}");
            }
        }
        csv.AppendLine();

        // 写入数据行
        foreach (var entry in sharedData.Entries)
        {
            // Key（始终用引号包裹，匹配现有 CSV 格式）
            string key = entry.Key ?? "";
            // 如果 Key 包含引号、逗号或换行符，需要转义引号
            if (key.Contains("\"") || key.Contains(",") || key.Contains("\n") || key.Contains("\r"))
            {
                csv.Append($"\"{key.Replace("\"", "\"\"")}\",");
            }
            else
            {
                csv.Append($"\"{key}\",");
            }
            
            // Id
            csv.Append(entry.Id);
            
            // 各语言的值（不包含 Comments）
            foreach (var table in tables)
            {
                csv.Append(",");
                if (table != null)
                {
                    var tableEntry = table.GetEntry(entry.Id);
                    if (tableEntry != null && !string.IsNullOrEmpty(tableEntry.Value))
                    {
                        string value = tableEntry.Value;
                        // 转义引号
                        if (value.Contains("\""))
                        {
                            value = value.Replace("\"", "\"\"");
                        }
                        csv.Append($"\"{value}\"");
                    }
                    else
                    {
                        csv.Append("\"\"");
                    }
                }
                else
                {
                    csv.Append("\"\"");
                }
            }
            
            csv.AppendLine();
        }

        // 使用 UTF-8 with BOM 写入文件
        File.WriteAllText(filePath, csv.ToString(), new UTF8Encoding(true));
        
        // 刷新资源数据库
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 转义 CSV 字段中的特殊字符
    /// </summary>
    private static string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // 将引号转义为两个引号（CSV 标准转义方式）
        return value.Replace("\"", "\"\"");
    }
}

