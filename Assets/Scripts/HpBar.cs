using System.Collections.Generic;
using UnityEngine;

public class HpBar : MonoBehaviour
{
    // 몬스터 hp바 프리팹을 담을 변수를 선언해준다
    [SerializeField] GameObject _monsterHPbar;

    // 몬스터들의 위치를 담을 리스트를 선언해준다
    List<Transform> monsterTransforms;
    
    // 몬스터들의 hpBar 를 담을 리스트를 선언해준다
    List<GameObject> multipleMonsterHPbars;

    // 메인카메라를 담을 객체를 만들어준다
    Camera mainCamera;
    private void Awake()
    {
        // 초기화해준다
        monsterTransforms = new List<Transform>();
        multipleMonsterHPbars= new List<GameObject>();
    }

    void Start()
    {
        // 메인카메라 객체를 담아주고
        mainCamera= Camera.main;

        // 임시배열안에 Monster 라는 태그가 달린 게임오브젝트들을 담아준다
        GameObject[] temporaryArray = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject monster in temporaryArray)
        {
            // 각 몬스터의 transform을 위에 선언해준 몬스터위치 리스트에 담아준다
            monsterTransforms.Add(monster.transform);

            // 몬스터 체력바 오브젝트를 동적으로 생성해준다
            GameObject hpbar = Instantiate(_monsterHPbar, monster.transform.position, monster.transform.rotation, transform);
            
            // 만들어준 몬스터 체력바를 hpbar 리스트에 추가해준다
            multipleMonsterHPbars.Add(hpbar);
        }
    }

    void Update()
    {
        for (int i = 0; i < monsterTransforms.Count ;++i)
        {
            multipleMonsterHPbars[i].transform.position = mainCamera.WorldToScreenPoint(monsterTransforms[i].position + new Vector3(0, 2f, 0));
            // multipleMonsterHPbars[i].transform.position = mainCamera.ScreenToWorldPoint(monsterTransforms[i].position + new Vector3(0, 2f, 0));
        }
    }
}
