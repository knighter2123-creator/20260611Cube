// URP 2D Renderer 용 스프라이트 흑백(Grayscale) 셰이더
// _GrayAmount : 0 = 원본, 1 = 완전 흑백
Shader "Custom/2D/SpriteGrayscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color      ("Tint", Color) = (1,1,1,1)
        _GrayAmount ("Gray Amount", Range(0,1)) = 1
        _GrayTint   ("Gray Tint", Color) = (0.75, 0.78, 0.85, 1)   // 살짝 푸른 잿빛
        _Brightness ("Brightness", Range(0,2)) = 0.85
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
                float4 _MainTex_ST;
                half4  _Color;
                half4  _GrayTint;
                half   _GrayAmount;
                half   _Brightness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // 휘도(luminance) 계산 후 잿빛 톤 적용
                half  lum  = dot(c.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 gray = lum * _GrayTint.rgb * _Brightness;

                c.rgb = lerp(c.rgb, gray, saturate(_GrayAmount));
                return c;
            }
            ENDHLSL
        }
    }

    // URP가 아닌 환경(Built-in)에서도 최소한 렌더되도록 하는 폴백
    Fallback "Sprites/Default"
}
