using System.Collections.Generic;
using UnityEngine;

public class HpBar : MonoBehaviour
{
    [SerializeField] GameObject _monsterHPbar;

    List<Transform>  monsterTransforms      = new List<Transform>();
    List<GameObject> multipleMonsterHPbars  = new List<GameObject>();

    Camera        mainCamera;
    RectTransform canvasRect;

    void Start()
    {
        mainCamera = Camera.main;
        canvasRect = GetComponent<RectTransform>();

        // ✅ EnemyRespawn이 RegisterEnemy()를 호출하므로 여기서 중복 등록 제거
        // (씬에 미리 배치된 Enemy가 있을 때만 필요하면 아래 주석 해제)
        // foreach (GameObject monster in GameObject.FindGameObjectsWithTag("Enemy"))
        //     RegisterEnemy(monster);
    }

    public void RegisterEnemy(GameObject monster)
    {
        // ✅ 이미 등록된 Enemy면 스킵 (중복 방지)
        if (monsterTransforms.Contains(monster.transform)) return;

        monsterTransforms.Add(monster.transform);
        GameObject hpbar = Instantiate(_monsterHPbar, transform);
        multipleMonsterHPbars.Add(hpbar);

        monster.GetComponent<Enemy>()?.SetHpBar(hpbar);
    }

    void Update()
    {
        for (int i = multipleMonsterHPbars.Count - 1; i >= 0; i--)
        {
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