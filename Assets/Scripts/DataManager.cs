using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 数据管理器，负责保存和加载游戏数据
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager Instance;
    
    private const string SAVE_FILE_NAME = "gamedata.json";
    private string saveFilePath;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 设置保存文件路径
            saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 保存游戏数据到JSON文件
    /// </summary>
    public void SaveGameData()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager.Instance is null, cannot save game data.");
            return;
        }
        
        try
        {
            // 从TutorialManager获取tutorialForceBoard
            if (TutorialManager.Instance != null)
            {
                GameManager.Instance.gameData.tutorialForceBoard = TutorialManager.Instance.tutorialForceBoard;
            }
            
            // 从SettingsMenu获取设置数据
            if (SettingsMenu.Instance != null)
            {
                GameManager.Instance.gameData.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
                GameManager.Instance.gameData.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
                GameManager.Instance.gameData.fullscreenMode = PlayerPrefs.GetInt("FullscreenMode", 0);
            }
            
            // 序列化为JSON（只保存GameData，不包含mainGameData）
            string json = JsonUtility.ToJson(GameManager.Instance.gameData, true);
            
            // 写入文件
            File.WriteAllText(saveFilePath, json);
            
            Debug.Log($"Game data saved to: {saveFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game data: {e.Message}");
        }
    }
    
    /// <summary>
    /// 从JSON文件加载游戏数据
    /// </summary>
    public void LoadGameData()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager.Instance is null, cannot load game data.");
            return;
        }
        
        try
        {
            if (File.Exists(saveFilePath))
            {
                // 读取文件
                string json = File.ReadAllText(saveFilePath);
                
                // 反序列化
                GameData loadedData = JsonUtility.FromJson<GameData>(json);
                
                if (loadedData != null)
                {
                    // 复制数据到GameManager（只加载GameData，mainGameData不序列化）
                    GameManager.Instance.gameData = loadedData;
                    
                    // 恢复tutorialForceBoard到TutorialManager
                    if (TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.tutorialForceBoard = GameManager.Instance.gameData.tutorialForceBoard;
                    }
                    
                    // 恢复设置数据到PlayerPrefs
                    PlayerPrefs.SetFloat("SFXVolume", GameManager.Instance.gameData.sfxVolume);
                    PlayerPrefs.SetFloat("MusicVolume", GameManager.Instance.gameData.musicVolume);
                    PlayerPrefs.SetInt("FullscreenMode", GameManager.Instance.gameData.fullscreenMode);
                    PlayerPrefs.Save();
                    
                    // 应用设置
                    if (SettingsMenu.Instance != null)
                    {
                        SettingsMenu.Instance.ApplyLoadedSettings();
                    }
                    
                    Debug.Log($"Game data loaded from: {saveFilePath}");
                }
            }
            else
            {
                Debug.Log("No save file found, using default game data.");
                // 确保使用默认值
                if (GameManager.Instance != null)
                {
                    // GameData 的默认值已经在声明时设置，但需要确保 tutorialForceBoard 为 true
                    GameManager.Instance.gameData.tutorialForceBoard = true;
                    
                    // 同步到TutorialManager
                    if (TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.tutorialForceBoard = true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game data: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取保存文件路径（用于调试）
    /// </summary>
    public string GetSaveFilePath()
    {
        return saveFilePath;
    }
    
    /// <summary>
    /// 检查是否存在存档文件
    /// </summary>
    public bool HasSaveFile()
    {
        return File.Exists(saveFilePath);
    }
    
    /// <summary>
    /// 静态方法：直接删除存档文件（用于编辑器或测试，不需要GameManager实例）
    /// </summary>
    public static void DeleteSaveFile()
    {
        string saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        
        try
        {
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
                Debug.Log($"存档文件已删除: {saveFilePath}");
            }
            else
            {
                Debug.Log("未找到存档文件。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除存档文件失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 清除所有存档数据（包括游戏数据、教程、故事等）
    /// </summary>
    public void ClearAllSaveData()
    {
        try
        {
            // 删除存档文件
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
                Debug.Log($"Save file deleted: {saveFilePath}");
            }
            
            // 重置GameData到初始状态
            if (GameManager.Instance != null)
            {
                // mainGameData不序列化，每次游戏启动都会重新初始化，不需要清除
                
                // 重置GameData为默认值
                GameManager.Instance.gameData.tutorialForceBoard = true;
                GameManager.Instance.gameData.sfxVolume = 1f;
                GameManager.Instance.gameData.musicVolume = 1f;
                GameManager.Instance.gameData.fullscreenMode = 0;
                
                // 同步到TutorialManager
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.tutorialForceBoard = true;
                }
                
                Debug.Log("All game data cleared.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to clear save data: {e.Message}");
        }
    }
}

