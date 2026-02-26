using UnityEngine;

/// <summary>
/// 二选一显示：object1 与 object2 在 Inspector 中指定，根据状态只显示其中一个。
/// 用于分支进度等：完成时显示 object1，未完成时显示 object2。
/// </summary>
public class ToggleObject : MonoBehaviour
{
    [Tooltip("完成/第一种状态时显示")]
    public GameObject object1;
    [Tooltip("未完成/第二种状态时显示")]
    public GameObject object2;

    /// <summary>
    /// true 显示 object1、隐藏 object2；false 显示 object2、隐藏 object1。
    /// </summary>
    public void SetState(bool showObject1)
    {
        if (object1 != null) object1.SetActive(showObject1);
        if (object2 != null) object2.SetActive(!showObject1);
    }
}
