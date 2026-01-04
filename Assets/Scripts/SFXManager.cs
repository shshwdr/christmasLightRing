using UnityEngine;

/// <summary>
/// 音效管理器，负责播放游戏中的各种音效
/// </summary>
public class SFXManager : Singleton<SFXManager>
{
    private AudioSource audioSource;
    private AudioSource loopAudioSource; // 用于播放循环音效的 AudioSource
    private float baseVolume = 1f; // 基础音量，用于保存用户设置
    
    protected override void Awake()
    {
        base.Awake();
        
        // 创建或获取 AudioSource 组件（用于播放一次性音效）
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // 设置 AudioSource 属性
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        // 从PlayerPrefs加载音量设置
        baseVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        audioSource.volume = baseVolume;
        
        // 创建用于循环音效的 AudioSource
        GameObject loopAudioObject = new GameObject("LoopAudioSource");
        loopAudioObject.transform.SetParent(transform);
        loopAudioSource = loopAudioObject.AddComponent<AudioSource>();
        loopAudioSource.playOnAwake = false;
        loopAudioSource.loop = true;
    }
    
    /// <summary>
    /// 播放邪恶卡牌音效
    /// </summary>
    /// <param name="identifier">卡牌标识符（如 "nun", "snowman", "nutcracker" 等）</param>
    /// <param name="soundType">音效类型：normal（显示时）、atk（攻击时）、hurt（被灯光照开时）</param>
    public void PlayEnemySound(string identifier, string soundType = "normal")
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.LogWarning("SFXManager: Enemy identifier is null or empty");
            return;
        }
        
        // 处理 identifier 映射：enemy 和 grinch 都映射到 nutcracker
        string soundIdentifier = identifier;
        if (identifier == "enemy" || identifier == "grinch")
        {
            soundIdentifier = "nutcracker";
        }
        
        string soundName = $"enemies_ {soundIdentifier}_{soundType}";
        string path = $"sfx/enemies/{soundName}";
        PlaySound(path);
    }
    
    /// <summary>
    /// 播放卡牌揭示音效
    /// </summary>
    /// <param name="identifier">卡牌标识符（如 "coin", "gift", "bell" 等）</param>
    public void PlayCardRevealSound(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.LogWarning("SFXManager: Card identifier is null or empty");
            return;
        }
        
        string soundName = $"card_ {identifier}";
        string path = $"sfx/cards/{soundName}";
        PlaySound(path);
    }
    
    /// <summary>
    /// 播放其他音效
    /// </summary>
    /// <param name="identifier">音效标识符（如 "finishLevel", "lightOn", "buyItem" 等）</param>
    public void PlaySFX(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.LogWarning("SFXManager: SFX identifier is null or empty");
            return;
        }
        
        string soundName = $"sfx_{identifier}";
        string path = $"sfx/sfx/{soundName}";
        PlaySound(path);
    }
    
    /// <summary>
    /// 播放点击音效
    /// </summary>
    public void PlayClickSound()
    {
        PlaySound("sfx/sfx/sfx_click");
    }
    
    /// <summary>
    /// 播放循环音效（用于 light 等需要循环播放的音效）
    /// </summary>
    /// <param name="identifier">音效标识符</param>
    public void PlayLoopSFX(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.LogWarning("SFXManager: Loop SFX identifier is null or empty");
            return;
        }
        
        if (loopAudioSource == null)
        {
            Debug.LogWarning("SFXManager: LoopAudioSource is null");
            return;
        }
        
        // 如果已经在播放，先停止
        if (loopAudioSource.isPlaying)
        {
            loopAudioSource.Stop();
        }
        
        string soundName = $"sfx_{identifier}";
        string path = $"sfx/sfx/{soundName}";
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"SFXManager: Audio clip not found at path: {path}");
            return;
        }
        
        loopAudioSource.clip = clip;
        loopAudioSource.Play();
    }
    
    /// <summary>
    /// 停止循环音效
    /// </summary>
    public void StopLoopSFX()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
        {
            loopAudioSource.Stop();
        }
    }

    private int sfxCount = 0;
    /// <summary>
    /// 内部方法：从 Resources 加载并播放音效
    /// </summary>
    /// <param name="path">音效资源路径（相对于 Resources 文件夹）</param>
    private void PlaySound(string path)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("SFXManager: AudioSource is null");
            return;
        }
        
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"SFXManager: Audio clip not found at path: {path}");
            return;
        }

        sfxCount++;
        if (sfxCount <= 1)
        {
            return;
        }
        
        audioSource.PlayOneShot(clip);
    }
    
    /// <summary>
    /// 设置音效音量（0.0 到 1.0）
    /// </summary>
    public void SetVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = baseVolume;
        }
        
        // 保存到PlayerPrefs
        PlayerPrefs.SetFloat("SFXVolume", baseVolume);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 获取当前音效音量
    /// </summary>
    public float GetVolume()
    {
        return baseVolume;
    }
}

