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
        _Style ("Style", Float) = 0
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
                float _Style;
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
                float3 q = i.posOS * 2.0;
                float pat;
                if (_Style < 0.5)
                    pat = sin(p) * 0.5 + 0.5;
                else if (_Style < 1.5)
                {
                    float sw = atan2(q.z, q.x) * 2.0 + q.y * _Bands * 1.6 + sin(q.y * 7.0 + _Seed) * 1.6;
                    pat = sin(sw + sin(q.x * 6.0 + _Seed) * 1.8) * 0.5 + 0.5;
                }
                else if (_Style < 2.5)
                {
                    float3 c = q * (3.0 + _Bands * 0.5) + _Seed;
                    float3 f = frac(c) - 0.5;
                    float d = length(f);
                    float h = frac(sin(dot(floor(c), float3(12.9898, 78.233, 37.719))) * 43758.5453);
                    pat = (h > 0.45) ? smoothstep(0.38, 0.30, d) * 0.9 : sin(p) * 0.15 + 0.5;
                }
                else if (_Style < 3.5)
                {
                    float v = sin(q.x * 9.0 + sin(q.y * 8.0 + _Seed) * 2.0) * sin(q.z * 9.0 + sin(q.x * 7.0) * 2.0);
                    pat = 1.0 - smoothstep(0.0, 0.18, abs(v));
                }
                else
                {
                    float3 c = floor(q * (4.0 + _Bands * 0.4) + _Seed);
                    pat = frac(sin(dot(c, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                }
                float band = pat;
                float3 col = lerp(_AltColor.rgb, _BaseColor.rgb, band);
                col *= 0.25 + 0.9 * ndl * ndl;
                if (_Style > 2.5 && _Style < 3.5) col += _RimColor.rgb * pat * 0.6;
                if (_Style > 3.5) col += _RimColor.rgb * band * 0.12;

                float lat = abs(frac(i.posOS.y * 5.0) - 0.5);
                float latLine = smoothstep(0.46, 0.5, lat);
                col += _RimColor.rgb * (fres + latLine * 0.35);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
