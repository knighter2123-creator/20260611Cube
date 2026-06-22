using UnityEngine;

public class PlayerBuffManager : MonoBehaviour
{
    public static PlayerBuffManager Instance { get; private set; }

    private const string DamageBuffKey = "PlayerDamageBuff";

    public float DamageMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();   // 저장된 누적 버프 복원
    }

    public void AddPermanentDamageBuff(float percent)
    {
        DamageMultiplier += percent;
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(DamageBuffKey, DamageMultiplier);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        DamageMultiplier = PlayerPrefs.GetFloat(DamageBuffKey, 1f); // 기본값 1
    }
}