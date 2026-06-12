using UnityEngine;
using UnityEngine.UI;

public class EnemyHpBar : MonoBehaviour
{
    [SerializeField] Slider slider;

    public void UpdateHp(float current, float max)
    {
        if (slider != null)
            slider.value = current / max;
    }
}