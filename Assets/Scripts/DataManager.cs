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
            // 同步HashSet到List
            GameManager.Instance.gameData.SyncShownTutorials();
            GameManager.Instance.gameData.SyncReadStories();
            
            // 从SettingsMenu获取设置数据
            if (SettingsMenu.Instance != null)
            {
                GameManager.Instance.gameData.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
                GameManager.Instance.gameData.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
                GameManager.Instance.gameData.fullscreenMode = PlayerPrefs.GetInt("FullscreenMode", 0);
            }
            
            // 序列化为JSON
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
                    // 复制数据到GameManager
                    GameManager.Instance.gameData = loadedData;
                    
                    // 恢复HashSet
                    if (GameManager.Instance.gameData.shownTutorials != null)
                    {
                        GameManager.Instance.gameData.GetShownTutorials().Clear();
                        foreach (string tutorial in GameManager.Instance.gameData.shownTutorials)
                        {
                            GameManager.Instance.gameData.GetShownTutorials().Add(tutorial);
                        }
                    }
                    
                    if (GameManager.Instance.gameData.readStories != null)
                    {
                        GameManager.Instance.gameData.GetReadStories().Clear();
                        foreach (string story in GameManager.Instance.gameData.readStories)
                        {
                            GameManager.Instance.gameData.GetReadStories().Add(story);
                        }
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
}

