Shader "K1L0/FrostedGlass"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.15, 0.18, 0.25, 0.12)
        _RimColor ("Rim Color", Color) = (0.6, 0.7, 0.9, 0.3)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _NoiseScale ("Noise Scale", Range(1, 100)) = 40
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "FrostedGlass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _RimColor;
                float _RimPower;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);

                // Fresnel rim
                float ndotv = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - ndotv, _RimPower);

                // Noise overlay
                float n = noise(input.uv * _NoiseScale);

                // Combine
                float4 col = _TintColor;
                col.rgb += n * _NoiseStrength;
                col.rgb += _RimColor.rgb * rim * _RimColor.a;
                col.a += rim * _RimColor.a * 0.3;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
