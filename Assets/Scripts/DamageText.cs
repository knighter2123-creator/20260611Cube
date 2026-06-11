using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    public static bool showDamage = true;
    
    //Set Font size
    private float minFontSize;
    private float sizeChangeSpeed;
    
    //Set LifeTime
    private float moveSpeed = 0.15f;
    private float alphaSpeed = 1.5f;
    public float destroyTime = 0.4f;
    
    //Set Timer
    private float time;
    
    //Set Damage Text
    private string damage;

    Color alpha;
    private TextMeshPro txt;

    private void Awake()
    {
        if (gameObject.name == "DmgText")
        {
            minFontSize = 1.2f;
            sizeChangeSpeed = 2f;
        }
        else
        {
            minFontSize = 1.4f;
            sizeChangeSpeed = 1.5f;
        }
    }
    void Start()
    {
        time = 0;
        txt = GetComponent<TextMeshPro>();
        txt.text = damage;
        alpha = txt.color;
        Destroy(gameObject, destroyTime);
    }

    void Update()
    {
        // DmgTxt를 위로 떠오르게 한다
        transform.Translate(new Vector3(0, moveSpeed * Time.deltaTime, 0));

        if (time < 0.2f)
        {
            txt.fontSize += Time.deltaTime * sizeChangeSpeed;
        }
        else
        {
            // 최소 크기보다 작거나 같지 않으면 폰트 크기를 계속 줄인다.
            if (!(txt.fontSize >= minFontSize))
            {
                txt.fontSize -= Time.deltaTime * sizeChangeSpeed;
            }
        }

        time += Time.deltaTime * sizeChangeSpeed;
        
        alpha.a = Mathf.Lerp(alpha.a, 0, Time.deltaTime * alphaSpeed);
        txt.color = alpha;
    }

    public void SetDamage(int amount)
    {
        damage = amount.ToString();
    }
    public static void ToggleDamageText(bool isOn)
    {
        showDamage = isOn;  // 단순하게 그냥 대입
        Debug.Log($"대미지 텍스트 {(isOn ? "활성화" : "비활성화")}");
    }
}