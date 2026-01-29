using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Settings;
using UnityEditor.Localization;

/// <summary>
/// Unity Localization编辑器工具
/// 提供两个功能：
/// 1. 从场景中提取TMP_Text的text作为key保存到CSV
/// 2. 根据TMP_Text的text更新LocalizeStringEvent的StringReference
/// </summary>
public class LocalizationEditor
{
    private const string CSV_PATH = "Assets/Localization/GameText.csv";
    private const string TABLE_NAME = "GameText";
    
    /// <summary>
    /// 功能1：提取场景中所有TMP_Text的text作为key保存到CSV
    /// </summary>
    [MenuItem("Tools/Localization/提取Text到CSV")]
    public static void ExtractTextsToCSV()
    {
        // 获取当前场景中所有的TMP_Text组件
        TMP_Text[] allTexts = Object.FindObjectsOfType<TMP_Text>(true);
        
        // 读取现有CSV文件
        HashSet<string> existingKeys = new HashSet<string>();
        List<string[]> csvLines = new List<string[]>();
        
        if (File.Exists(CSV_PATH))
        {
            string[] lines = File.ReadAllLines(CSV_PATH);
            if (lines.Length > 0)
            {
                // 读取表头
                csvLines.Add(ParseCSVLine(lines[0]));
                
                // 读取现有数据
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;
                        
                    string[] columns = ParseCSVLine(lines[i]);
                    if (columns.Length > 0)
                    {
                        string key = columns[0].Trim('"');
                        existingKeys.Add(key);
                        csvLines.Add(columns);
                    }
                }
            }
            else
            {
                // 如果没有表头，创建默认表头
                csvLines.Add(new string[] { "Key", "Id", "Chinese (Simplified)(zh-Hans)", "English(en)" });
            }
        }
        else
        {
            // 创建新CSV文件，添加表头
            csvLines.Add(new string[] { "Key", "Id", "Chinese (Simplified)(zh-Hans)", "English(en)" });
        }
        
        // 收集需要添加的新keys
        List<string> newKeys = new List<string>();
        int addedCount = 0;
        
        foreach (TMP_Text text in allTexts)
        {
            if (text == null)
                continue;
                
            // 检查是否有LocalizeStringEvent组件且enabled
            LocalizeStringEvent localizeEvent = text.GetComponent<LocalizeStringEvent>();
            if (localizeEvent == null || !localizeEvent.enabled)
                continue;
            
            string textContent = text.text;
            if (string.IsNullOrWhiteSpace(textContent))
                continue;
            
            // 如果key不存在，添加到列表
            if (!existingKeys.Contains(textContent))
            {
                newKeys.Add(textContent);
                existingKeys.Add(textContent);
                addedCount++;
            }
        }
        
        // 生成新的ID（简单递增，实际应该使用Unity的ID生成系统）
        long nextId = 1000000000000; // 从一个大数开始
        if (csvLines.Count > 1)
        {
            // 尝试从现有行中获取最大ID
            for (int i = 1; i < csvLines.Count; i++)
            {
                if (csvLines[i].Length > 1)
                {
                    if (long.TryParse(csvLines[i][1], out long id))
                    {
                        if (id >= nextId)
                            nextId = id + 1;
                    }
                }
            }
        }
        
        // 添加新行到CSV
        foreach (string key in newKeys)
        {
            string[] newRow = new string[4];
            newRow[0] = "\"" + key + "\""; // Key列
            newRow[1] = nextId.ToString(); // Id列
            newRow[2] = "\"\""; // Chinese列，留空
            newRow[3] = "\"" + key + "\""; // English列，使用key作为值
            csvLines.Add(newRow);
            nextId++;
        }
        
        // 写入CSV文件
        using (StreamWriter writer = new StreamWriter(CSV_PATH, false, System.Text.Encoding.UTF8))
        {
            foreach (string[] line in csvLines)
            {
                writer.WriteLine(string.Join(",", line));
            }
        }
        
        // 刷新Asset数据库
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("提取完成", 
            $"成功提取 {addedCount} 个新的文本key到CSV文件。\n文件路径: {CSV_PATH}", 
            "确定");
        
