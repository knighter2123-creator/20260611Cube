using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TalkManager : MonoBehaviour
{
    public TextMeshPro talkText;
    public GameObject talkPrefab;
    void Start()
    {
        
    }

    
    public void TalkAction(GameObject talk)
    {
        talkPrefab = talk;
        talkText.text = "이것의 이름은 " + talkPrefab.name + "이라고 한다.";
    }
}
