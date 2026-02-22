Shader "Custom/SeamlessRoadWorldSpace"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        _EmissionMap("Emission Map", 2D) = "white" {}
        
        [Header(Screen Space Reflection)]
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 0.5
        _ReflectionYOffset("Reflection Y Offset (Meters)", Float) = 15.0
        _ReflectionDistortion("Reflection Distortion", Range(0, 0.2)) = 0.05
        _ReflectionWarble("Horizontal Warble", Range(0, 0.1)) = 0.02
        _ReflectionWarbleScale("Warble Scale", Float) = 5.0
        
        [Header(Puddles)]
        _PuddleColor("Puddle Color", Color) = (0,0,0,1)
        _PuddleScale("Puddle Frequency (Size)", Float) = 0.5
        _PuddleSpread("Puddle Amount", Range(0, 1)) = 0.5
        _PuddleSharpness("Puddle Edge Sharpness", Range(1, 10)) = 3.0

        [Toggle] _DebugReflection("Debug Reflection Only", Float) = 0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
    }
    SubShader
    {
        // Changed Queue to Transparent to render AFTER the Opaque Copy (Scene Color) pass
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Transparent" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            // Force ZWrite On so the road is treated as solid despite the Transparent queue
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _EMISSION
            
            // CRITICAL FIX: Enable Forward+ (Required because RenderingMode is set to 2 in PC_Renderer)
            #pragma multi_compile_fragment _ _FORWARD_PLUS

            // CRITICAL FIX: Ensure Reflection Probes and Light Probes are compiled
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ LIGHTMAP_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                float4 screenPos : TEXCOORD6;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                float _BumpScale;
                float _OcclusionStrength;
                half4 _EmissionColor;
                float4 _BaseMap_ST;
                
                float _ReflectionStrength;
                float _ReflectionYOffset;
                float _ReflectionDistortion;
                float _ReflectionWarble;
                float _ReflectionWarbleScale;
                float4 _PuddleColor;
                float _PuddleScale;
                float _PuddleSpread;
                float _PuddleSharpness;
                float _DebugReflection;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            
            // Camera Opaque Texture (Scene Color)
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            float4 _CameraOpaqueTexture_TexelSize;

            // Simple Noise Function for Puddles
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
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                // Calculate Vertex Lighting (for lights not processing per-pixel)
                output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);

                output.screenPos = ComputeScreenPos(output.positionCS);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. World Space UVs (Planar XZ) ---
                float2 uv = input.positionWS.xz * _BaseMap_ST.xy + _BaseMap_ST.zw;

                // --- 2. Sample Textures ---
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                
                #ifdef _EMISSION
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, uv).rgb * _EmissionColor.rgb;
                #else
                half3 emission = 0;
                #endif

                // --- 3. Construct SurfaceData ---
                // This struct tells the lighting model about the material properties
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = 0; // Not used in Metallic workflow (calculated internally)
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = albedoAlpha.a;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                // --- 4. Construct InputData ---
                // This struct tells the lighting model about the geometry and context
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                
                // Normal Handling
                half3 meshNormal = normalize(input.normalWS);
                
                // Robust Tangent Generation for World-Space Planar Mapping
                // Since our UVs are XZ, we want a tangent frame aligned with the world XZ plane.
                // Tangent = World X (1,0,0), Bitangent = World Z (0,0,1) roughly.
                // But we must orthogonalize against the actual mesh normal to handle slopes.
                
                half3 worldTangent = half3(1, 0, 0);
                half3 tangentWS = normalize(worldTangent - meshNormal * dot(worldTangent, meshNormal));
                // If normal is parallel to World X, use World Z
                if (length(tangentWS) < 0.001)
                {
                    tangentWS = normalize(half3(0, 0, 1) - meshNormal * dot(half3(0, 0, 1), meshNormal));
                }
                
                half3 bitangentWS = normalize(cross(meshNormal, tangentWS));
                half3x3 tbn = half3x3(tangentWS, bitangentWS, meshNormal);
                
                inputData.normalWS = normalize(mul(normalTS, tbn));

                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = input.vertexLighting;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // --- DISABLE REAL SPECULAR ---
                // We zero out smoothness and metallic for the official URP PBR call
                // so that directional/point lights don't create bright specular spots.
                float puddleSmoothness = surfaceData.smoothness;
                surfaceData.smoothness = 0;
                surfaceData.metallic = 0;

                // Calculate PBR Lighting (now diffuse-only)
                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                
                // Restore smoothness for our fake reflection math
                surfaceData.smoothness = puddleSmoothness;
                
                // --- HOMEMADE REFLECTION PROBE (Puddle & Warp) ---
                
                // A. Generate Puddle Mask using World Space Noise
                // Layered noise for more organic, patchy distribution (1-2m sized patches on 8-10m road)
                float noise1 = noise(input.positionWS.xz * _PuddleScale);
                float noise2 = noise(input.positionWS.xz * (_PuddleScale * 0.43) + float2(12.5, 3.1));
                float puddleNoise = noise1 * noise2; // Multiply to create sparser, more organic patches
                
                // Contrast/Spread control
                float puddleMask = smoothstep(1.0 - _PuddleSpread, 1.0, puddleNoise);

                // Apply sharpness to create defined edges (1 = soft gradient, 10 = hard edges)
                puddleMask = pow(puddleMask, _PuddleSharpness);

                // --- B. Puddle Color ---
                // Blend road color to Puddle Color based on mask
                finalColor.rgb = lerp(finalColor.rgb, _PuddleColor.rgb, puddleMask);

                // C. Reflection Coordinates (Vertical Offset)
                // Sample screen space area of a world space ~15m "above" this point
                float3 reflectionTargetWS = input.positionWS + float3(0, _ReflectionYOffset, 0);
                float4 reflectionClipPos = TransformWorldToHClip(reflectionTargetWS);
                
                // Safety: If point is behind camera, don't reflect it
                float validProjection = step(0.0, reflectionClipPos.w);
                
                float4 reflectionScreenPos = ComputeScreenPos(reflectionClipPos);
                float2 reflectionUV = reflectionScreenPos.xy / max(reflectionScreenPos.w, 0.0001);
                
                // D. Warp / Stretch
                // 1. Normal Distortion (Bumpy road)
                float2 normalDistort = surfaceData.normalTS.xy * _ReflectionDistortion;
                
                // 2. Ripple/Stretch (Sin wave distortion)
                float2 ripple;
                // Horizontal Warble based on World Z (distance down the road)
                ripple.x = sin(input.positionWS.z * _ReflectionWarbleScale) * _ReflectionWarble;
                ripple.y = cos(input.positionWS.x * 5.0) * 0.005; // Vertical stretch/wobble
                
                // Combine: Puddles get Ripple, Road gets Bump
                float2 finalDistortion = lerp(normalDistort, ripple, puddleMask * 0.8);
                
                // Apply distortion
                reflectionUV += finalDistortion;

                // E. Sample Opaque
                // Clamp to valid screen area to avoid sampling border garbage
                reflectionUV = clamp(reflectionUV, 0.001, 0.999);
                half3 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, reflectionUV).rgb;
                
                // F. Masking
                // STRICT Reflection: Only reflect in puddles.
                float reflectionMask = puddleMask * _ReflectionStrength * validProjection;
                
                // Edge Fade: Soften reflections near screen edges to hide the "clamp" and "pop"
                float edgeFade = smoothstep(0.0, 0.15, reflectionUV.y) * smoothstep(1.0, 0.85, reflectionUV.y); 
                // (Horizontal fade is less critical but good for corners)
                edgeFade *= smoothstep(0.0, 0.05, reflectionUV.x) * smoothstep(1.0, 0.95, reflectionUV.x);
                
                reflectionMask *= edgeFade;
                
                if (_DebugReflection > 0.5)
                {
                    // Visualize Puddles (White) vs Road (Black)
                    return half4(puddleMask, puddleMask, puddleMask, 1.0);
                }

                // Additive blend
                finalColor.rgb += sceneColor * reflectionMask;
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // ShadowCaster Pass (Needed for the road to cast shadows on itself/others if needed, though usually roads receive)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}