        Debug.Log($"LocalizationEditor: 提取了 {addedCount} 个新的文本key到CSV");
    }
    
    /// <summary>
    /// 功能2：根据TMP_Text的text更新LocalizeStringEvent的StringReference
    /// </summary>
    [MenuItem("Tools/Localization/更新StringReference")]
    public static void UpdateStringReferences()
    {
        // 使用LocalizationEditorSettings获取StringTableCollection
        StringTableCollection tableCollection = null;
        
        try
        {
            // 方法1：尝试通过LocalizationEditorSettings获取（字符串会自动隐式转换为TableReference）
            tableCollection = LocalizationEditorSettings.GetStringTableCollection(TABLE_NAME);
        }
        catch
        {
            // 如果失败，尝试通过AssetDatabase直接加载
            string[] guids = AssetDatabase.FindAssets($"t:StringTableCollection {TABLE_NAME}");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                tableCollection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            }
        }
        
        if (tableCollection == null)
        {
            EditorUtility.DisplayDialog("错误", $"未找到名为 '{TABLE_NAME}' 的StringTableCollection。", "确定");
            return;
        }
        
        // 获取当前场景中所有的TMP_Text组件
        TMP_Text[] allTexts = Object.FindObjectsOfType<TMP_Text>(true);
        
        int updatedCount = 0;
        int notFoundCount = 0;
        
        foreach (TMP_Text text in allTexts)
        {
            if (text == null)
                continue;
                
            // 检查是否有LocalizeStringEvent组件且enabled
            LocalizeStringEvent localizeEvent = text.GetComponent<LocalizeStringEvent>();
            if (localizeEvent == null || !localizeEvent.enabled)
                continue;
            
            string textContent = text.text;
            if (string.IsNullOrWhiteSpace(textContent))
                continue;
            
            // 在StringTable中查找匹配的key
            SharedTableData sharedTableData = tableCollection.SharedData;
            if (sharedTableData == null)
                continue;
            
            // 查找key
            long? entryId = null;
            foreach (var entry in sharedTableData.Entries)
            {
                if (entry.Key == textContent)
                {
                    entryId = entry.Id;
                    break;
                }
            }
            
            if (entryId.HasValue)
            {
                // 更新StringReference
                SerializedObject serializedObject = new SerializedObject(localizeEvent);
                SerializedProperty stringReferenceProperty = serializedObject.FindProperty("m_StringReference");
                
                if (stringReferenceProperty != null)
                {
                    // 设置TableReference
                    SerializedProperty tableReferenceProperty = stringReferenceProperty.FindPropertyRelative("m_TableReference");
                    if (tableReferenceProperty != null)
                    {
                        SerializedProperty tableCollectionNameProperty = tableReferenceProperty.FindPropertyRelative("m_TableCollectionName");
                        if (tableCollectionNameProperty != null)
                        {
                            // 获取TableCollection的GUID
                            string tablePath = AssetDatabase.GetAssetPath(tableCollection);
                            string tableGuid = AssetDatabase.AssetPathToGUID(tablePath);
                            tableCollectionNameProperty.stringValue = $"GUID:{tableGuid}";
                        }
                    }
                    
                    // 设置TableEntryReference
                    SerializedProperty tableEntryReferenceProperty = stringReferenceProperty.FindPropertyRelative("m_TableEntryReference");
                    if (tableEntryReferenceProperty != null)
                    {
                        SerializedProperty keyIdProperty = tableEntryReferenceProperty.FindPropertyRelative("m_KeyId");
                        SerializedProperty keyProperty = tableEntryReferenceProperty.FindPropertyRelative("m_Key");
                        
                        if (keyIdProperty != null)
                        {
                            keyIdProperty.longValue = entryId.Value;
                        }
                        
                        if (keyProperty != null)
                        {
                            keyProperty.stringValue = textContent;
                        }
                    }
                    
                    serializedObject.ApplyModifiedProperties();
                    updatedCount++;
                    
                    Debug.Log($"更新了 {text.gameObject.name} 的StringReference，key: {textContent}");
                }
            }
            else
            {
                notFoundCount++;
                Debug.LogWarning($"未找到key '{textContent}' 在StringTable中，跳过 {text.gameObject.name}");
            }
        }
        
        // 标记场景为已修改
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
        
        EditorUtility.DisplayDialog("更新完成", 
            $"成功更新 {updatedCount} 个StringReference。\n未找到的key: {notFoundCount} 个", 
            "确定");
        
        Debug.Log($"LocalizationEditor: 更新了 {updatedCount} 个StringReference，{notFoundCount} 个未找到");
    }
    
    /// <summary>
    /// 解析CSV行，处理引号内的逗号
    /// </summary>
    private static string[] ParseCSVLine(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 转义的引号
                    currentField.Append('"');
                    i++; // 跳过下一个引号
                }
                else
                {
                    // 切换引号状态
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // 字段分隔符
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        // 添加最后一个字段
        fields.Add(currentField.ToString());
        
        return fields.ToArray();
    }
}

