Shader "K1L0/ZossWindows"
{
    // Per-window vaporwave palette for building emissive (window) surfaces.
    // Each texture-tile cell hashes to its own color from a fixed palette and
    // its own lit/dark state, so facades read as hundreds of individual rooms
    // instead of one uniform glow sheet. _PaletteMix 0 reproduces the old
    // single-color behavior; _LitFraction thins the lit windows out.
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor("Emission", Color) = (0,0,0)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        _LitFraction("Lit Fraction", Range(0,1)) = 0.72
        _PaletteMix("Palette Mix", Range(0,1)) = 1.0
        _PaletteSaturation("Palette Saturation", Range(0,1.5)) = 1.0
        _Warmth("Warm Palette Mix", Range(0,1)) = 1.0
        _AccentFraction("Neon Accent Fraction", Range(0,0.5)) = 0.08
        _WindowBrightness("Window Brightness", Range(0.1,2)) = 1.0
        _BrightnessJitter("Brightness Jitter", Range(0,1)) = 0.5
        _BrightnessJitterRate("Brightness Jitter Rate", Range(0.05,4)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _LitFraction;
                half _PaletteMix;
                half _PaletteSaturation;
                half _Warmth;
                half _AccentFraction;
                half _WindowBrightness;
                half _BrightnessJitter;
                half _BrightnessJitterRate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                // The Zoss building generator writes each window quad's random
                // brightness into UV1 (constant across the quad). It doubles as
                // a stable per-window ID for the palette hash.
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvTiled : TEXCOORD0;
                float fogCoord : TEXCOORD1;
                float objSeed : TEXCOORD2;
                float windowValue : TEXCOORD3;
                float viewDistance : TEXCOORD4;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Pure anomaly palette. The independently live-tunable _Warmth
            // and _AccentFraction controls decide whether/how often this is
            // used; keeping this function pure avoids a hidden hardcoded 15%
            // accent allocation fighting those controls.
            half3 windowPalette(float t)
            {
                if (t < 0.32) return half3(1.00, 0.22, 0.62);
                if (t < 0.52) return half3(0.85, 0.25, 1.00);
                if (t < 0.70) return half3(0.15, 0.85, 1.00);
                if (t < 0.84) return half3(1.00, 0.45, 0.72);
                if (t < 0.96) return half3(1.00, 0.88, 0.72);
                return half3(0.72, 0.30, 1.00);
            }

            half3 warmWindowPalette(float t)
            {
                if (t < 0.30) return half3(1.00, 0.50, 0.10);
                if (t < 0.65) return half3(1.00, 0.66, 0.20);
                if (t < 0.90) return half3(1.00, 0.78, 0.34);
                return half3(1.00, 0.58, 0.14);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uvTiled = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                output.windowValue = input.uv1.x;
                // Per-mesh-chunk seed so identical facades don't repeat.
                float3 objOrigin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                output.objSeed = hash21(objOrigin.xz + objOrigin.y);
                output.viewDistance = distance(_WorldSpaceCameraPos, positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvTiled);
                half4 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uvTiled);

                // Textures act as LUMINANCE masks only. Multiplying the palette
                // by the warm-tinted window texture turned pink/cyan into muddy
                // browns and greens.
                half baseLuma = dot(baseSample.rgb, half3(0.299, 0.587, 0.114));
                half emissionMask = dot(emissionSample.rgb, half3(0.299, 0.587, 0.114));

                // One color per window quad, keyed off the per-window brightness
                // the generator bakes into UV1 — perfectly aligned to the real
                // window geometry (world/UV cells cut across quads as blocks).
                float windowValue = max(input.windowValue, 0.02);
                float cellHash = frac(sin(windowValue * 341.17 + input.objSeed * 12.9898) * 43758.5453);
                float litHash = frac(sin(windowValue * 173.31 + input.objSeed * 78.233 + 1.234) * 24634.6345);

                half3 neonColor = windowPalette(cellHash);
                half3 warmColor = warmWindowPalette(cellHash);
                half3 paletteColor = lerp(neonColor, warmColor, _Warmth);
                half accent = cellHash >= (1.0h - _AccentFraction) ? 1.0h : 0.0h;
                paletteColor = lerp(paletteColor, neonColor, accent);
                half paletteLuma = dot(paletteColor, half3(0.299, 0.587, 0.114));
                paletteColor = lerp(half3(paletteLuma, paletteLuma, paletteLuma), paletteColor, _PaletteSaturation);

                // _EmissionColor carries the global day/night intensity ramp;
                // reuse its magnitude so the palette dims at noon exactly like
                // the old uniform glow did. windowValue restores the generator's
                // per-room brightness variance and bottom-floor darkening.
                half emissionLevel = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
                half3 uniformGlow = _EmissionColor.rgb;
                half3 variedGlow = paletteColor * emissionLevel;
                half3 glow = lerp(uniformGlow, variedGlow, _PaletteMix) * half(saturate(windowValue)) * _WindowBrightness;

                // Simplified distant facades concentrate the same emissive area into
                // fewer, larger quads. Attenuate only those far windows so they do not
                // collapse into a solid yellow bloom bank while nearby rooms stay vivid.
                half farFade = half(smoothstep(240.0, 850.0, input.viewDistance));
                glow *= lerp(1.0h, 0.28h, farFade);

                // Per-window brightness jitter: each window flickers between
                // 0 and 1 on its own phase + frequency so no two rooms pulse
                // in sync. _BrightnessJitter blends between "always full" (0)
                // and "full 0..1 swing" (1). Rate scales the global speed.
                float phaseHash = frac(sin(windowValue * 91.71 + input.objSeed * 37.42 + 5.71) * 12983.1245);
                float freqHash = frac(sin(windowValue * 47.23 + input.objSeed * 61.17 + 9.31) * 8641.8837);
                float perWindowFreq = _BrightnessJitterRate * (0.35 + freqHash * 1.30);
                // Sin sweep floored so the dimmest point of the flicker cycle
                // never fully extinguishes the window — even at full jitter
                // strength a room retains a faint occupied glow.
                const float JITTER_FLOOR = 0.10;
                float sweep = 0.5 + 0.5 * sin(_Time.y * perWindowFreq + phaseHash * 6.2831853);
                float flicker = JITTER_FLOOR + (1.0 - JITTER_FLOOR) * sweep;
                half jitterMul = lerp(1.0h, half(flicker), _BrightnessJitter);
                glow *= jitterMul;

                // Some rooms are simply dark.
                half lit = litHash < _LitFraction ? 1.0h : 0.04h;

                half3 color = _BaseColor.rgb * baseLuma * 0.18h
                            + glow * emissionMask * lit;
                color = MixFog(color, input.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Depth-only pass so buildings keep occluding correctly.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half depthFrag(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
