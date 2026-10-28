Shader "Frontier/StructureOutline"
{
    Properties
    {
        [HDR] _Color ("Outline", Color) = (0.2,2,2,1)
        _Width ("Width (world units)", Float) = 0.018
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 position = TransformObjectToWorld(i.positionOS.xyz);
                position += TransformObjectToWorldNormal(i.normalOS) * _Width;
                o.positionCS = TransformWorldToHClip(position);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
