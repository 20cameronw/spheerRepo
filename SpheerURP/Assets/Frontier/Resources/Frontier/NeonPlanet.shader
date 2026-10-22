Shader "Frontier/NeonPlanet"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.2,0.8,0.4,1)
        _AltColor ("Alt", Color) = (0.02,0.2,0.1,1)
        _CloudColor ("Clouds and ice", Color) = (0.85,0.94,1,1)
        [HDR] _RimColor ("Rim", Color) = (0.4,1.5,1,1)
        _Bands ("Bands", Float) = 6
        _Biome ("Biome", Float) = 0
        _CloudCoverage ("Cloud coverage", Range(0,1)) = 0
        _PolarCaps ("Polar caps", Range(0,1)) = 0
        _RimPower ("Rim Power", Float) = 2.5
        _Seed ("Seed", Float) = 0
        _Center ("Center (object space)", Vector) = (0,0,0,0)
        _Style ("Style", Float) = 0
        _Lines ("Latitude lines", Float) = 1
        _CraterSoft ("Terrain between craters (style 2)", Range(0,1)) = 0
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
                half4 _CloudColor;
                half4 _RimColor;
                float _Bands;
                float _Biome;
                float _CloudCoverage;
                float _PolarCaps;
                float _RimPower;
                float _Seed;
                float4 _Center;
                float _Style;
                float _Lines;
                float _CraterSoft;
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
                    float disc = smoothstep(0.38, 0.30, d);
                    float ground = sin(p) * 0.15 + 0.5;
                    // _CraterSoft 0: a cell with a crater falls to the alt colour around its disc (the original look); 1: the terrain shows between the discs.
                    pat = (h > 0.45) ? lerp(disc * 0.9, lerp(ground, 0.9, disc), _CraterSoft) : ground;
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

                float cloudField = 0.5
                    + 0.25 * sin(q.x * 7.0 + sin(q.y * 4.0 + _Seed) * 1.8 + _Seed)
                    + 0.25 * sin(q.z * 7.5 + sin(q.x * 3.5 + _Seed * 1.3) * 1.5);
                float clouds = smoothstep(0.45, 0.68, cloudField) * _CloudCoverage;
                col = lerp(col, _CloudColor.rgb * (0.4 + 0.6 * ndl), clouds * 0.72);
                float caps = smoothstep(0.32, 0.39, abs(i.posOS.y)) * _PolarCaps;
                col = lerp(col, _CloudColor.rgb, caps * 0.88);

                float longitude = atan2(i.posOS.z, i.posOS.x);
                float tectonic = abs(sin(longitude * 8.0 + sin(i.posOS.y * 13.0 + _Seed) * 1.4));
                float fissures = (1.0 - smoothstep(0.025, 0.11, tectonic))
                    * (0.55 + 0.45 * sin(i.posOS.y * 37.0 + longitude * 3.0 + _Seed) * sin(i.posOS.x * 29.0));
                if (abs(_Biome - 3.0) < 0.5)
                    col += float3(2.0, 0.32, 0.035) * saturate(fissures);
                else if (abs(_Biome - 2.0) < 0.5)
                {
                    float iceVeins = 1.0 - smoothstep(0.025, 0.12, abs(sin(longitude * 12.0 + sin(i.posOS.y * 9.0 + _Seed))));
                    col = lerp(col, _CloudColor.rgb * (0.7 + 0.5 * ndl), iceVeins * 0.55);
                }
                else if (abs(_Biome - 5.0) < 0.5)
                {
                    float crystalVeins = 1.0 - smoothstep(0.015, 0.075, abs(sin(longitude * 16.0 + i.posOS.y * 11.0 + _Seed)));
                    col += _RimColor.rgb * crystalVeins * 0.8;
                }
                else if (abs(_Biome - 8.0) < 0.5)
                {
                    float dunes = sin(i.posOS.y * 38.0 + sin(longitude * 4.0 + _Seed) * 2.5);
                    col *= 0.82 + 0.18 * smoothstep(-0.25, 0.7, dunes);
                }

                float lat = abs(frac(i.posOS.y * 5.0) - 0.5);
                float latLine = smoothstep(0.46, 0.5, lat);
                col += _RimColor.rgb * (fres + latLine * 0.35 * _Lines);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
