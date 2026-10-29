// URP 2D Renderer 용 "텍스처에 색을 더하는" 스프라이트 셰이더
//   _AddColor  : 더할 색
//   _AddAmount : 얼마나 더할지 (0 = 원본, 1 = 최대)
//   _Alpha     : 전체 페이드
//
// 투명한 배경까지 물드는 걸 막기 위해 더하는 양에 텍스처 알파를 곱합니다.
//
// _MainTex_ST(타일링·오프셋)는 두지 않습니다 — 2D SRP Batcher 가 머티리얼 CBUFFER 의 _ST / _TexelSize 를 지원하지 않아
// 있으면 이 머티리얼을 쓰는 스프라이트의 배칭이 꺼집니다. 스프라이트 텍스처는 SpriteRenderer 가 넣으므로 타일링은 늘 (1,1,0,0).
Shader "Custom/2D/SpriteAddTexColor"
{
    Properties
    {
        [PerRendererData][NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color     ("Tint", Color) = (1,1,1,1)
        [HDR] _AddColor  ("Add Color", Color) = (0.15, 0.85, 0.30, 1)
        _AddAmount ("Add Amount", Range(0,1)) = 0
        _Alpha     ("Alpha", Range(0,1)) = 1
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
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _AddColor;   // HDR 가능하므로 float
                float  _AddAmount;
                float  _Alpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                // 알파를 곱해 스프라이트의 불투명한 영역에만 색을 더함
                c.rgb += _AddColor.rgb * saturate(_AddAmount) * c.a;
                c.a   *= saturate(_Alpha);

                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
