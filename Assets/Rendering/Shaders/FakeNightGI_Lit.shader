Shader "KiloWorld/FakeNightGI_Lit"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)

        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1.0

        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5

        [HDR] _EmissionColor("Emission", Color) = (0,0,0)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        // Fake GI tuning (set globally via KiloSettings, not per-material)
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Global shader properties set by RenderManager
            float3 _KiloMoonDir;
            float3 _KiloMoonColor;
            float3 _KiloSkyColor;
            float3 _KiloGroundColor;
            float _VerticalFalloff;
            float _BounceStrength;
            float _SkyStrength;
            float _NightLift;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample textures
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseColor = albedoAlpha.rgb * _BaseColor.rgb;

                // Normal mapping
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3x3 tangentToWorld = half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));

                // World position
                float3 worldPos = input.positionWS;

                // ========================================
                // FAKE GLOBAL ILLUMINATION (NIGHT ONLY)
                // ========================================

                // Height gradient mask
                float heightMask = saturate(worldPos.y * _VerticalFalloff);

                // Direct moonlight (Lambert diffuse)
                float NdotM = saturate(dot(normalWS, _KiloMoonDir));
                float3 directLight = _KiloMoonColor * NdotM;

                // Apply shadows to direct light
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                directLight *= mainLight.shadowAttenuation;

                // Fake ground bounce (stronger near ground)
                float3 groundBounce = _KiloGroundColor * (1.0 - heightMask) * _BounceStrength;

                // Fake sky ambient (stronger higher up)
                float3 skyAmbient = _KiloSkyColor * heightMask * _SkyStrength;

                // Night lift (constant base illumination to prevent pure black)
                float3 liftedShadow = baseColor * _NightLift;

                // Combine all fake GI components
                float3 fakeGI = directLight + groundBounce + skyAmbient + liftedShadow;

                // Apply fake GI to base color
                float3 litColor = baseColor * fakeGI;

                // ========================================
                // EMISSION (ADDED AFTER GI, NOT MULTIPLIED)
                // ========================================
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                litColor += emission;

                // ========================================
                // SPECULAR (Simple Blinn-Phong for moonlight)
                // ========================================
                float3 viewDirWS = normalize(GetCameraPositionWS() - worldPos);
                float3 halfDir = normalize(_KiloMoonDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(10 * _Smoothness + 1);
                float spec = pow(NdotH, specPower) * _Smoothness;
                litColor += _KiloMoonColor * spec * mainLight.shadowAttenuation * (1.0 - _Metallic);

                // Fog
                litColor = MixFog(litColor, input.fogFactor);

                return half4(litColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
