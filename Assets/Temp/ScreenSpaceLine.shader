Shader "Custom/ScreenSpaceLine"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _WidthPixels ("Width Pixels", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ScreenSpaceLine"

            Cull Off

            // 仍然接受場景深度測試
            ZTest LEqual

            // 線本身不寫入深度
            ZWrite Off

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WidthPixels;
            CBUFFER_END

            struct Attributes
            {
                // 目前端點
                float4 positionOS : POSITION;

                // x 儲存 -1 或 +1
                float2 lineData : TEXCOORD0;

                // 同一線段的另一個端點
                float4 otherPositionOS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 目前端點轉到 Clip Space
                float4 clipA =
                    TransformObjectToHClip(input.positionOS.xyz);

                // 另一個端點轉到 Clip Space
                float4 clipB =
                    TransformObjectToHClip(input.otherPositionOS.xyz);

                // Clip Space 轉成 NDC
                float2 ndcA = clipA.xy / clipA.w;
                float2 ndcB = clipB.xy / clipB.w;

                // 將線段方向換成 Pixel 空間
                float2 directionPixels =
                    (ndcB - ndcA) *
                    0.5 *
                    _ScreenParams.xy;

                float directionLength =
                    length(directionPixels);

                // 避免兩個點重疊時除以零
                if (directionLength > 0.0001)
                {
                    directionPixels /= directionLength;

                    // 線段的垂直方向
                    float2 normalPixels =
                        float2(
                            -directionPixels.y,
                             directionPixels.x
                        );

                    // 完整寬度 2 px，左右各 1 px
                    float halfWidthPixels =
                        _WidthPixels * 0.5;

                    // Pixel 位移換成 NDC 位移
                    float2 offsetNDC =
                        normalPixels *
                        halfWidthPixels *
                        (2.0 / _ScreenParams.xy);

                    // lineData.x 是 -1 或 +1
                    offsetNDC *= input.lineData.x;

                    // NDC 位移轉回 Clip Space
                    clipA.xy += offsetNDC * clipA.w;
                }

                output.positionHCS = clipA;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}