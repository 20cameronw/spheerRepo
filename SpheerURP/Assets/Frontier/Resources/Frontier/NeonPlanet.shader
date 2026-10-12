Shader "Frontier/NeonPlanet"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.2,0.8,0.4,1)
        _AltColor ("Alt", Color) = (0.02,0.2,0.1,1)
        [HDR] _RimColor ("Rim", Color) = (0.4,1.5,1,1)
        _Bands ("Bands", Float) = 6
        _RimPower ("Rim Power", Float) = 2.5
        _Seed ("Seed", Float) = 0
        _Center ("Center (object space)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AltColor;
                half4 _RimColor;
                float _Bands;
                float _RimPower;
                float _Seed;
                float4 _Center;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float3 posOS : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(vp.positionWS);
                o.posOS = normalize(i.positionOS.xyz - _Center.xyz + float3(0, 1e-4, 0)) * 0.5;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewWS);
                float fres = pow(saturate(1.0 - dot(n, v)), _RimPower);
                float3 L = normalize(float3(-0.5, 0.6, -0.7));
                float ndl = saturate(dot(n, L) * 0.5 + 0.5);

                float p = i.posOS.y * _Bands + sin(i.posOS.x * 4.0 + _Seed) * 0.9 + sin(i.posOS.z * 5.0 + _Seed * 1.7) * 0.7;
                float band = sin(p) * 0.5 + 0.5;
                float3 col = lerp(_AltColor.rgb, _BaseColor.rgb, band);
                col *= 0.25 + 0.9 * ndl * ndl;

                float lat = abs(frac(i.posOS.y * 5.0) - 0.5);
                float latLine = smoothstep(0.46, 0.5, lat);
                col += _RimColor.rgb * (fres + latLine * 0.35);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
