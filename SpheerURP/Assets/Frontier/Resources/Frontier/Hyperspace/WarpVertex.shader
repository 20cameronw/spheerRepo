Shader "Frontier/Hyperspace/WarpVertex"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float depth : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.depth = input.positionOS.z;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half fog = saturate((input.depth - 25) / 150);
                return half4(lerp(input.color.rgb, half3(0.012, 0.02, 0.055), fog), input.color.a);
            }
            ENDHLSL
        }
    }
}
