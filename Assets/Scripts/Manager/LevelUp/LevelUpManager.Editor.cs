// LevelUpManager 의 에디터 전용 테스트 partial. 빌드에는 포함되지 않습니다.

#if UNITY_EDITOR
using System.Text;
using UnityEngine;

public partial class LevelUpManager
{
    /// <summary>
    /// 두 곡선의 레벨별 공격 주기 / 초당 공격 횟수를 콘솔에 표로 찍습니다.
    /// Play 하지 않아도 동작합니다 (스탯이 아니라 공식만 쓰기 때문).
    /// </summary>
    [ContextMenu("테스트: 공격 속도 곡선 출력")]
    private void DumpAttackSpeedCurves()
    {
        AttackSpeedCurve saved = attackSpeedCurve;
        var sb = new StringBuilder();
        sb.AppendLine("[공격 속도 곡선]   레벨  |  LinearMs (ms / 초당)  |  LinearSpeed (ms / 초당)");

        try
        {
            for (int lv = 0; lv <= MAX_UPGRADE_LEVEL; lv += 500)
            {
                attackSpeedCurve = AttackSpeedCurve.LinearMs;
                float a = AttackSpdForLevel(lv);

                attackSpeedCurve = AttackSpeedCurve.LinearSpeed;
                float b = AttackSpdForLevel(lv);

                sb.AppendLine($"  Lv.{lv,4}  |  {a,7:0.0}ms / {1000f / a,5:0.00}회  |  {b,7:0.0}ms / {1000f / b,5:0.00}회");
            }
        }
        finally
        {
            // 비교하느라 잠깐 바꾼 설정을 반드시 원래대로 돌려놓습니다.
            attackSpeedCurve = saved;
        }

        Debug.Log(sb.ToString());
    }
}
#endif
