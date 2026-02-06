using UnityEngine;

/// <summary>
/// 音乐管理器，负责播放背景音乐
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private AudioSource audioSource;
    private float baseVolume = 1f; // 基础音量，用于保存用户设置
    
    protected override void Awake()
    {
        base.Awake();
        
        // 创建或获取 AudioSource 组件
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 设置 AudioSource 属性
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = baseVolume;
        
        // 从PlayerPrefs加载音量设置
        baseVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        audioSource.volume = baseVolume;
    }
    
    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="musicPath">音乐资源路径（相对于 Resources 文件夹）</param>
    public void PlayMusic(string musicPath)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("MusicManager: AudioSource is null");
            return;
        }
        
        AudioClip clip = Resources.Load<AudioClip>(musicPath);
        if (clip == null)
        {
            Debug.LogWarning($"MusicManager: Audio clip not found at path: {musicPath}");
            return;
        }
        
        // 如果正在播放相同的音乐，不重复播放
        if (audioSource.clip == clip && audioSource.isPlaying)
        {
            return;
        }
        
        audioSource.clip = clip;
        audioSource.Play();
    }
    
    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
    
    /// <summary>
    /// 设置音乐音量（0.0 到 1.0）
    /// </summary>
    public void SetVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = baseVolume;
        }
        
        // 保存到PlayerPrefs
        PlayerPrefs.SetFloat("MusicVolume", baseVolume);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 获取当前音乐音量
    /// </summary>
    public float GetVolume()
    {
        return baseVolume;
    }
}














