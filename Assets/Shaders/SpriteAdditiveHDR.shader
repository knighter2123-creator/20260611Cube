// URP 2D Renderer 용 가산(Additive) 스프라이트 셰이더
// _Color 를 HDR(1 초과)로 올리면 Bloom 이 반응합니다.
//
// ★ 이번 수정 — 2D SRP Batcher 경고 해결
//   경고: "Material 'Glow_AdditiveHDR' has _TexelSize / _ST texture properties
//          which are not supported by 2D SRP Batcher."
//
//   UnityPerMaterial 묶음에서 _MainTex_ST 를 빼고, TRANSFORM_TEX 대신
//   버텍스 UV 를 그대로 쓰도록 바꿨습니다. 겉모습은 완전히 동일합니다.
//   자세한 이유는 아래 CBUFFER 위 주석에 적어두었습니다.
Shader "Custom/2D/SpriteAdditiveHDR"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Color (HDR)", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull  Off
        ZWrite Off
        Blend SrcAlpha One          // 가산 합성 : 텍스처 알파가 곧 밝기 기여도

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ──────────────────────────────────────────────────────────
            //  UnityPerMaterial — SRP Batcher 가 통째로 GPU 에 올리는 묶음
            // ──────────────────────────────────────────────────────────
            //
            // [왜 _MainTex_ST 를 뺐는가 — 학습 포인트]
            //
            // SRP Batcher 는 "같은 셰이더를 쓰는 오브젝트들의 머티리얼 상수를
            // 미리 GPU 버퍼에 올려두고 한 번에 그리는" 최적화입니다.
            // 그 버퍼가 바로 이 UnityPerMaterial 이에요.
            //
            // 그런데 URP **2D** 렌더러의 배처는 이 묶음 안에 텍스처 부가 정보
            // (_ST = 타일링/오프셋, _TexelSize = 텍스처 픽셀 크기)가 들어 있으면
            // 처리하지 못하고, 이 머티리얼을 쓰는 2D 렌더러 전체를 배칭에서 제외합니다.
            // 그때 나오는 게 그 경고입니다.
            //
            // [빼도 괜찮은 이유]
            // _MainTex 가 [PerRendererData] 입니다. 즉 텍스처를 머티리얼이 아니라
            // SpriteRenderer 가 매번 꽂아줍니다. 그리고 스프라이트는 아틀라스 안의
            // 자기 영역에 맞는 UV 가 **메시에 이미 구워져** 들어옵니다.
            // 그래서 타일링/오프셋을 다시 곱할 일이 애초에 없었습니다.
            // TRANSFORM_TEX 는 (1,1,0,0) 을 곱하고 더하는, 아무 일도 안 하는 연산이었어요.
            //
            // 남은 _Color 는 진짜 머티리얼 속성이라 그대로 둡니다.
            // (Properties 에 선언한 non-texture 속성은 전부 여기 있어야
            //  SRP Batcher 호환이 유지됩니다 — 하나라도 빠지면 다시 배칭에서 빠집니다)

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;      // HDR : 1을 넘을 수 있으므로 half 대신 float
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv         = IN.uv;     // 스프라이트 메시의 UV 를 그대로 사용
                OUT.color      = IN.color;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 c   = tex * IN.color * _Color;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}