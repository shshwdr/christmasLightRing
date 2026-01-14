using System.Collections.Generic;

/// <summary>
/// 游戏数据（需要持久化到文件的数据）
/// </summary>
[System.Serializable]
public class GameData
{
    // 教程强制棋盘设置（需要持久化）
    public bool tutorialForceBoard = true;
    
    // 设置数据
    public float sfxVolume = 1f;
    public float musicVolume = 1f;
    public int fullscreenMode = 0; // 0: Fullscreen, 1: FullscreenWindow, 2: Windowed
    
    // 游戏进度数据（需要持久化）
    public int currentLevel = 1;
    public string currentScene = "";
    
    // 已完成的scene列表（不会在reset时清除）
    public List<string> completedScenes = new List<string>();
}
