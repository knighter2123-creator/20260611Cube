using System.Collections.Generic;
using UnityEngine;

public class HpBar : MonoBehaviour
{
    [SerializeField] GameObject _monsterHPbar;

    List<Transform> monsterTransforms = new List<Transform>();
    List<GameObject> multipleMonsterHPbars = new List<GameObject>();

    Camera mainCamera;
    RectTransform canvasRect;

    void Start()
    {
        mainCamera = Camera.main;
        canvasRect = GetComponent<RectTransform>();

        foreach (GameObject monster in GameObject.FindGameObjectsWithTag("Enemy"))
            RegisterEnemy(monster);
    }

    // 동적 스폰된 Enemy 등록용
    public void RegisterEnemy(GameObject monster)
    {
        monsterTransforms.Add(monster.transform);
        GameObject hpbar = Instantiate(_monsterHPbar, transform);
        multipleMonsterHPbars.Add(hpbar);

        // Enemy에 HP바 연결
        monster.GetComponent<Enemy>()?.SetHpBar(hpbar);
    }

    void Update()
    {
        for (int i = multipleMonsterHPbars.Count - 1; i >= 0; i--)
        {
            // Enemy 또는 HP바가 삭제된 경우 리스트에서 제거
            if (monsterTransforms[i] == null || multipleMonsterHPbars[i] == null)
            {
                monsterTransforms.RemoveAt(i);
                multipleMonsterHPbars.RemoveAt(i);
                continue;
            }

            Vector3 screenPos = mainCamera.WorldToScreenPoint(
                monsterTransforms[i].position + new Vector3(0, 0.5f, 0)
            );

            multipleMonsterHPbars[i].SetActive(screenPos.z > 0);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out Vector2 localPoint
            );
            multipleMonsterHPbars[i].GetComponent<RectTransform>().anchoredPosition = localPoint;
        }
    }
}