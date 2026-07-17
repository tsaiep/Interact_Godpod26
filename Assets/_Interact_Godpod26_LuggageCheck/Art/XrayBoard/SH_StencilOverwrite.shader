Shader "Custom/URP/StencilOverwrite"
{
    Properties
    {
        _StencilRef ("Stencil Reference", Integer) = 2
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
            Name "Stencil Writer"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // 不寫入任何顏色通道
            ColorMask 0

            // 不寫入深度
            ZWrite Off

            // 只有未被前方深度遮擋的部分才寫入
            ZTest LEqual

            Cull Back

            Stencil
            {
                // 從 Material 取得 Stencil 值
                Ref [_StencilRef]

                ReadMask 255
                WriteMask 255

                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}