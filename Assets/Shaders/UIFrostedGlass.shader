Shader "K1L0/UIFrostedGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlassTint ("Glass Tint", Color) = (0.06, 0.08, 0.12, 0.55)
        _BlurSize ("Blur Size", Range(0, 0.08)) = 0.02
        _BorderColor ("Border Color", Color) = (0.35, 0.4, 0.5, 0.3)
        _BorderWidth ("Border Width", Range(0, 0.08)) = 0.025
        _NoiseStrength ("Noise Strength", Range(0, 0.05)) = 0.01

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIFrostedGlass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Global scene capture texture set by K1L0SceneCapture
            TEXTURE2D(_K1L0SceneTex);
            SAMPLER(sampler_K1L0SceneTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _GlassTint;
                float _BlurSize;
                float4 _BorderColor;
                float _BorderWidth;
                float _NoiseStrength;
            CBUFFER_END

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sprite shape mask
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half mask = sprite.a * input.color.a;
                if (mask < 0.01) return half4(0, 0, 0, 0);

                // For ScreenSpaceOverlay: use SV_POSITION pixel coords directly
                float2 screenUV = input.positionCS.xy / _ScreenParams.xy;

                // Multi-ring blur of captured scene (17 taps)
                float2 b1 = float2(_BlurSize, _BlurSize);
                float2 b2 = b1 * 2.0;
                float2 b3 = b1 * 3.5;
                half3 scene = half3(0, 0, 0);
                // Center
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV).rgb * 0.14;
                // Ring 1 (4 taps)
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b1.x, 0)).rgb * 0.08;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b1.x, 0)).rgb * 0.08;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(0,  b1.y)).rgb * 0.08;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(0, -b1.y)).rgb * 0.08;
                // Ring 2 (4 taps diagonal)
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b2.x,  b2.y)).rgb * 0.06;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b2.x,  b2.y)).rgb * 0.06;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b2.x, -b2.y)).rgb * 0.06;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b2.x, -b2.y)).rgb * 0.06;
                // Ring 3 (4 taps cardinal, wide)
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b3.x, 0)).rgb * 0.04;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b3.x, 0)).rgb * 0.04;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(0,  b3.y)).rgb * 0.04;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(0, -b3.y)).rgb * 0.04;
                // Ring 3 diagonals
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b3.x,  b3.y)).rgb * 0.025;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b3.x,  b3.y)).rgb * 0.025;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2( b3.x, -b3.y)).rgb * 0.025;
                scene += SAMPLE_TEXTURE2D(_K1L0SceneTex, sampler_K1L0SceneTex, screenUV + float2(-b3.x, -b3.y)).rgb * 0.025;

                // Scene with minimal frost
                half3 glass = scene * 1.15 + half3(0.04, 0.045, 0.05);

                // Frosted noise texture
                float n = vnoise(input.uv * 80.0);
                glass += (n - 0.5) * _NoiseStrength;

                // Subtle top highlight (works on any shape via UV.y gradient)
                float topGrad = smoothstep(0.7, 0.0, input.uv.y);
                glass += topGrad * 0.05;

                // Edge highlight using sprite alpha falloff (shape-agnostic)
                float2 texelSize = float2(1.0 / 256.0, 1.0 / 256.0);
                half edgeL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x * 3.0, 0)).a;
                half edgeR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( texelSize.x * 3.0, 0)).a;
                half edgeU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0,  texelSize.y * 3.0)).a;
                half edgeD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -texelSize.y * 3.0)).a;
                half edgeFactor = saturate(1.0 - min(min(edgeL, edgeR), min(edgeU, edgeD)));
                glass += edgeFactor * _BorderColor.rgb * _BorderColor.a;

                return half4(glass, mask * 0.78);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
