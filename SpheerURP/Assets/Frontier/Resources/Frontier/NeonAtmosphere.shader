Shader "Frontier/NeonAtmosphere"
{
    // A see-through fresnel shell: fully clear in the middle, glowing only near the silhouette.
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.4,1,1,1)
        _Power ("Edge Power", Float) = 3.5
        _Intensity ("Intensity", Float) = 1.2
        _Inner ("Inner Haze", Range(0,0.3)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Power;
                float _Intensity;
                float _Inner;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(vp.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float ndv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float f = pow(1.0 - ndv, _Power);
                float a = saturate(f * _Intensity + _Inner * (1.0 - ndv));
                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
