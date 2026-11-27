// 敵と味方の HP ゲージ用の単色 unlit シェーダー（spec 015 FR-042）。深度を見ずに最後に描くので、ほかの敵や体に隠れない。
// 色は _BaseColor（BattleActorsView が MaterialPropertyBlock で入れる）。Overlay の列は遠い順に描くので、中身は背景よりカメラ寄りに置く。
Shader "UnitySlotTower/HP Gauge"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
