using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenMissionButton : MonoBehaviour
{
    [SerializeField] private MissionPopup missionPopup;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (missionPopup != null) missionPopup.Open();
        });
    }
}