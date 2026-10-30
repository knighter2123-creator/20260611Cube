using System.Collections;
using UnityEngine;

/// <summary>
/// 머티리얼마다 투명한 스프라이트를 카메라 앞에 한 프레임 그려서, 셰이더의 GPU 프로그램을 미리 만들어 둡니다.
///
/// ─── 왜 머티리얼을 읽어 두는 것만으론 부족한가? (학습 포인트) ─────────────
/// Resources.Load / new Material 은 "CPU 쪽 데이터" 만 준비합니다.
/// 셰이더를 GPU 가 실행할 수 있는 형태(프로그램/파이프라인 상태)로 만드는 건
/// 그 머티리얼로 "처음 그리는 순간" 입니다. 흑백(기절) 셰이더는 첫 기절 때 처음 쓰이므로
/// 그 프레임에 이 비용이 몰려 화면이 멈칫했습니다.
///
/// ShaderVariantCollection.WarmUp 도 있지만 Vulkan/Metal 에서는 정점 레이아웃을 몰라
/// 완전하지 않습니다. 실제 SpriteRenderer 로 한 번 그리면 모든 그래픽 API 에서 확실합니다.
/// 색 알파를 0 으로 두므로 화면에는 아무것도 보이지 않습니다.
///
/// ─── 왜 머티리얼마다 두 번 그리는가 ────────────────────────────────────
/// 같은 머티리얼이라도 렌더러에 MaterialPropertyBlock 이 붙어 있으면 SRP Batcher 를 못 타고 다른 경로로 그려집니다.
/// 어떤 경로로 그려질지 머티리얼만 보고는 알 수 없으므로, 한 경로만 예열해 '처음 그리는 순간' 비용이
/// 남는 일이 없게 둘 다 한 번씩 그립니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public static class RenderWarmup
{
    // 어느 셰이더에도 없는 이름 — 값은 아무 데도 쓰이지 않고, '블록이 붙어 있다' 는 사실만 필요합니다.
    private static readonly int WarmupPropertyId = Shader.PropertyToID("_RenderWarmup");

    public static IEnumerator Run(params Material[] materials)
    {
        Camera cam = Camera.main;
        if (cam == null || materials == null || materials.Length == 0) yield break;

        Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        GameObject[] objects = new GameObject[materials.Length * 2];

        Transform camTr = cam.transform;
        Vector3 pos = camTr.position + camTr.forward * (cam.nearClipPlane + 1f);

        var block = new MaterialPropertyBlock();
        block.SetFloat(WarmupPropertyId, 0f);

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null) continue;
            objects[i * 2]     = CreateDrawer(sprite, materials[i], pos, null);    // SRP Batcher 경로
            objects[i * 2 + 1] = CreateDrawer(sprite, materials[i], pos, block);   // MaterialPropertyBlock 경로
        }

        // 만든 프레임이 렌더링된 다음 프레임까지 기다렸다가 치웁니다.
        yield return null;
        yield return null;

        for (int i = 0; i < objects.Length; i++)
            if (objects[i] != null) Object.Destroy(objects[i]);
        Object.Destroy(sprite);
    }

    private static GameObject CreateDrawer(Sprite sprite, Material material, Vector3 position, MaterialPropertyBlock block)
    {
        // 씬이 언로드되면 같이 파괴되도록 DontSave 는 붙이지 않고, 계층 창에서만 숨깁니다.
        GameObject go = new GameObject("RenderWarmup") { hideFlags = HideFlags.HideInHierarchy };
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = sprite;
        sr.sharedMaterial = material;
        sr.color          = new Color(1f, 1f, 1f, 0f);   // 셰이더가 정점 색을 곱하므로 완전히 투명
        if (block != null) sr.SetPropertyBlock(block);
        return go;
    }
}
