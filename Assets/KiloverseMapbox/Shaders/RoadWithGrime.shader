Shader "KiloWorld/RoadWithGrime"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        
        [Header(Grime Settings)]
        _GrimeColor("Grime Color", Color) = (0.15, 0.12, 0.1, 1)
        _GrimeWidth("Grime Width", Range(0, 1)) = 0.3
        _GrimeIntensity("Grime Intensity", Range(0, 1)) = 0.9
        _GrimeNoiseScale("Noise Scale", Float) = 20.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                float4 _GrimeColor;
                float _GrimeWidth;
                float _GrimeIntensity;
                float _GrimeNoiseScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            // Simple Noise Function
            float hash(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0,0)), hash(i + float2(1,0)), f.x),
                            lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Sample Base
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 2. Calculate Grime Mask
                // Assuming UV.y is across the road width (0..1)
                // If UV.x is width, swap to input.uv.x
                // Mapbox usually uses V (y) for width.
                float distFromCenter = abs(input.uv.y - 0.5) * 2.0; // 0 at center, 1 at edges
                
                // Add noise to break up the straight line
                float n = noise(input.uv * _GrimeNoiseScale);
                float noisyEdge = distFromCenter + (n * 0.1); 

                // Threshold
                float grimeMask = smoothstep(1.0 - _GrimeWidth, 1.0, noisyEdge);
                
                // Blend Grime
                half3 finalAlbedo = lerp(albedo.rgb, _GrimeColor.rgb, grimeMask * _GrimeIntensity);
                // Grime reduces smoothness (dirt is matte)
                half finalSmoothness = lerp(_Smoothness, 0.0, grimeMask * _GrimeIntensity);

                // 3. Lighting Data
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // 4. BRDF Setup (PBR)
                BRDFData brdfData;
                InitializeBRDFData(finalAlbedo, _Metallic, half3(0.04, 0.04, 0.04), finalSmoothness, 1.0, brdfData);

                // 5. Calculate Lighting
                // Ambient / GI
                half3 color = GlobalIllumination(brdfData, inputData.bakedGI, 1.0, inputData.normalWS, inputData.viewDirectionWS);
                
                // Main Light
                Light mainLight = GetMainLight(input.shadowCoord);
                color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

                // Additional Lights
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    // Fix: GetAdditionalLight(index, positionWS, shadowMask) 
                    // We use inputData.shadowMask which is set to 1,1,1,1 above
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, inputData.shadowMask);
                    color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
                }
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}